using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Trips;

[Collection(DatabaseCollection.Name)]
public sealed class ListTripsApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Password = "Test!Password123";
    private readonly Guid _ownerId = Guid.NewGuid();
    private RoamlyApiFactory _factory = null!;
    private RoamlyClient _client = null!;
    private DatabaseLease _database = null!;

    public async ValueTask InitializeAsync()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        _database = migrated.Lease;
        await _database.ResetAsync(TestContext.Current.CancellationToken);

        _factory = new RoamlyApiFactory(_database.ConnectionString);
        _client = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));

        await CreateUserAsync(_ownerId, "trip-list-owner@example.test");
        using var login = await _client.LoginAsync(
            "trip-list-owner@example.test",
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
    public async Task traveller_can_list_trips_without_loading_stops()
    {
        var tripId = NewId();
        await CreateTripAsync(tripId, "Weekend in Bretagna");

        using var response = await _client.GetAsync(
            "/api/v1/trips?limit=10",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PageResponse<TripSummaryResponse>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(page);
        var trip = Assert.Single(page.Items);
        Assert.Equal(tripId, trip.Id);
        Assert.NotEqual(Guid.Empty, trip.CamperId);
        Assert.Equal("Weekend in Bretagna", trip.Name);
        Assert.Equal(0, trip.Status);
        Assert.Null(page.NextCursor);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(
            "stops",
            document.RootElement.GetProperty("items")[0]
                .EnumerateObject()
                .Select(property => property.Name),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task traveller_can_continue_a_trip_page_without_duplicates_or_gaps()
    {
        var tripIds = new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
        };
        for (var index = 0; index < tripIds.Length; index++)
        {
            await CreateTripAsync(tripIds[index], $"Trip {index}");
        }

        using var firstResponse = await _client.GetAsync(
            "/api/v1/trips?limit=2",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<PageResponse<TripSummaryResponse>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.NextCursor));

        using var secondResponse = await _client.GetAsync(
            $"/api/v1/trips?limit=2&after={Uri.EscapeDataString(firstPage.NextCursor!)}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<PageResponse<TripSummaryResponse>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(secondPage);
        Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextCursor);

        var listedIds = firstPage.Items.Select(trip => trip.Id)
            .Concat(secondPage.Items.Select(trip => trip.Id))
            .ToArray();
        Assert.Equal(tripIds.Order().ToArray(), listedIds);
        Assert.Equal(listedIds.Length, listedIds.Distinct().Count());
    }

    [Fact]
    public async Task traveller_only_sees_trips_owned_by_the_authenticated_user()
    {
        var ownTripId = NewId();
        await CreateTripAsync(_client, ownTripId, "My trip");

        var otherOwnerId = Guid.NewGuid();
        const string otherEmail = "other-trip-list-owner@example.test";
        await CreateUserAsync(otherOwnerId, otherEmail);
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
        await CreateTripAsync(otherClient, NewId(), "Someone else's trip");

        using var response = await _client.GetAsync(
            "/api/v1/trips",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PageResponse<TripSummaryResponse>>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        var trip = Assert.Single(page.Items);
        Assert.Equal(ownTripId, trip.Id);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task trip_collection_defaults_to_fifty_and_validates_limit_and_cursor()
    {
        await SeedTripsAsync(51);

        using var defaults = await _client.GetAsync(
            "/api/v1/trips",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, defaults.StatusCode);
        var page = await defaults.Content.ReadFromJsonAsync<PageResponse<TripSummaryResponse>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(page);
        Assert.Equal(50, page.Items.Count);
        Assert.NotNull(page.NextCursor);

        using var minimum = await _client.GetAsync(
            "/api/v1/trips?limit=1",
            TestContext.Current.CancellationToken);
        var minimumPage = await minimum.Content.ReadFromJsonAsync<PageResponse<TripSummaryResponse>>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, minimum.StatusCode);
        Assert.NotNull(minimumPage);
        Assert.Single(minimumPage.Items);
        Assert.NotNull(minimumPage.NextCursor);

        using var maximum = await _client.GetAsync(
            "/api/v1/trips?limit=200",
            TestContext.Current.CancellationToken);
        var maximumPage = await maximum.Content.ReadFromJsonAsync<PageResponse<TripSummaryResponse>>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, maximum.StatusCode);
        Assert.NotNull(maximumPage);
        Assert.Equal(51, maximumPage.Items.Count);
        Assert.Null(maximumPage.NextCursor);

        foreach (var path in new[]
        {
            "/api/v1/trips?limit=0",
            "/api/v1/trips?limit=201",
            "/api/v1/trips?limit=not-a-number",
            "/api/v1/trips?limit=1%0A",
        })
        {
            using var invalidLimit = await _client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
            using var validation = JsonDocument.Parse(
                await invalidLimit.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Contains(
                "limit",
                validation.RootElement.GetProperty("errors").EnumerateObject()
                    .Select(error => error.Name),
                StringComparer.OrdinalIgnoreCase);
        }

        using var invalidCursor = await _client.GetAsync(
            "/api/v1/trips?after=not-a-cursor",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCursor.StatusCode);
        using var problem = JsonDocument.Parse(
            await invalidCursor.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/invalid-cursor",
            problem.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task anonymous_caller_cannot_list_trips()
    {
        using var anonymousClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));

        using var response = await anonymousClient.GetAsync(
            "/api/v1/trips",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task SeedTripsAsync(int count)
    {
        await using var context = CreateOwnerContext(_ownerId);
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var camperId = NewId();
        context.Campers.Add(new Camper
        {
            Id = camperId,
            OwnerId = _ownerId,
            Name = "Seed camper",
            VehicleKind = VehicleKind.Van,
            CreatedAtUtc = now,
        });
        var ids = new SequentialGuidGenerator(TimeProvider.System);
        context.Trips.AddRange(Enumerable.Range(0, count).Select(_ => new Trip
        {
            Id = ids.NewId(),
            OwnerId = _ownerId,
            CamperId = camperId,
            Name = "Seed trip",
            CreatedAtUtc = now,
        }));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task CreateTripAsync(Guid tripId, string name)
    {
        await CreateTripAsync(_client, tripId, name);
    }

    private static async Task CreateTripAsync(RoamlyClient client, Guid tripId, string name)
    {
        var camperId = NewId();
        using var camper = await client.PostJsonAsync(
            "/api/v1/campers",
            new { id = camperId, name = "Il Grigio", vehicleKind = 1 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, camper.StatusCode);

        using var trip = await client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            new { id = tripId, name, stops = new[] { new { name = "Saint-Malo" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, trip.StatusCode);
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
            CreatedAtUtc = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
        }, Password);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private RoamlyDbContext CreateOwnerContext(Guid ownerId)
    {
        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .Options;
        return new RoamlyDbContext(options, new TestCurrentUser(ownerId));
    }

    private static Guid NewId() => new SequentialGuidGenerator(TimeProvider.System).NewId();

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
        public bool IsAuthenticated => true;
    }

    private sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);

    private sealed record TripSummaryResponse(Guid Id, Guid CamperId, string Name, int Status);
}
