using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Common;
using Roamly.Domain.Abstractions;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Campers;

[Collection(DatabaseCollection.Name)]
public sealed class MaintenanceApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Password = "Test!Password123";
    private readonly Guid _ownerId = NewId();
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
        await CreateUserAsync(_ownerId, "maintenance-owner@example.test");
        using var login = await _client.LoginAsync(
            "maintenance-owner@example.test",
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
    public async Task owner_can_create_a_recurring_maintenance_without_service_history()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        using var response = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/maintenance",
            new
            {
                id = itemId,
                name = "  Cambio olio  ",
                category = (int)MaintenanceCategory.Engine,
                intervalKm = 15_000,
                intervalMonths = 12,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            $"/api/v1/campers/{camperId:D}/maintenance/{itemId:D}",
            response.Headers.Location?.OriginalString);
        var created = await response.Content.ReadFromJsonAsync<MaintenanceItemResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal(itemId, created.Id);
        Assert.Equal("Cambio olio", created.Name);
        Assert.Null(created.LastServiceOnUtc);
        Assert.Null(created.NextDueOnUtc);

        using var dashboardResponse = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/dashboard",
            TestContext.Current.CancellationToken);
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<DashboardResponse>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        var maintenance = Assert.Single(Assert.IsType<DashboardResponse>(dashboard).Maintenance);
        Assert.Equal(itemId, maintenance.Id);
        Assert.False(maintenance.HasServiceHistory);
        Assert.Equal("NotScheduled", maintenance.TimeAxis.Status);
        Assert.Equal("NotScheduled", maintenance.OdometerAxis.Status);
    }

    [Fact]
    public async Task maintenance_validation_uses_json_field_names_and_requires_a_recurrence()
    {
        var camperId = await CreateCamperAsync();
        using var response = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/maintenance",
            new
            {
                id = NewId(),
                name = "Cambio olio",
                category = (int)MaintenanceCategory.Engine,
                intervalKm = (int?)null,
                intervalMonths = (short?)null,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("intervalKm", out _), errors.GetRawText());
        Assert.True(errors.TryGetProperty("intervalMonths", out _), errors.GetRawText());
        Assert.False(errors.TryGetProperty("ownerId", out _));
    }

    [Fact]
    public async Task maintenance_collection_uses_id_ordered_keyset_pages()
    {
        var camperId = await CreateCamperAsync();
        var firstId = NewId();
        var secondId = NewId();
        await CreateMaintenanceAsync(camperId, firstId, "Cambio olio");
        await CreateMaintenanceAsync(camperId, secondId, "Controllo freni");

        using var firstResponse = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/maintenance?limit=1",
            TestContext.Current.CancellationToken);
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<PageResponse<MaintenanceListItem>>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstPage);
        Assert.Equal(firstId, Assert.Single(firstPage.Items).Id);
        Assert.NotNull(firstPage.NextCursor);

        using var secondResponse = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/maintenance?limit=1&after={firstPage.NextCursor}",
            TestContext.Current.CancellationToken);
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<PageResponse<MaintenanceListItem>>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondPage);
        Assert.Equal(secondId, Assert.Single(secondPage.Items).Id);
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task malformed_maintenance_cursor_returns_the_shared_invalid_cursor_problem()
    {
        var camperId = await CreateCamperAsync();
        using var response = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/maintenance?after=not-a-cursor",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/invalid-cursor",
            problem.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task maintenance_log_history_is_id_ordered_with_keyset_pages_and_hides_unknown_items()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        var firstId = NewId();
        var secondId = NewId();
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio");
        using var firstLog = await PostMaintenanceLogAsync(camperId, itemId, new
        {
            id = firstId,
            performedOnUtc = new DateOnly(2026, 1, 1),
        });
        using var secondLog = await PostMaintenanceLogAsync(camperId, itemId, new
        {
            id = secondId,
            performedOnUtc = new DateOnly(2026, 2, 1),
        });
        Assert.Equal(HttpStatusCode.Created, firstLog.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondLog.StatusCode);

        using var firstResponse = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/maintenance/{itemId:D}/logs?limit=1",
            TestContext.Current.CancellationToken);
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<PageResponse<MaintenanceLogResponse>>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstPage);
        Assert.Equal(firstId, Assert.Single(firstPage.Items).Id);
        Assert.NotNull(firstPage.NextCursor);

        using var secondResponse = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/maintenance/{itemId:D}/logs?limit=1&after={firstPage.NextCursor}",
            TestContext.Current.CancellationToken);
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<PageResponse<MaintenanceLogResponse>>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondPage);
        Assert.Equal(secondId, Assert.Single(secondPage.Items).Id);
        Assert.Null(secondPage.NextCursor);

        using var invalidCursor = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/maintenance/{itemId:D}/logs?after=not-a-cursor",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCursor.StatusCode);
        using var invalidCursorProblem = JsonDocument.Parse(
            await invalidCursor.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/invalid-cursor",
            invalidCursorProblem.RootElement.GetProperty("type").GetString());

        using var unknownItem = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/maintenance/{NewId():D}/logs",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, unknownItem.StatusCode);
    }

    [Fact]
    public async Task retry_with_same_maintenance_id_and_content_replays_the_existing_item()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        using var created = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/maintenance",
            new
            {
                id = itemId,
                name = "  Cambio olio  ",
                category = (int)MaintenanceCategory.Engine,
                intervalKm = 15_000,
            },
            TestContext.Current.CancellationToken);
        using var replay = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/maintenance",
            new
            {
                id = itemId,
                name = "Cambio olio",
                category = (int)MaintenanceCategory.Engine,
                intervalKm = 15_000,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var body = await replay.Content.ReadFromJsonAsync<MaintenanceItemResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(itemId, body.Id);
        Assert.Equal("Cambio olio", body.Name);
    }

    [Fact]
    public async Task maintenance_id_reused_with_different_content_returns_conflict()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio");
        using var conflict = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/maintenance",
            new
            {
                id = itemId,
                name = "Cambio olio",
                category = (int)MaintenanceCategory.Engine,
                intervalKm = 20_000,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var problem = JsonDocument.Parse(
            await conflict.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/maintenance-id-content-conflict",
            problem.RootElement.GetProperty("type").GetString());

        using var list = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/maintenance",
            TestContext.Current.CancellationToken);
        var page = await list.Content.ReadFromJsonAsync<PageResponse<MaintenanceListItem>>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var unchanged = Assert.Single(Assert.IsType<PageResponse<MaintenanceListItem>>(page).Items);
        Assert.Null(unchanged.IntervalKm);
        Assert.Equal((short)12, unchanged.IntervalMonths);
    }

    [Fact]
    public async Task maintenance_id_replayed_under_another_camper_returns_conflict()
    {
        var originalCamperId = await CreateCamperAsync();
        var otherCamperId = await CreateCamperAsync();
        var itemId = NewId();
        await CreateMaintenanceAsync(originalCamperId, itemId, "Cambio olio");

        using var response = await _client.PostJsonAsync(
            $"/api/v1/campers/{otherCamperId:D}/maintenance",
            ValidMaintenanceBody(itemId),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/maintenance-id-content-conflict",
            problem.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task unknown_and_non_owned_camper_ids_have_the_same_maintenance_not_found_contract()
    {
        var otherOwnerId = NewId();
        const string otherEmail = "different-maintenance-owner@example.test";
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
        var nonOwnedCamperId = await CreateCamperAsync(otherClient);
        var unknownCamperId = NewId();

        using var nonOwned = await _client.PostJsonAsync(
            $"/api/v1/campers/{nonOwnedCamperId:D}/maintenance",
            ValidMaintenanceBody(NewId()),
            TestContext.Current.CancellationToken);
        using var missing = await _client.PostJsonAsync(
            $"/api/v1/campers/{unknownCamperId:D}/maintenance",
            ValidMaintenanceBody(NewId()),
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
    }

    [Fact]
    public async Task non_owned_maintenance_history_and_log_writes_are_uniform_404()
    {
        var otherOwnerId = NewId();
        const string otherEmail = "maintenance-history-owner@example.test";
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

        var foreignCamperId = await CreateCamperAsync(otherClient);
        var foreignItemId = NewId();
        using var foreignCreate = await otherClient.PostJsonAsync(
            $"/api/v1/campers/{foreignCamperId:D}/maintenance",
            ValidMaintenanceBody(foreignItemId),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, foreignCreate.StatusCode);

        var unknownCamperId = NewId();
        var unknownItemId = NewId();
        using var foreignHistory = await _client.GetAsync(
            $"/api/v1/campers/{foreignCamperId:D}/maintenance/{foreignItemId:D}/logs",
            TestContext.Current.CancellationToken);
        using var unknownHistory = await _client.GetAsync(
            $"/api/v1/campers/{unknownCamperId:D}/maintenance/{unknownItemId:D}/logs",
            TestContext.Current.CancellationToken);
        using var foreignLog = await PostMaintenanceLogAsync(
            foreignCamperId,
            foreignItemId,
            new { id = NewId(), performedOnUtc = new DateOnly(2026, 1, 1) });
        using var unknownLog = await PostMaintenanceLogAsync(
            unknownCamperId,
            unknownItemId,
            new { id = NewId(), performedOnUtc = new DateOnly(2026, 1, 1) });

        await AssertUniformNotFoundAsync(foreignHistory, unknownHistory);
        await AssertUniformNotFoundAsync(foreignLog, unknownLog);
    }

    [Fact]
    public async Task recording_service_recalculates_both_due_axes_and_adds_a_service_odometer_reading()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        var logId = NewId();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio", 10_000, 12);

        using var response = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/maintenance/{itemId:D}/logs",
            new
            {
                id = logId,
                performedOnUtc = today,
                odometerKm = 50_000,
                cost = new { amount = 123.45m, currency = "EUR" },
                workshop = "Officina",
                notes = "Filtro sostituito",
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            $"/api/v1/campers/{camperId:D}/maintenance/{itemId:D}/logs/{logId:D}",
            response.Headers.Location?.OriginalString);
        var log = await response.Content.ReadFromJsonAsync<MaintenanceLogResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(log);
        Assert.Equal(today, log.PerformedOnUtc);
        Assert.Equal(50_000, log.OdometerKm);
        Assert.Equal(123.45m, log.Cost!.Amount);
        Assert.Equal("EUR", log.Cost.Currency);

        using var dashboardResponse = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/dashboard",
            TestContext.Current.CancellationToken);
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<DashboardResponse>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        var maintenance = Assert.Single(Assert.IsType<DashboardResponse>(dashboard).Maintenance);
        Assert.Equal(itemId, maintenance.Id);
        Assert.True(maintenance.HasServiceHistory);
        Assert.Equal(today, maintenance.LastServiceOnUtc);
        Assert.Equal("Ok", maintenance.TimeAxis.Status);
        Assert.Equal("Ok", maintenance.OdometerAxis.Status);
        Assert.Equal(60_000, maintenance.OdometerAxis.DueAtKm);
        Assert.Equal(50_000, dashboard!.Odometer!.ReadingKm);

        await using var context = CreateOwnerContext();
        var reading = await context.OdometerReadings.SingleAsync(
            entry => entry.CamperId == camperId && entry.TakenOnUtc == today,
            TestContext.Current.CancellationToken);
        Assert.Equal(OdometerSource.Service, reading.Source);
    }

    [Fact]
    public async Task retry_with_same_log_id_and_content_replays_without_adding_a_second_service()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        var logId = NewId();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio", 10_000, 12);
        var body = new
        {
            id = logId,
            performedOnUtc = today,
            odometerKm = 50_000,
            cost = new { amount = 123.45m, currency = "EUR" },
        };

        using var created = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/maintenance/{itemId:D}/logs",
            body,
            TestContext.Current.CancellationToken);
        using var replay = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/maintenance/{itemId:D}/logs",
            body,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var log = await replay.Content.ReadFromJsonAsync<MaintenanceLogResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(log);
        Assert.Equal(logId, log.Id);
        await using var context = CreateOwnerContext();
        Assert.Equal(1, await context.MaintenanceLogs.CountAsync(
            item => item.MaintenanceItemId == itemId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task same_log_id_with_different_content_returns_conflict_without_moving_projection()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        var logId = NewId();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio", 10_000, 12);
        using var created = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/maintenance/{itemId:D}/logs",
            new { id = logId, performedOnUtc = today, odometerKm = 50_000 },
            TestContext.Current.CancellationToken);
        using var conflict = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/maintenance/{itemId:D}/logs",
            new { id = logId, performedOnUtc = today, odometerKm = 51_000 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var problem = JsonDocument.Parse(
            await conflict.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/maintenance-log-id-content-conflict",
            problem.RootElement.GetProperty("type").GetString());
        var dashboard = await GetDashboardAsync(camperId);
        var maintenance = Assert.Single(dashboard.Maintenance);
        Assert.Equal(today, maintenance.LastServiceOnUtc);
        Assert.Equal(60_000, maintenance.OdometerAxis.DueAtKm);
    }

    [Fact]
    public async Task retroactive_log_does_not_move_projection_back_and_later_same_date_log_wins()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio", 10_000, 12);
        using var newest = await PostMaintenanceLogAsync(
            camperId,
            itemId,
            new { id = NewId(), performedOnUtc = today, odometerKm = 50_000 });
        using var retroactive = await PostMaintenanceLogAsync(
            camperId,
            itemId,
            new { id = NewId(), performedOnUtc = today.AddDays(-60), odometerKm = 40_000 });

        Assert.Equal(HttpStatusCode.Created, newest.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retroactive.StatusCode);
        var afterRetroactive = await GetDashboardAsync(camperId);
        var projected = Assert.Single(afterRetroactive.Maintenance);
        Assert.Equal(today, projected.LastServiceOnUtc);
        Assert.Equal(today.AddMonths(12), projected.TimeAxis.DueOnUtc);
        Assert.Equal(60_000, projected.OdometerAxis.DueAtKm);

        using var laterWithoutKm = await PostMaintenanceLogAsync(
            camperId,
            itemId,
            new { id = NewId(), performedOnUtc = today, odometerKm = (int?)null });
        Assert.Equal(HttpStatusCode.Created, laterWithoutKm.StatusCode);
        var afterTie = await GetDashboardAsync(camperId);
        var latest = Assert.Single(afterTie.Maintenance);
        Assert.Equal(today, latest.LastServiceOnUtc);
        Assert.Equal(today.AddMonths(12), latest.TimeAxis.DueOnUtc);
        Assert.Equal("Unknown", latest.OdometerAxis.Status);
        Assert.Null(latest.OdometerAxis.DueAtKm);

        using var historyResponse = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/maintenance/{itemId:D}/logs",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<PageResponse<MaintenanceLogHistoryItem>>(
            TestContext.Current.CancellationToken);
        var historyPage = Assert.IsType<PageResponse<MaintenanceLogHistoryItem>>(history);
        Assert.Equal(3, historyPage.Items.Count);
        Assert.Contains(
            historyPage.Items,
            log => log.PerformedOnUtc == today.AddDays(-60));

        await using var context = CreateOwnerContext();
        var item = await context.MaintenanceItems.SingleAsync(
            maintenance => maintenance.Id == itemId,
            TestContext.Current.CancellationToken);
        Assert.Null(item.LastServiceOdometerKm);
        Assert.Null(item.NextDueOdometerKm);
    }

    [Fact]
    public async Task concurrent_distinct_logs_are_both_recorded_and_projection_uses_latest_date()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio", null, 12);

        var responses = await Task.WhenAll(
            PostMaintenanceLogAsync(
                camperId,
                itemId,
                new { id = NewId(), performedOnUtc = today, odometerKm = (int?)null }),
            PostMaintenanceLogAsync(
                camperId,
                itemId,
                new { id = NewId(), performedOnUtc = today.AddDays(-1), odometerKm = (int?)null }));

        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
            await using var context = CreateOwnerContext();
            Assert.Equal(2, await context.MaintenanceLogs.CountAsync(
                log => log.MaintenanceItemId == itemId,
                TestContext.Current.CancellationToken));
            var item = await context.MaintenanceItems.SingleAsync(
                entry => entry.Id == itemId,
                TestContext.Current.CancellationToken);
            Assert.Equal(today, item.LastServiceOnUtc);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task cancelling_a_log_waiting_for_the_same_maintenance_lock_does_not_write()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio");

        var resource = $"RoamlyMaintenance:{_ownerId:N}:{itemId:N}";
        await using var lockContext = CreateOwnerContext();
        await using var transaction = await lockContext.Database
            .BeginTransactionAsync(TestContext.Current.CancellationToken);
        var lockResults = await lockContext.Database.SqlQuery<int>($"""
            DECLARE @lockResult int;
            EXEC @lockResult = sys.sp_getapplock
                @Resource = {resource},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 5000;
            SELECT @lockResult AS [Value];
            """)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.True(Assert.Single(lockResults) >= 0);

        var resourceDescription = await lockContext.Database.SqlQuery<string>($"""
            SELECT [resource_description] AS [Value]
            FROM sys.dm_tran_locks
            WHERE [request_session_id] = @@SPID
                AND [resource_type] = N'APPLICATION'
                AND [request_status] = N'GRANT'
            """)
            .SingleAsync(TestContext.Current.CancellationToken);

        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        Task<HttpResponseMessage>? pendingRequest = null;
        try
        {
            var request = PostMaintenanceLogAsync(
                camperId,
                itemId,
                new { id = NewId(), performedOnUtc = new DateOnly(2026, 10, 10) },
                requestCancellation.Token);
            pendingRequest = request;
            await WaitForMaintenanceLockWaitAsync(
                resourceDescription,
                expectedWaiting: true,
                TestContext.Current.CancellationToken);

            requestCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            await WaitForMaintenanceLockWaitAsync(
                resourceDescription,
                expectedWaiting: false,
                TestContext.Current.CancellationToken);

            Assert.Equal(0, await lockContext.MaintenanceLogs.CountAsync(
                log => log.MaintenanceItemId == itemId,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            requestCancellation.Cancel();
            await transaction.DisposeAsync();
            if (pendingRequest is not null)
            {
                try
                {
                    using var response = await pendingRequest.WaitAsync(
                        TimeSpan.FromSeconds(10),
                        TestContext.Current.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }

    [Fact]
    public async Task log_is_rejected_atomically_when_time_due_date_is_out_of_range()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio", null, 12);

        using var response = await PostMaintenanceLogAsync(
            camperId,
            itemId,
            new { id = NewId(), performedOnUtc = DateOnly.MaxValue, odometerKm = 50_000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/maintenance-log-projection-out-of-range",
            problem.RootElement.GetProperty("type").GetString());
        await using var context = CreateOwnerContext();
        Assert.Equal(0, await context.MaintenanceLogs.CountAsync(
            log => log.MaintenanceItemId == itemId,
            TestContext.Current.CancellationToken));
        Assert.Empty(await context.OdometerReadings
            .Where(reading => reading.CamperId == camperId)
            .ToListAsync(TestContext.Current.CancellationToken));
        var item = await context.MaintenanceItems.SingleAsync(
            entry => entry.Id == itemId,
            TestContext.Current.CancellationToken);
        Assert.Null(item.LastServiceOnUtc);
    }

    [Fact]
    public async Task log_is_rejected_atomically_when_odometer_due_km_is_out_of_range()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio", 10_000, null);

        using var response = await PostMaintenanceLogAsync(
            camperId,
            itemId,
            new
            {
                id = NewId(),
                performedOnUtc = today,
                odometerKm = int.MaxValue - 1,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/maintenance-log-projection-out-of-range",
            problem.RootElement.GetProperty("type").GetString());
        await using var context = CreateOwnerContext();
        Assert.Equal(0, await context.MaintenanceLogs.CountAsync(
            log => log.MaintenanceItemId == itemId,
            TestContext.Current.CancellationToken));
        Assert.Empty(await context.OdometerReadings
            .Where(reading => reading.CamperId == camperId)
            .ToListAsync(TestContext.Current.CancellationToken));
        var item = await context.MaintenanceItems.SingleAsync(
            entry => entry.Id == itemId,
            TestContext.Current.CancellationToken);
        Assert.Null(item.LastServiceOnUtc);
        Assert.Null(item.NextDueOdometerKm);
    }

    [Fact]
    public async Task same_day_equal_manual_reading_is_reused_without_changing_its_source()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio", 10_000, 12);
        await SeedManualOdometerAsync(camperId, today, 50_000);

        using var response = await PostMaintenanceLogAsync(
            camperId,
            itemId,
            new { id = NewId(), performedOnUtc = today, odometerKm = 50_000 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var context = CreateOwnerContext();
        var readings = await context.OdometerReadings
            .Where(reading => reading.CamperId == camperId)
            .ToListAsync(TestContext.Current.CancellationToken);
        var reading = Assert.Single(readings);
        Assert.Equal(OdometerSource.Manual, reading.Source);
        Assert.Equal(50_000, reading.ReadingKm);
        Assert.Equal(1, await context.MaintenanceLogs.CountAsync(
            log => log.MaintenanceItemId == itemId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task differing_same_day_odometer_rejects_the_whole_log_write()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio", 10_000, 12);
        await SeedManualOdometerAsync(camperId, today, 49_999);

        using var response = await PostMaintenanceLogAsync(
            camperId,
            itemId,
            new { id = NewId(), performedOnUtc = today, odometerKm = 50_000 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/odometer-reading-conflict",
            problem.RootElement.GetProperty("type").GetString());

        await using var context = CreateOwnerContext();
        var item = await context.MaintenanceItems.SingleAsync(
            maintenance => maintenance.Id == itemId,
            TestContext.Current.CancellationToken);
        var reading = await context.OdometerReadings.SingleAsync(
            entry => entry.CamperId == camperId,
            TestContext.Current.CancellationToken);
        Assert.Equal(0, await context.MaintenanceLogs.CountAsync(
            log => log.MaintenanceItemId == itemId,
            TestContext.Current.CancellationToken));
        Assert.Null(item.LastServiceOnUtc);
        Assert.Null(item.LastServiceOdometerKm);
        Assert.Null(item.NextDueOnUtc);
        Assert.Null(item.NextDueOdometerKm);
        Assert.Equal(49_999, reading.ReadingKm);
        Assert.Equal(OdometerSource.Manual, reading.Source);
    }

    [Fact]
    public async Task log_cost_validation_reports_nested_json_keys_and_model_ranges()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio");

        using var response = await PostMaintenanceLogAsync(
            camperId,
            itemId,
            new
            {
                id = NewId(),
                performedOnUtc = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime),
                cost = new { amount = 1_000_000_000_000_000m, currency = "eur" },
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("cost.amount", out _), errors.GetRawText());
        Assert.True(errors.TryGetProperty("cost.currency", out _), errors.GetRawText());
    }

    [Fact]
    public async Task log_cost_with_more_than_four_decimal_places_is_rejected()
    {
        var camperId = await CreateCamperAsync();
        var itemId = NewId();
        await CreateMaintenanceAsync(camperId, itemId, "Cambio olio");

        using var response = await PostMaintenanceLogAsync(
            camperId,
            itemId,
            new
            {
                id = NewId(),
                performedOnUtc = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime),
                cost = new { amount = 1.23456m, currency = "EUR" },
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("cost.amount", out _), errors.GetRawText());
        await using var context = CreateOwnerContext();
        Assert.Equal(0, await context.MaintenanceLogs.CountAsync(
            log => log.MaintenanceItemId == itemId,
            TestContext.Current.CancellationToken));
    }

    private Task<HttpResponseMessage> PostMaintenanceLogAsync(
        Guid camperId,
        Guid itemId,
        object body,
        CancellationToken? cancellationToken = null)
    {
        return _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/maintenance/{itemId:D}/logs",
            body,
            cancellationToken ?? TestContext.Current.CancellationToken);
    }

    private async Task WaitForMaintenanceLockWaitAsync(
        string resourceDescription,
        bool expectedWaiting,
        CancellationToken cancellationToken)
    {
        await using var context = CreateOwnerContext();
        var timeout = Stopwatch.StartNew();
        do
        {
            var waitingCount = await context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS [Value]
                FROM sys.dm_tran_locks
                WHERE [resource_type] = N'APPLICATION'
                    AND [resource_description] = {resourceDescription}
                    AND [request_status] = N'WAIT'
                """)
                .SingleAsync(cancellationToken);
            if ((waitingCount > 0) == expectedWaiting)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
        while (timeout.Elapsed < TimeSpan.FromSeconds(10));

        Assert.Fail(
            expectedWaiting
                ? "La richiesta non risulta in attesa del lock di manutenzione."
                : "La richiesta annullata risulta ancora in attesa del lock di manutenzione.");
    }

    private async Task SeedManualOdometerAsync(Guid camperId, DateOnly takenOn, int readingKm)
    {
        await using var context = CreateOwnerContext();
        context.OdometerReadings.Add(new OdometerReading
        {
            Id = NewId(),
            OwnerId = _ownerId,
            CamperId = camperId,
            ReadingKm = readingKm,
            TakenOnUtc = takenOn,
            Source = OdometerSource.Manual,
            CreatedAtUtc = TimeProvider.System.GetUtcNow().UtcDateTime,
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task CreateMaintenanceAsync(Guid camperId, Guid id, string name)
    {
        await CreateMaintenanceAsync(camperId, id, name, null, 12);
    }

    private async Task CreateMaintenanceAsync(
        Guid camperId,
        Guid id,
        string name,
        int? intervalKm,
        short? intervalMonths)
    {
        using var response = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/maintenance",
            new
            {
                id,
                name,
                category = (int)MaintenanceCategory.Engine,
                intervalKm,
                intervalMonths,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<Guid> CreateCamperAsync()
    {
        return await CreateCamperAsync(_client);
    }

    private static async Task<Guid> CreateCamperAsync(RoamlyClient client)
    {
        var id = NewId();
        using var response = await client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "Il Girasole",
            vehicleKind = (int)VehicleKind.Van,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return id;
    }

    private static object ValidMaintenanceBody(Guid id)
    {
        return new
        {
            id,
            name = "Cambio olio",
            category = (int)MaintenanceCategory.Engine,
            intervalMonths = 12,
        };
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

    private static Guid NewId() => new SequentialGuidGenerator(TimeProvider.System).NewId();

    private static async Task AssertUniformNotFoundAsync(
        HttpResponseMessage nonOwned,
        HttpResponseMessage missing)
    {
        Assert.Equal(HttpStatusCode.NotFound, nonOwned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var nonOwnedProblem = JsonDocument.Parse(await nonOwned.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        using var missingProblem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(nonOwnedProblem.RootElement.GetProperty("type").GetString(),
            missingProblem.RootElement.GetProperty("type").GetString());
        Assert.Equal(nonOwnedProblem.RootElement.GetProperty("title").GetString(),
            missingProblem.RootElement.GetProperty("title").GetString());
        Assert.False(nonOwnedProblem.RootElement.TryGetProperty("detail", out _));
        Assert.False(missingProblem.RootElement.TryGetProperty("detail", out _));
    }

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
        public bool IsAuthenticated => true;
    }

    private sealed record MaintenanceItemResponse(
        Guid Id,
        string Name,
        DateOnly? LastServiceOnUtc,
        DateOnly? NextDueOnUtc);

    private sealed record DashboardResponse(
        DashboardOdometer? Odometer,
        IReadOnlyList<DashboardMaintenance> Maintenance);

    private sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);

    private sealed record MaintenanceListItem(
        Guid Id,
        string Name,
        int? IntervalKm,
        short? IntervalMonths);

    private sealed record DashboardMaintenance(
        Guid Id,
        bool HasServiceHistory,
        DateOnly? LastServiceOnUtc,
        DashboardAxis TimeAxis,
        DashboardOdometerAxis OdometerAxis);

    private sealed record DashboardAxis(string Status, DateOnly? DueOnUtc);

    private sealed record DashboardOdometer(int ReadingKm, DateOnly TakenOnUtc, bool IsStale);

    private sealed record DashboardOdometerAxis(string Status, int? DueAtKm);

    private sealed record MaintenanceLogResponse(
        Guid Id,
        DateOnly PerformedOnUtc,
        int? OdometerKm,
        CostResponse? Cost);

    private sealed record MaintenanceLogHistoryItem(DateOnly PerformedOnUtc);

    private sealed record CostResponse(decimal Amount, string Currency);
}
