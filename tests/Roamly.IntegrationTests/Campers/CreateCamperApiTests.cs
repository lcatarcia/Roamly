using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Campers;

[Collection(DatabaseCollection.Name)]
public sealed class CreateCamperApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
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

        await CreateUserAsync(_ownerId, "camper-owner@example.test");
        using var login = await _client.LoginAsync(
            "camper-owner@example.test",
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
    public async Task authenticated_owner_can_create_a_camper_with_the_client_id()
    {
        var id = NewCamperId();
        using var response = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "Il Grigio",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/v1/campers/{id:D}", response.Headers.Location?.OriginalString);

        var body = await response.Content.ReadFromJsonAsync<CreateCamperResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(id, body.Id);
        Assert.Equal("Il Grigio", body.Name);
        Assert.Equal(1, body.VehicleKind);

        await using var context = CreateOwnerContext(_ownerId);
        var camper = await context.Campers.SingleAsync(
            item => item.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal(_ownerId, camper.OwnerId);
        Assert.Equal(id, camper.Id);
        Assert.Equal("Il Grigio", camper.Name);
    }

    [Fact]
    public async Task required_fields_are_reported_as_validation_problem_fields()
    {
        using var response = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id = (Guid?)null,
            name = "",
            vehicleKind = (int?)null,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("id", out _), errors.GetRawText());
        Assert.True(errors.TryGetProperty("name", out _), errors.GetRawText());
        Assert.True(errors.TryGetProperty("vehicleKind", out _), errors.GetRawText());
        Assert.False(errors.TryGetProperty("ownerId", out _));
    }

    [Fact]
    public async Task owner_id_in_the_request_cannot_spoof_the_authenticated_owner()
    {
        var id = NewCamperId();
        var spoofedOwnerId = Guid.NewGuid();
        using var response = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "Camper mio",
            vehicleKind = 1,
            ownerId = spoofedOwnerId,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var actualOwnerContext = CreateOwnerContext(_ownerId);
        var camper = await actualOwnerContext.Campers.SingleAsync(
            item => item.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal(_ownerId, camper.OwnerId);

        await using var spoofedOwnerContext = CreateOwnerContext(spoofedOwnerId);
        Assert.Null(await spoofedOwnerContext.Campers.SingleOrDefaultAsync(
            item => item.Id == id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task retry_with_same_id_and_normalized_content_replays_existing_resource()
    {
        var id = NewCamperId();
        using var created = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "  Il Blu  ",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        using var replay = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "Il Blu",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        var body = await replay.Content.ReadFromJsonAsync<CreateCamperResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(id, body.Id);
        Assert.Equal("Il Blu", body.Name);
    }

    [Fact]
    public async Task same_owner_and_id_with_different_content_returns_conflict()
    {
        var id = NewCamperId();
        using var created = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "Il Blu",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        using var conflict = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "Il Rosso",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.StartsWith("application/problem+json", conflict.Content.Headers.ContentType?.MediaType);

        await using var context = CreateOwnerContext(_ownerId);
        var unchanged = await context.Campers.SingleAsync(
            camper => camper.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal("Il Blu", unchanged.Name);
    }

    [Fact]
    public async Task case_only_name_change_with_same_id_returns_conflict()
    {
        var id = NewCamperId();
        using var created = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "Il Blu",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        using var conflict = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "il blu",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        await using var context = CreateOwnerContext(_ownerId);
        var unchanged = await context.Campers.SingleAsync(
            camper => camper.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal("Il Blu", unchanged.Name);
    }

    [Fact]
    public async Task different_vehicle_kind_with_same_id_returns_conflict()
    {
        var id = NewCamperId();
        using var created = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "Il Blu",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        using var conflict = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "Il Blu",
            vehicleKind = 2,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        await using var context = CreateOwnerContext(_ownerId);
        var unchanged = await context.Campers.SingleAsync(
            camper => camper.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal((byte)1, (byte)unchanged.VehicleKind);
    }

    [Fact]
    public async Task another_owner_can_use_the_same_id_without_reading_the_first_owners_row()
    {
        var id = NewCamperId();
        using var firstCreate = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "Il Blu",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);

        var secondOwnerId = Guid.NewGuid();
        const string secondEmail = "another-owner@example.test";
        await CreateUserAsync(secondOwnerId, secondEmail);
        using var secondClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        using var login = await secondClient.LoginAsync(
            secondEmail,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var secondCreate = await secondClient.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "Il Blu",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondCreate.StatusCode);

        await using var firstOwnerContext = CreateOwnerContext(_ownerId);
        await using var secondOwnerContext = CreateOwnerContext(secondOwnerId);
        Assert.Equal(_ownerId, (await firstOwnerContext.Campers.SingleAsync(
            camper => camper.Id == id,
            TestContext.Current.CancellationToken)).OwnerId);
        Assert.Equal(secondOwnerId, (await secondOwnerContext.Campers.SingleAsync(
            camper => camper.Id == id,
            TestContext.Current.CancellationToken)).OwnerId);
    }

    [Fact]
    public async Task anonymous_user_cannot_create_a_camper()
    {
        using var anonymousClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        using var response = await anonymousClient.PostJsonAsync("/api/v1/campers", new
        {
            id = Guid.NewGuid(),
            name = "Non autorizzato",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(
            (int)HttpStatusCode.Unauthorized,
            problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            "https://roamly.dev/problems/unauthorized",
            problem.RootElement.GetProperty("type").GetString());
        Assert.False(problem.RootElement.TryGetProperty("detail", out _));
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

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
        public bool IsAuthenticated => true;
    }

    private sealed record CreateCamperResponse(Guid Id, string Name, int VehicleKind);

    private static Guid NewCamperId() => new SequentialGuidGenerator(TimeProvider.System).NewId();
}
