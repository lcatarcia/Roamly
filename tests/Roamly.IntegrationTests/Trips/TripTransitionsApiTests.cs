using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roamly.Common;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Trips;

[Collection(DatabaseCollection.Name)]
public sealed class TripTransitionsApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Email = "trip-transition@example.test";
    private const string Password = "Test!Password123";
    private static readonly DateTimeOffset ClockNow =
        new(2026, 10, 7, 23, 59, 30, TimeSpan.Zero);
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly FixedTimeProvider _clock = new(ClockNow);
    private RoamlyApiFactory _factory = null!;
    private RoamlyClient _client = null!;
    private DatabaseLease _database = null!;

    public async ValueTask InitializeAsync()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        _database = migrated.Lease;
        await _database.ResetAsync(TestContext.Current.CancellationToken);

        _factory = new RoamlyApiFactory(
            _database.ConnectionString,
            configureTestServices: services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_clock);
            });
        _client = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        await CreateUserAsync(_ownerId, Email);
        using var login = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task owner_can_start_a_planned_trip_and_receives_server_utc_actual_date_and_new_etag()
    {
        var camperId = NewId();
        using var camper = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id = camperId,
            name = "Il Grigio",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, camper.StatusCode);

        var tripId = NewId();
        using var created = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            new { id = tripId, name = "Partenza", stops = new[] { new { name = "Bologna" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var path = $"/api/v1/trips/{tripId:D}";
        using var initial = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var oldEtag = HttpEtagAssertions.StrongRowVersion(initial.Headers.ETag?.ToString());

        using var weakTransition = await SendTransitionAsync(
            path,
            1,
            HttpEtagAssertions.AsWeak(oldEtag));
        Assert.Equal(HttpStatusCode.PreconditionFailed, weakTransition.StatusCode);

        using var transitioned = await SendTransitionAsync(path, 1, oldEtag);
        Assert.True(
            transitioned.StatusCode == HttpStatusCode.OK,
            await transitioned.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.NotEqual(
            oldEtag,
            HttpEtagAssertions.StrongRowVersion(transitioned.Headers.ETag?.ToString()));
        using var transitionBody = JsonDocument.Parse(await transitioned.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(1, transitionBody.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            new DateOnly(2026, 10, 7),
            DateOnly.ParseExact(
                transitionBody.RootElement.GetProperty("actualStartOnUtc").GetString()!,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture));
        Assert.Equal(JsonValueKind.Null, transitionBody.RootElement.GetProperty("actualEndOnUtc").ValueKind);

        using var readBack = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, readBack.StatusCode);
        using var readBody = JsonDocument.Parse(await readBack.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(1, readBody.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            new DateOnly(2026, 10, 7),
            DateOnly.ParseExact(
                readBody.RootElement.GetProperty("actualStartOnUtc").GetString()!,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture));
        Assert.Equal(JsonValueKind.Null, readBody.RootElement.GetProperty("actualEndOnUtc").ValueKind);
    }

    [Fact]
    public async Task completing_uses_the_new_utc_day_and_reopening_preserves_start_and_clears_end()
    {
        var trip = await CreateTripAsync();
        var activeEtag = await TransitionAndGetEtagAsync(trip.Path, 1, trip.ETag);
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 8, 0, 0, 1, TimeSpan.Zero));

        var completedEtag = await TransitionAndGetEtagAsync(trip.Path, 2, activeEtag);
        var completed = await ReadStateAsync(trip.Path);
        Assert.Equal(2, completed.State.Status);
        Assert.Equal(new DateOnly(2026, 10, 7), completed.State.ActualStartOnUtc);
        Assert.Equal(new DateOnly(2026, 10, 8), completed.State.ActualEndOnUtc);

        using var reopenedResponse = await SendTransitionAsync(trip.Path, 1, completedEtag);
        Assert.Equal(HttpStatusCode.OK, reopenedResponse.StatusCode);
        Assert.NotEqual(completedEtag, reopenedResponse.Headers.ETag?.ToString());
        var reopened = await ReadStateAsync(trip.Path);
        Assert.Equal(1, reopened.State.Status);
        Assert.Equal(new DateOnly(2026, 10, 7), reopened.State.ActualStartOnUtc);
        Assert.Null(reopened.State.ActualEndOnUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task owner_can_cancel_a_planned_or_active_trip(int sourceStatus)
    {
        var trip = await CreateTripAsync();
        var etag = sourceStatus == 0
            ? trip.ETag
            : await TransitionAndGetEtagAsync(trip.Path, sourceStatus, trip.ETag);

        using var cancelled = await SendTransitionAsync(trip.Path, 3, etag);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.NotEqual(etag, cancelled.Headers.ETag?.ToString());
        var state = await ReadStateAsync(trip.Path);
        Assert.Equal(3, state.State.Status);
        Assert.Equal(sourceStatus == 1 ? new DateOnly(2026, 10, 7) : null, state.State.ActualStartOnUtc);
        Assert.Null(state.State.ActualEndOnUtc);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(3, 3)]
    public async Task illegal_or_same_state_transition_is_a_stable_conflict_without_mutation(
        int sourceStatus,
        int destinationStatus)
    {
        var trip = await CreateTripAsync();
        var etag = await TransitionToStatusAsync(trip.Path, sourceStatus, trip.ETag);
        var before = await ReadStateAsync(trip.Path);

        using var rejected = await SendTransitionAsync(trip.Path, destinationStatus, etag);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/invalid-trip-transition",
            problem.RootElement.GetProperty("type").GetString());

        var after = await ReadStateAsync(trip.Path);
        Assert.Equal(before.State, after.State);
        Assert.Equal(before.ETag, after.ETag);
    }

    [Fact]
    public async Task missing_and_malformed_preconditions_do_not_change_trip_state()
    {
        var trip = await CreateTripAsync();

        using var missing = await SendTransitionAsync(trip.Path, new { status = 1 }, null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        using var malformed = await SendTransitionAsync(trip.Path, new { status = 1 }, "not-an-etag");
        Assert.Equal(HttpStatusCode.PreconditionFailed, malformed.StatusCode);
        Assert.Equal(0, (await ReadStateAsync(trip.Path)).State.Status);

        var activeEtag = await TransitionAndGetEtagAsync(trip.Path, 1, trip.ETag);
        using var stale = await SendTransitionAsync(trip.Path, new { status = 2 }, trip.ETag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        var current = await ReadStateAsync(trip.Path);
        Assert.Equal(1, current.State.Status);
        Assert.Equal(new DateOnly(2026, 10, 7), current.State.ActualStartOnUtc);
        Assert.Null(current.State.ActualEndOnUtc);
        Assert.Equal(activeEtag, current.ETag);
    }

    [Fact]
    public async Task stale_trip_etag_after_a_database_change_returns_precondition_failed()
    {
        var trip = await CreateTripAsync();
        await ChangeTripOutsideRequestAsync(trip.Id);

        using var response = await SendTransitionAsync(trip.Path, 1, trip.ETag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

        var current = await ReadStateAsync(trip.Path);
        Assert.Equal(0, current.State.Status);
        Assert.Null(current.State.ActualStartOnUtc);
        Assert.Null(current.State.ActualEndOnUtc);
        Assert.NotEqual(trip.ETag, current.ETag);
    }

    [Fact]
    public async Task missing_or_unowned_trip_is_not_found_before_precondition_evaluation()
    {
        using var missing = await SendTransitionAsync(
            $"/api/v1/trips/{NewId():D}/transitions",
            new { status = 1 },
            null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var missingProblem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        var trip = await CreateTripAsync();
        var otherEmail = "other-trip-owner@example.test";
        await CreateUserAsync(Guid.NewGuid(), otherEmail);
        using var otherClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        using var login = await otherClient.LoginAsync(
            otherEmail,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var unowned = await SendTransitionAsync(otherClient, trip.Path, new { status = 1 }, null);
        Assert.Equal(HttpStatusCode.NotFound, unowned.StatusCode);
        using var unownedProblem = JsonDocument.Parse(await unowned.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(
            missingProblem.RootElement.GetProperty("type").GetString(),
            unownedProblem.RootElement.GetProperty("type").GetString());
        Assert.Equal(
            missingProblem.RootElement.GetProperty("title").GetString(),
            unownedProblem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task missing_undefined_and_malformed_destinations_are_bad_requests()
    {
        var trip = await CreateTripAsync();

        using var missing = await SendTransitionAsync(trip.Path, new { }, trip.ETag);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var undefined = await SendTransitionAsync(trip.Path, new { status = 99 }, trip.ETag);
        Assert.Equal(HttpStatusCode.BadRequest, undefined.StatusCode);
        using var malformed = await SendTransitionAsync(trip.Path, new { status = "Active" }, trip.ETag);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(0, (await ReadStateAsync(trip.Path)).State.Status);
        Assert.Equal(trip.ETag, (await ReadStateAsync(trip.Path)).ETag);
    }

    private Task<HttpResponseMessage> SendTransitionAsync(string path, int status, string? etag) =>
        SendTransitionAsync(_client, path, new { status }, etag);

    private Task<HttpResponseMessage> SendTransitionAsync(string path, object body, string? etag) =>
        SendTransitionAsync(_client, path, body, etag);

    private static async Task<HttpResponseMessage> SendTransitionAsync(
        RoamlyClient client,
        string path,
        object body,
        string? etag)
    {
        using var csrf = await client.GetAsync("/api/v1/csrf-token", TestContext.Current.CancellationToken);
        var token = await csrf.Content.ReadFromJsonAsync<CsrfTokenResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(token);
        var transitionPath = path.EndsWith("/transitions", StringComparison.Ordinal)
            ? path
            : path + "/transitions";
        using var request = new HttpRequestMessage(HttpMethod.Post, transitionPath)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add("X-XSRF-TOKEN", token.Token);
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<TripHandle> CreateTripAsync()
    {
        var camperId = NewId();
        using var camper = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id = camperId,
            name = "Il Grigio",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, camper.StatusCode);

        var tripId = NewId();
        using var created = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            new { id = tripId, name = "Viaggio", stops = new[] { new { name = "Bologna" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var path = $"/api/v1/trips/{tripId:D}";
        var initial = await ReadStateAsync(path);
        Assert.Equal(0, initial.State.Status);
        Assert.NotNull(initial.ETag);
        return new TripHandle(tripId, path, initial.ETag);
    }

    private async Task<TripSnapshot> ReadStateAsync(string path)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<TripState>(TestContext.Current.CancellationToken);
        Assert.NotNull(state);
        return new TripSnapshot(state, response.Headers.ETag?.ToString());
    }

    private async Task<string> TransitionAndGetEtagAsync(string path, int status, string etag)
    {
        using var response = await SendTransitionAsync(path, status, etag);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var nextEtag = response.Headers.ETag?.ToString();
        Assert.NotNull(nextEtag);
        Assert.NotEqual(etag, nextEtag);
        return nextEtag;
    }

    private async Task<string> TransitionToStatusAsync(string path, int status, string etag)
    {
        if (status == 0)
        {
            return etag;
        }

        if (status == 3)
        {
            return await TransitionAndGetEtagAsync(path, status, etag);
        }

        var activeEtag = await TransitionAndGetEtagAsync(path, 1, etag);
        return status switch
        {
            1 => activeEtag,
            2 => await TransitionAndGetEtagAsync(path, 2, activeEtag),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
    }

    private async Task CreateUserAsync(Guid userId, string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAtUtc = ClockNow.UtcDateTime,
        }, Password);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private async Task ChangeTripOutsideRequestAsync(Guid tripId)
    {
        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .Options;
        await using var db = new RoamlyDbContext(options, new TestCurrentUser(_ownerId));
        var trip = await db.Trips.SingleAsync(
            candidate => candidate.OwnerId == _ownerId && candidate.Id == tripId,
            TestContext.Current.CancellationToken);
        trip.Name = "Changed outside HTTP";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Guid NewId() => new SequentialGuidGenerator(TimeProvider.System).NewId();

    private sealed record CsrfTokenResponse(string Token);

    private sealed record TripHandle(Guid Id, string Path, string ETag);

    private sealed record TripState(int Status, DateOnly? ActualStartOnUtc, DateOnly? ActualEndOnUtc);

    private sealed record TripSnapshot(TripState State, string? ETag);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void SetUtcNow(DateTimeOffset utcNow) => _now = utcNow;
    }

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
        public bool IsAuthenticated => true;
    }
}
