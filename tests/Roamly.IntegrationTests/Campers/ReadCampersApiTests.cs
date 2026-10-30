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

namespace Roamly.IntegrationTests.Campers;

[Collection(DatabaseCollection.Name)]
public sealed class ReadCampersApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
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

        await CreateUserAsync(_ownerId, "camper-reader@example.test");
        using var login = await _client.LoginAsync(
            "camper-reader@example.test",
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
    public async Task camper_collection_uses_opaque_keyset_pages_and_returns_null_at_the_end()
    {
        await SeedCampersAsync(3);

        using var first = await _client.GetAsync(
            "/api/v1/campers?limit=2",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstPage = await first.Content.ReadFromJsonAsync<PageResponse<CamperResponse>>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.NextCursor));

        using var second = await _client.GetAsync(
            $"/api/v1/campers?limit=2&after={Uri.EscapeDataString(firstPage.NextCursor!)}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondPage = await second.Content.ReadFromJsonAsync<PageResponse<CamperResponse>>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(secondPage);
        Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextCursor);
        Assert.Equal(
            3,
            firstPage.Items.Select(camper => camper.Id)
                .Concat(secondPage.Items.Select(camper => camper.Id))
                .Distinct()
                .Count());
    }

    [Fact]
    public async Task camper_collection_defaults_to_50_and_rejects_invalid_limit_and_cursor()
    {
        await SeedCampersAsync(1);

        using var defaults = await _client.GetAsync(
            "/api/v1/campers",
            TestContext.Current.CancellationToken);
        var page = await defaults.Content.ReadFromJsonAsync<PageResponse<CamperResponse>>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, defaults.StatusCode);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Null(page.NextCursor);

        foreach (var path in new[]
                 {
                     "/api/v1/campers?limit=0",
                     "/api/v1/campers?limit=201",
                     "/api/v1/campers?after=not-a-cursor",
                 })
        {
            using var invalid = await _client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            if (path.Contains("after=", StringComparison.Ordinal))
            {
                using var problem = await JsonDocument.ParseAsync(
                    await invalid.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
                    cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(
                    "https://roamly.dev/problems/invalid-cursor",
                    problem.RootElement.GetProperty("type").GetString());
            }
        }

        using var anonymousClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        using var anonymous = await anonymousClient.GetAsync(
            "/api/v1/campers",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task camper_collection_accepts_the_minimum_and_maximum_page_sizes()
    {
        await SeedCampersAsync(3);

        using var minimum = await _client.GetAsync(
            "/api/v1/campers?limit=1",
            TestContext.Current.CancellationToken);
        var firstPage = await minimum.Content.ReadFromJsonAsync<PageResponse<CamperResponse>>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, minimum.StatusCode);
        Assert.NotNull(firstPage);
        Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);

        using var maximum = await _client.GetAsync(
            "/api/v1/campers?limit=200",
            TestContext.Current.CancellationToken);
        var allCampers = await maximum.Content.ReadFromJsonAsync<PageResponse<CamperResponse>>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, maximum.StatusCode);
        Assert.NotNull(allCampers);
        Assert.Equal(3, allCampers.Items.Count);
        Assert.Null(allCampers.NextCursor);
    }

    [Fact]
    public async Task camper_collection_excludes_other_owners_rows()
    {
        await SeedCampersAsync(1);
        var otherOwnerId = Guid.NewGuid();
        await CreateUserAsync(otherOwnerId, "other-camper-owner@example.test");
        await using (var context = CreateOwnerContext(otherOwnerId))
        {
            context.Campers.Add(new Camper
            {
                Id = NewCamperId(),
                OwnerId = otherOwnerId,
                Name = "Non mio",
                VehicleKind = VehicleKind.Van,
                CreatedAtUtc = TimeProvider.System.GetUtcNow().UtcDateTime,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var response = await _client.GetAsync(
            "/api/v1/campers",
            TestContext.Current.CancellationToken);
        var page = await response.Content.ReadFromJsonAsync<PageResponse<CamperResponse>>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal("Camper 0", page.Items[0].Name);
    }

    private async Task SeedCampersAsync(int count)
    {
        await using var context = CreateOwnerContext(_ownerId);
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var ids = new SequentialGuidGenerator(TimeProvider.System);
        context.Campers.AddRange(Enumerable.Range(0, count).Select(index => new Camper
        {
            Id = ids.NewId(),
            OwnerId = _ownerId,
            Name = $"Camper {index}",
            VehicleKind = VehicleKind.Van,
            CreatedAtUtc = now,
        }));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
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

    private sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
    private sealed record CamperResponse(Guid Id, string Name, int VehicleKind);
    private static Guid NewCamperId() => new SequentialGuidGenerator(TimeProvider.System).NewId();
}
