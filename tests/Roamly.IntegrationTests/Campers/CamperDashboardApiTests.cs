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
public sealed class CamperDashboardApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
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

        await CreateUserAsync(_ownerId, "dashboard-owner@example.test");
        using var login = await _client.LoginAsync(
            "dashboard-owner@example.test",
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
    public async Task dashboard_returns_camper_and_real_empty_states_without_inventing_data()
    {
        var id = await CreateCamperAsync("La Libellula", VehicleKind.Van);

        using var response = await _client.GetAsync(
            $"/api/v1/campers/{id:D}/dashboard",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(dashboard);
        Assert.Equal(id, dashboard.Camper.Id);
        Assert.Equal("La Libellula", dashboard.Camper.Name);
        Assert.Equal((int)VehicleKind.Van, dashboard.Camper.VehicleKind);
        Assert.Null(dashboard.Odometer);
        Assert.Empty(dashboard.Maintenance);
    }

    [Fact]
    public async Task dashboard_reports_maintenance_states_independently_for_each_axis()
    {
        var id = await CreateCamperAsync("Il Girasole", VehicleKind.Motorhome);
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await SeedOdometerAsync(id, 10_000, today);
        await SeedMaintenanceAsync(id,
            ("Tempo tra 30 giorni", today.AddMonths(-11), 12, 1_000, today.AddDays(30), 9_900),
            ("Km tra 1.000", today.AddMonths(-13), 12, 1_000, today.AddDays(-1), 11_000),
            ("Mai eseguito", null, 12, 1_000, null, null));

        using var response = await _client.GetAsync(
            $"/api/v1/campers/{id:D}/dashboard",
            TestContext.Current.CancellationToken);
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dashboard);
        Assert.False(dashboard.Odometer!.IsStale);
        var timeDriven = Assert.Single(dashboard.Maintenance, item => item.Name == "Tempo tra 30 giorni");
        Assert.Equal("DueSoon", timeDriven.TimeAxis.Status);
        Assert.Equal("Overdue", timeDriven.OdometerAxis.Status);
        var kilometerDriven = Assert.Single(dashboard.Maintenance, item => item.Name == "Km tra 1.000");
        Assert.Equal("Overdue", kilometerDriven.TimeAxis.Status);
        Assert.Equal("DueSoon", kilometerDriven.OdometerAxis.Status);
        var neverServiced = Assert.Single(dashboard.Maintenance, item => item.Name == "Mai eseguito");
        Assert.False(neverServiced.HasServiceHistory);
        Assert.Equal("NotScheduled", neverServiced.TimeAxis.Status);
        Assert.Equal("NotScheduled", neverServiced.OdometerAxis.Status);
    }

    [Fact]
    public async Task stale_or_missing_odometer_makes_kilometer_axis_unknown_without_hiding_dated_reading()
    {
        var staleCamperId = await CreateCamperAsync("Il Lento", VehicleKind.Van);
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await SeedOdometerAsync(staleCamperId, 10_000, today.AddDays(-91));
        await SeedMaintenanceAsync(staleCamperId,
            ("Revisione", today.AddMonths(-11), 12, 5_000, today.AddDays(20), 12_000));

        var noReadingCamperId = await CreateCamperAsync("La Nuvola", VehicleKind.Van);
        await SeedMaintenanceAsync(noReadingCamperId,
            ("Cambio olio", today.AddMonths(-11), 12, 5_000, today.AddDays(20), 12_000));

        var staleDashboard = await GetDashboardAsync(staleCamperId);
        var staleItem = Assert.Single(staleDashboard.Maintenance);
        Assert.True(staleDashboard.Odometer!.IsStale);
        Assert.Equal(10_000, staleDashboard.Odometer.ReadingKm);
        Assert.Equal(today.AddDays(-91), staleDashboard.Odometer.TakenOnUtc);
        Assert.Equal("DueSoon", staleItem.TimeAxis.Status);
        Assert.Equal("Unknown", staleItem.OdometerAxis.Status);
        Assert.Null(staleItem.OdometerAxis.KmRemaining);

        var noReadingDashboard = await GetDashboardAsync(noReadingCamperId);
        var noReadingItem = Assert.Single(noReadingDashboard.Maintenance);
        Assert.Null(noReadingDashboard.Odometer);
        Assert.Equal("Unknown", noReadingItem.OdometerAxis.Status);
        Assert.Equal("DueSoon", noReadingItem.TimeAxis.Status);

        var boundaryCamperId = await CreateCamperAsync("La Sosta", VehicleKind.Van);
        await SeedOdometerAsync(boundaryCamperId, 10_000, today.AddDays(-90));
        await SeedMaintenanceAsync(boundaryCamperId,
            ("Tagliando", today.AddMonths(-11), 12, 5_000, today.AddDays(40), 11_000));
        var boundaryDashboard = await GetDashboardAsync(boundaryCamperId);
        var boundaryItem = Assert.Single(boundaryDashboard.Maintenance);
        Assert.False(boundaryDashboard.Odometer!.IsStale);
        Assert.Equal("DueSoon", boundaryItem.OdometerAxis.Status);
    }

    [Fact]
    public async Task unknown_and_non_owned_camper_ids_have_the_same_not_found_contract()
    {
        var otherOwnerId = Guid.NewGuid();
        const string otherEmail = "different-dashboard-owner@example.test";
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
        var nonOwnedId = await CreateCamperAsync(otherClient, "Altro camper", VehicleKind.Van);

        using var nonOwned = await _client.GetAsync(
            $"/api/v1/campers/{nonOwnedId:D}/dashboard",
            TestContext.Current.CancellationToken);
        using var missing = await _client.GetAsync(
            $"/api/v1/campers/{Guid.NewGuid():D}/dashboard",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, nonOwned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var nonOwnedProblem = JsonDocument.Parse(
            await nonOwned.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var missingProblem = JsonDocument.Parse(
            await missing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            nonOwnedProblem.RootElement.GetProperty("type").GetString(),
            missingProblem.RootElement.GetProperty("type").GetString());
        Assert.Equal(
            nonOwnedProblem.RootElement.GetProperty("title").GetString(),
            missingProblem.RootElement.GetProperty("title").GetString());
        Assert.False(nonOwnedProblem.RootElement.TryGetProperty("detail", out _));
        Assert.False(missingProblem.RootElement.TryGetProperty("detail", out _));
        Assert.False(nonOwnedProblem.RootElement.TryGetProperty("instance", out _));
        Assert.False(missingProblem.RootElement.TryGetProperty("instance", out _));
    }

    private async Task<Guid> CreateCamperAsync(string name, VehicleKind vehicleKind)
    {
        return await CreateCamperAsync(_client, name, vehicleKind);
    }

    private static async Task<Guid> CreateCamperAsync(
        RoamlyClient client,
        string name,
        VehicleKind vehicleKind)
    {
        var id = new SequentialGuidGenerator(TimeProvider.System).NewId();
        using var response = await client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name,
            vehicleKind = (int)vehicleKind,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return id;
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

    private async Task SeedOdometerAsync(Guid camperId, int readingKm, DateOnly takenOn)
    {
        await using var context = CreateOwnerContext();
        context.OdometerReadings.Add(new OdometerReading
        {
            Id = new SequentialGuidGenerator(TimeProvider.System).NewId(),
            OwnerId = _ownerId,
            CamperId = camperId,
            ReadingKm = readingKm,
            TakenOnUtc = takenOn,
            Source = OdometerSource.Manual,
            CreatedAtUtc = TimeProvider.System.GetUtcNow().UtcDateTime,
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedMaintenanceAsync(
        Guid camperId,
        params (string Name, DateOnly? LastServiceOn, short? IntervalMonths, int? IntervalKm,
            DateOnly? DueOn, int? DueAtKm)[] items)
    {
        await using var context = CreateOwnerContext();
        var ids = new SequentialGuidGenerator(TimeProvider.System);
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        context.MaintenanceItems.AddRange(items.Select(item => new MaintenanceItem
        {
            Id = ids.NewId(),
            OwnerId = _ownerId,
            CamperId = camperId,
            Name = item.Name,
            Category = MaintenanceCategory.Engine,
            IntervalMonths = item.IntervalMonths,
            IntervalKm = item.IntervalKm,
            LastServiceOnUtc = item.LastServiceOn,
            NextDueOnUtc = item.DueOn,
            NextDueOdometerKm = item.DueAtKm,
            Origin = MaintenanceOrigin.User,
            IsActive = true,
            CreatedAtUtc = now,
        }));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<DashboardResponse> GetDashboardAsync(Guid camperId)
    {
        using var response = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/dashboard",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DashboardResponse>(
            TestContext.Current.CancellationToken))!;
    }

    private RoamlyDbContext CreateOwnerContext()
    {
        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .Options;
        return new RoamlyDbContext(options, new TestCurrentUser(_ownerId));
    }

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
        public bool IsAuthenticated => true;
    }

    private sealed record CamperResponse(Guid Id, string Name, int VehicleKind);
    private sealed record DashboardResponse(
        CamperResponse Camper,
        OdometerResponse? Odometer,
        IReadOnlyList<MaintenanceResponse> Maintenance);
    private sealed record OdometerResponse(int ReadingKm, DateOnly TakenOnUtc, bool IsStale);
    private sealed record MaintenanceResponse(
        string Name,
        bool HasServiceHistory,
        DateOnly? LastServiceOnUtc,
        AxisResponse TimeAxis,
        AxisOdometerResponse OdometerAxis);
    private sealed record AxisResponse(string Status, DateOnly? DueOnUtc);
    private sealed record AxisOdometerResponse(string Status, int? DueAtKm, int? KmRemaining);
}
