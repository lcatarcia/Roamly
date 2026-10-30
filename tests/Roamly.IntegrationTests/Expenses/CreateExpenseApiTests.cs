using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Expenses;

[Collection(DatabaseCollection.Name)]
public sealed class CreateExpenseApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Password = "Test!Password123";
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly ExpenseTripAssociationRaceInterceptor _associationRace = new();
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
            configureTestServices: services => services.ConfigureDbContext<RoamlyDbContext>(
                options => options.AddInterceptors(_associationRace)));
        _client = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));

        await CreateUserAsync("expense-owner@example.test", _ownerId);
        using var login = await _client.LoginAsync(
            "expense-owner@example.test",
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
    public async Task traveller_can_create_and_read_a_camper_expense_with_explicit_money()
    {
        var camperId = NewId();
        using var camper = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id = camperId,
            name = "Il Grigio",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, camper.StatusCode);

        var expenseId = NewId();
        using var created = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/expenses",
            new
            {
                id = expenseId,
                amount = new { amount = 78.4321m, currency = "EUR" },
                category = 0,
                incurredOnUtc = new DateOnly(2026, 10, 8),
                description = "  Rifornimento  ",
                odometerKm = 12345,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(
            $"/api/v1/expenses/{expenseId:D}",
            created.Headers.Location?.OriginalString);
        HttpEtagAssertions.StrongRowVersion(created.Headers.ETag?.ToString());

        using var read = await _client.GetAsync(
            $"/api/v1/expenses/{expenseId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        HttpEtagAssertions.StrongRowVersion(read.Headers.ETag?.ToString());

        using var document = JsonDocument.Parse(
            await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = document.RootElement;
        Assert.Equal(expenseId, root.GetProperty("id").GetGuid());
        Assert.Equal(camperId, root.GetProperty("camperId").GetGuid());
        Assert.Equal("Rifornimento", root.GetProperty("description").GetString());
        var amount = root.GetProperty("amount");
        Assert.Equal(78.4321m, amount.GetProperty("amount").GetDecimal());
        Assert.Equal("EUR", amount.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task expense_creation_replays_normalized_content_and_rejects_id_reuse()
    {
        var camperId = await CreateCamperAsync();
        var expenseId = NewId();
        var original = ExpenseRequest(expenseId, 15.25m, "  Rifornimento  ");
        using var created = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/expenses",
            original,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var etag = created.Headers.ETag?.ToString();
        Assert.NotNull(etag);

        using var replayed = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/expenses",
            ExpenseRequest(expenseId, 15.25m, "Rifornimento"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.Equal(etag, replayed.Headers.ETag?.ToString());

        using var conflict = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/expenses",
            ExpenseRequest(expenseId, 15.26m, "Rifornimento"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        using var current = await _client.GetAsync(
            $"/api/v1/expenses/{expenseId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(etag, current.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task expense_validation_rejects_invalid_values_and_allows_signed_refunds()
    {
        var camperId = await CreateCamperAsync();
        var invalidRequests = new object[]
        {
            new { id = NewId(), amount = new { amount = 0m, currency = "EUR" }, category = 0, incurredOnUtc = new DateOnly(2026, 10, 8) },
            new { id = NewId(), amount = new { amount = 1m, currency = "eur" }, category = 0, incurredOnUtc = new DateOnly(2026, 10, 8) },
            new { id = NewId(), amount = new { amount = 1.12345m, currency = "EUR" }, category = 0, incurredOnUtc = new DateOnly(2026, 10, 8) },
            new { id = NewId(), amount = new { amount = 1.23000m, currency = "EUR" }, category = 0, incurredOnUtc = new DateOnly(2026, 10, 8) },
            new { id = NewId(), amount = new { amount = decimal.MinValue, currency = "EUR" }, category = 0, incurredOnUtc = new DateOnly(2026, 10, 8) },
            new { id = NewId(), amount = new { amount = 1m, currency = "EUR" }, category = 99, incurredOnUtc = new DateOnly(2026, 10, 8) },
            new { id = NewId(), amount = new { amount = 1m, currency = "EUR" }, category = 0, incurredOnUtc = new DateOnly(2026, 10, 8), odometerKm = -1 },
            new { id = NewId(), amount = new { amount = 1m, currency = "EUR" }, category = 0, incurredOnUtc = new DateOnly(2026, 10, 8), description = new string('x', 201) },
            new { id = NewId(), amount = new { amount = 1m, currency = "EUR" }, category = 0 },
        };

        foreach (var invalidRequest in invalidRequests)
        {
            using var invalid = await _client.PostJsonAsync(
                $"/api/v1/campers/{camperId:D}/expenses",
                invalidRequest,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        var refundId = NewId();
        using var refund = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/expenses",
            new
            {
                id = refundId,
                amount = new { amount = -12.3456m, currency = "EUR" },
                category = 0,
                incurredOnUtc = new DateOnly(2026, 10, 8),
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, refund.StatusCode);
        using var collection = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/expenses",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(
            await collection.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(refundId, item.GetProperty("id").GetGuid());
        Assert.Equal(-12.3456m, item.GetProperty("amount").GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task foreign_owner_expenses_and_campers_are_uniformly_not_found()
    {
        var otherEmail = "other-expense-owner@example.test";
        await CreateUserAsync(otherEmail, Guid.NewGuid());
        using var otherClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        using var login = await otherClient.LoginAsync(otherEmail, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var foreignCamperId = NewId();
        using var camper = await otherClient.PostJsonAsync("/api/v1/campers", new
        {
            id = foreignCamperId,
            name = "Camper privato",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, camper.StatusCode);
        var foreignExpenseId = NewId();
        using var expense = await otherClient.PostJsonAsync(
            $"/api/v1/campers/{foreignCamperId:D}/expenses",
            ExpenseRequest(foreignExpenseId, 12m, "Spesa privata"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, expense.StatusCode);

        using var foreignRead = await _client.GetAsync(
            $"/api/v1/expenses/{foreignExpenseId:D}",
            TestContext.Current.CancellationToken);
        using var absentRead = await _client.GetAsync(
            $"/api/v1/expenses/{NewId():D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, absentRead.StatusCode);
        using var foreignProblem = JsonDocument.Parse(
            await foreignRead.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var absentProblem = JsonDocument.Parse(
            await absentRead.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(foreignProblem.RootElement.GetProperty("type").GetString(),
            absentProblem.RootElement.GetProperty("type").GetString());
        Assert.Equal(foreignProblem.RootElement.GetProperty("title").GetString(),
            absentProblem.RootElement.GetProperty("title").GetString());

        using var foreignUpdate = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/v1/expenses/{foreignExpenseId:D}",
            null,
            UpdateBody(99m, null));
        Assert.Equal(HttpStatusCode.NotFound, foreignUpdate.StatusCode);
        using var foreignDelete = await SendJsonAsync(
            HttpMethod.Delete,
            $"/api/v1/expenses/{foreignExpenseId:D}",
            null,
            new { });
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);
        using var foreignCollection = await _client.GetAsync(
            $"/api/v1/campers/{foreignCamperId:D}/expenses",
            TestContext.Current.CancellationToken);
        Assert.True(
            foreignCollection.StatusCode == HttpStatusCode.NotFound,
            await foreignCollection.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
                + " uri=" + foreignCollection.RequestMessage?.RequestUri);
        using var foreignCreate = await _client.PostJsonAsync(
            $"/api/v1/campers/{foreignCamperId:D}/expenses",
            ExpenseRequest(NewId(), 33m, "Tentativo"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignCreate.StatusCode);

        using var unchanged = await otherClient.GetAsync(
            $"/api/v1/expenses/{foreignExpenseId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
    }

    [Fact]
    public async Task expense_trip_association_requires_same_owner_camper_and_non_cancelled_trip()
    {
        var camperId = await CreateCamperAsync();
        var otherCamperId = await CreateCamperAsync();
        var mismatchedTripId = await CreateTripAsync(otherCamperId);

        using var mismatch = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/expenses",
            ExpenseRequestWithTrip(NewId(), 10m, mismatchedTripId),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, mismatch.StatusCode);

        var foreignTripId = await CreateForeignTripAsync();
        using var foreignTrip = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/expenses",
            ExpenseRequestWithTrip(NewId(), 11m, foreignTripId),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignTrip.StatusCode);

        var cancelledTripId = await CreateTripAsync(camperId);
        var linkedExpenseId = NewId();
        using var linked = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/expenses",
            ExpenseRequestWithTrip(linkedExpenseId, 20m, cancelledTripId),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, linked.StatusCode);

        using var tripRead = await _client.GetAsync(
            $"/api/v1/trips/{cancelledTripId:D}",
            TestContext.Current.CancellationToken);
        var tripEtag = tripRead.Headers.ETag?.ToString();
        Assert.NotNull(tripEtag);
        using var cancelled = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{cancelledTripId:D}/transitions",
            tripEtag,
            new { status = 3 });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        using var replayedExpenseUpdate = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/v1/expenses/{linkedExpenseId:D}",
            linked.Headers.ETag?.ToString(),
            UpdateBody(21m, cancelledTripId));
        Assert.Equal(HttpStatusCode.OK, replayedExpenseUpdate.StatusCode);

        using var newExpense = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/expenses",
            ExpenseRequestWithTrip(NewId(), 30m, cancelledTripId),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, newExpense.StatusCode);

        var unlinkedExpenseId = await CreateExpenseAsync(camperId, 40m, "EUR");
        using var unlinkedRead = await _client.GetAsync(
            $"/api/v1/expenses/{unlinkedExpenseId:D}",
            TestContext.Current.CancellationToken);
        using var attachCancelled = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/v1/expenses/{unlinkedExpenseId:D}",
            unlinkedRead.Headers.ETag?.ToString(),
            UpdateBody(40m, cancelledTripId));
        Assert.Equal(HttpStatusCode.Conflict, attachCancelled.StatusCode);
        using var unchanged = await _client.GetAsync(
            $"/api/v1/expenses/{unlinkedExpenseId:D}",
            TestContext.Current.CancellationToken);
        using var unchangedDocument = JsonDocument.Parse(
            await unchanged.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(JsonValueKind.Null, unchangedDocument.RootElement.GetProperty("tripId").ValueKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task expense_cannot_be_associated_using_a_trip_status_read_that_races_with_cancellation(
        bool updateExistingExpense)
    {
        var camperId = await CreateCamperAsync();
        var tripId = await CreateTripAsync(camperId);
        var tripPath = $"/api/v1/trips/{tripId:D}";
        using var tripRead = await _client.GetAsync(tripPath, TestContext.Current.CancellationToken);
        var tripEtag = tripRead.Headers.ETag?.ToString();
        Assert.NotNull(tripEtag);

        var expensePath = (string?)null;
        var expenseEtag = (string?)null;
        if (updateExistingExpense)
        {
            var expenseId = await CreateExpenseAsync(camperId, 25m, "EUR");
            expensePath = $"/api/v1/expenses/{expenseId:D}";
            using var expenseRead = await _client.GetAsync(
                expensePath,
                TestContext.Current.CancellationToken);
            expenseEtag = expenseRead.Headers.ETag?.ToString();
            Assert.NotNull(expenseEtag);
        }

        _associationRace.Arm();
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        Task<HttpResponseMessage>? cancellation = null;
        Task<HttpResponseMessage>? association = null;
        HttpResponseMessage? cancelled = null;
        HttpResponseMessage? linked = null;
        try
        {
            cancellation = SendJsonAsync(
                HttpMethod.Post,
                tripPath + "/transitions",
                tripEtag,
                new { status = 3 },
                requestTimeout.Token);
            await _associationRace.TransitionReadHeld.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);

            association = updateExistingExpense
                ? SendJsonAsync(
                    HttpMethod.Put,
                    expensePath!,
                    expenseEtag,
                    UpdateBody(25m, tripId),
                    requestTimeout.Token)
                : _client.PostJsonAsync(
                    $"/api/v1/campers/{camperId:D}/expenses",
                    ExpenseRequestWithTrip(NewId(), 25m, tripId),
                    requestTimeout.Token);
            await _associationRace.AssociationReadStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                requestTimeout.Token);
            await AssertAssociationWaitsForTransitionLockAsync(
                _associationRace.TransitionSessionId.Task,
                _associationRace.AssociationSessionId.Task,
                requestTimeout.Token);

            _associationRace.ReleaseTransitionRead();

            cancelled = await cancellation;
            linked = await association!;

            Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, linked.StatusCode);
            if (updateExistingExpense)
            {
                using var unchanged = await _client.GetAsync(
                    expensePath!,
                    TestContext.Current.CancellationToken);
                using var document = JsonDocument.Parse(
                    await unchanged.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("tripId").ValueKind);
            }
            else
            {
                using var expenses = await _client.GetAsync(
                    $"/api/v1/campers/{camperId:D}/expenses",
                    TestContext.Current.CancellationToken);
                using var document = JsonDocument.Parse(
                    await expenses.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                Assert.Empty(document.RootElement.GetProperty("items").EnumerateArray());
            }
        }
        finally
        {
            _associationRace.ReleaseTransitionRead();
            try
            {
                requestTimeout.Cancel();
            }
            catch (Exception)
            {
                // Continue draining requests even if a cancellation callback fails.
            }

            await CleanupRaceRequestsAsync(cancellation, association, cancelled, linked);
        }
    }

    [Fact]
    public async Task traveller_can_page_expenses_in_id_order()
    {
        var camperId = await CreateCamperAsync();
        var firstId = await CreateExpenseAsync(camperId, 12m, "EUR");
        var secondId = await CreateExpenseAsync(camperId, 24m, "EUR");

        using var firstPage = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/expenses?limit=1",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);

        using var firstDocument = JsonDocument.Parse(
            await firstPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var firstRoot = firstDocument.RootElement;
        Assert.Equal(firstId, firstRoot.GetProperty("items")[0].GetProperty("id").GetGuid());
        var nextCursor = firstRoot.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(nextCursor));

        using var secondPage = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/expenses?limit=1&after={Uri.EscapeDataString(nextCursor!)}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
        using var secondDocument = JsonDocument.Parse(
            await secondPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(secondId, secondDocument.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, secondDocument.RootElement.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task expense_collection_supports_typed_trip_filter_and_rejects_bad_cursors_and_limits()
    {
        var camperId = await CreateCamperAsync();
        var tripId = await CreateTripAsync(camperId);
        var linkedId = NewId();
        using var linked = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/expenses",
            ExpenseRequestWithTrip(linkedId, 15m, tripId),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, linked.StatusCode);
        await CreateExpenseAsync(camperId, 30m, "EUR");

        using var filtered = await _client.GetAsync(
            $"/api/v1/campers/{camperId:D}/expenses?tripId={tripId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        using var filteredDocument = JsonDocument.Parse(
            await filtered.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var filteredItem = Assert.Single(filteredDocument.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(linkedId, filteredItem.GetProperty("id").GetGuid());

        foreach (var path in new[]
        {
            $"/api/v1/campers/{camperId:D}/expenses?after=not-a-cursor",
            $"/api/v1/campers/{camperId:D}/expenses?limit=0",
            $"/api/v1/campers/{camperId:D}/expenses?limit=201",
        })
        {
            using var invalid = await _client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
    }

    [Fact]
    public async Task owner_can_update_an_expense_without_reassigning_its_camper()
    {
        var camperId = await CreateCamperAsync();
        var expenseId = await CreateExpenseAsync(camperId, 12m, "EUR");
        using var initialRead = await _client.GetAsync(
            $"/api/v1/expenses/{expenseId:D}",
            TestContext.Current.CancellationToken);
        var etag = HttpEtagAssertions.StrongRowVersion(initialRead.Headers.ETag?.ToString());

        using var update = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/v1/expenses/{expenseId:D}",
            etag,
            new
            {
                camperId = NewId(),
                tripId = (Guid?)null,
                amount = new { amount = -4.125m, currency = "EUR" },
                category = 6,
                incurredOnUtc = new DateOnly(2026, 10, 7),
                description = "  Rimborso  ",
                odometerKm = 12346,
            });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.NotEqual(etag, update.Headers.ETag?.ToString());

        using var updatedRead = await _client.GetAsync(
            $"/api/v1/expenses/{expenseId:D}",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(
            await updatedRead.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = document.RootElement;
        Assert.Equal(camperId, root.GetProperty("camperId").GetGuid());
        Assert.Equal("Rimborso", root.GetProperty("description").GetString());
        Assert.Equal(-4.125m, root.GetProperty("amount").GetProperty("amount").GetDecimal());
        Assert.Equal(6, root.GetProperty("category").GetInt32());
    }

    [Fact]
    public async Task expense_update_rejects_invalid_money_without_mutating_the_expense()
    {
        var camperId = await CreateCamperAsync();
        var expenseId = await CreateExpenseAsync(camperId, 12.3456m, "EUR");
        var path = $"/api/v1/expenses/{expenseId:D}";
        using var initial = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        var originalEtag = initial.Headers.ETag?.ToString();
        Assert.NotNull(originalEtag);

        foreach (var invalid in new[]
        {
            (Amount: 0m, Currency: "EUR"),
            (Amount: 1.12345m, Currency: "EUR"),
            (Amount: 1.23000m, Currency: "EUR"),
            (Amount: decimal.MinValue, Currency: "EUR"),
            (Amount: 1m, Currency: "eur"),
            (Amount: 1m, Currency: "U1R"),
        })
        {
            using var rejected = await SendJsonAsync(
                HttpMethod.Put,
                path,
                originalEtag,
                new
                {
                    tripId = (Guid?)null,
                    amount = new { amount = invalid.Amount, currency = invalid.Currency },
                    category = 0,
                    incurredOnUtc = new DateOnly(2026, 10, 8),
                    description = "Non salvare",
                });
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }

        using var unchanged = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(originalEtag, unchanged.Headers.ETag?.ToString());
        using var document = JsonDocument.Parse(
            await unchanged.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(12.3456m, document.RootElement.GetProperty("amount").GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task expense_update_can_link_and_reassign_only_to_a_trip_of_its_owner_and_camper()
    {
        var camperId = await CreateCamperAsync();
        var otherCamperId = await CreateCamperAsync();
        var firstTripId = await CreateTripAsync(camperId);
        var secondTripId = await CreateTripAsync(camperId);
        var differentCamperTripId = await CreateTripAsync(otherCamperId);
        var foreignTripId = await CreateForeignTripAsync();
        var expenseId = await CreateExpenseAsync(camperId, 14m, "EUR");
        var path = $"/api/v1/expenses/{expenseId:D}";
        using var initial = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        var etag = initial.Headers.ETag?.ToString();
        Assert.NotNull(etag);

        using var linked = await SendJsonAsync(
            HttpMethod.Put,
            path,
            etag,
            UpdateBody(14m, firstTripId));
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        etag = linked.Headers.ETag?.ToString();
        Assert.NotNull(etag);

        using var reassigned = await SendJsonAsync(
            HttpMethod.Put,
            path,
            etag,
            UpdateBody(15m, secondTripId));
        Assert.Equal(HttpStatusCode.OK, reassigned.StatusCode);
        etag = reassigned.Headers.ETag?.ToString();
        Assert.NotNull(etag);

        foreach (var invalidTripId in new[] { differentCamperTripId, foreignTripId })
        {
            using var rejected = await SendJsonAsync(
                HttpMethod.Put,
                path,
                etag,
                UpdateBody(99m, invalidTripId));
            Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
        }

        using var current = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(etag, current.Headers.ETag?.ToString());
        using var document = JsonDocument.Parse(
            await current.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(secondTripId, document.RootElement.GetProperty("tripId").GetGuid());
        Assert.Equal(15m, document.RootElement.GetProperty("amount").GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task expense_update_requires_a_valid_current_etag_and_is_atomic()
    {
        var camperId = await CreateCamperAsync();
        var expenseId = await CreateExpenseAsync(camperId, 12m, "EUR");
        var path = $"/api/v1/expenses/{expenseId:D}";
        using var initialRead = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        var etag = initialRead.Headers.ETag?.ToString();
        Assert.NotNull(etag);
        var body = new
        {
            amount = new { amount = 20m, currency = "EUR" },
            category = 1,
            incurredOnUtc = new DateOnly(2026, 10, 8),
            description = "Aggiornata",
            odometerKm = (int?)null,
            tripId = (Guid?)null,
        };

        using var missing = await SendJsonAsync(HttpMethod.Put, path, null, body);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        using var weak = await SendJsonAsync(
            HttpMethod.Put,
            path,
            HttpEtagAssertions.AsWeak(etag),
            body);
        Assert.Equal(HttpStatusCode.PreconditionFailed, weak.StatusCode);
        using var updated = await SendJsonAsync(HttpMethod.Put, path, etag, body);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var currentEtag = updated.Headers.ETag?.ToString();
        HttpEtagAssertions.StrongRowVersion(currentEtag);
        Assert.NotEqual(etag, currentEtag);

        using var stale = await SendJsonAsync(HttpMethod.Put, path, etag, body with
        {
            amount = new { amount = 99m, currency = "EUR" },
        });
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);

        using var current = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(currentEtag, current.Headers.ETag?.ToString());
        using var document = JsonDocument.Parse(
            await current.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(20m, document.RootElement.GetProperty("amount").GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task expense_update_returns_precondition_failed_on_database_rowversion_conflict()
    {
        var camperId = await CreateCamperAsync();
        var expenseId = await CreateExpenseAsync(camperId, 12m, "EUR");
        var path = $"/api/v1/expenses/{expenseId:D}";
        using var read = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        var oldEtag = read.Headers.ETag?.ToString();
        Assert.NotNull(oldEtag);

        await ChangeExpenseOutsideRequestAsync(expenseId);
        using var conflict = await SendJsonAsync(
            HttpMethod.Put,
            path,
            oldEtag,
            UpdateBody(99m, null));
        Assert.Equal(HttpStatusCode.PreconditionFailed, conflict.StatusCode);

        using var current = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(
            await current.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Changed outside HTTP", document.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public async Task owner_can_delete_an_expense_only_with_its_current_etag()
    {
        var camperId = await CreateCamperAsync();
        var expenseId = await CreateExpenseAsync(camperId, 12m, "EUR");
        var path = $"/api/v1/expenses/{expenseId:D}";
        using var read = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        var etag = read.Headers.ETag?.ToString();
        Assert.NotNull(etag);

        using var missing = await SendJsonAsync(HttpMethod.Delete, path, null, new { });
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);

        using var updated = await SendJsonAsync(
            HttpMethod.Put,
            path,
            etag,
            UpdateBody(13m, null));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var currentEtag = updated.Headers.ETag?.ToString();
        Assert.NotNull(currentEtag);

        using var stale = await SendJsonAsync(HttpMethod.Delete, path, etag, new { });
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using var stillPresent = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(currentEtag, stillPresent.Headers.ETag?.ToString());
        using var stillPresentDocument = JsonDocument.Parse(
            await stillPresent.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            13m,
            stillPresentDocument.RootElement.GetProperty("amount").GetProperty("amount").GetDecimal());

        using var deleted = await SendJsonAsync(HttpMethod.Delete, path, currentEtag, new { });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var stillThere = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, stillThere.StatusCode);
    }

    private async Task CreateUserAsync(string email, Guid userId)
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

    private async Task<Guid> CreateCamperAsync()
    {
        var camperId = NewId();
        using var response = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id = camperId,
            name = "Il Grigio",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return camperId;
    }

    private async Task<Guid> CreateExpenseAsync(Guid camperId, decimal value, string currency)
    {
        var expenseId = NewId();
        using var response = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/expenses",
            new
            {
                id = expenseId,
                amount = new { amount = value, currency },
                category = 0,
                incurredOnUtc = new DateOnly(2026, 10, 8),
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return expenseId;
    }

    private async Task<Guid> CreateTripAsync(Guid camperId)
    {
        var tripId = NewId();
        using var response = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            new { id = tripId, name = "Viaggio", stops = new[] { new { name = "Rennes" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return tripId;
    }

    private async Task<Guid> CreateForeignTripAsync()
    {
        var email = "foreign-trip-expense-owner@example.test";
        await CreateUserAsync(email, Guid.NewGuid());
        using var foreignClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        using var login = await foreignClient.LoginAsync(email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var camperId = NewId();
        using var camper = await foreignClient.PostJsonAsync("/api/v1/campers", new
        {
            id = camperId,
            name = "Camper estero",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, camper.StatusCode);
        var tripId = NewId();
        using var trip = await foreignClient.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            new { id = tripId, name = "Viaggio estero", stops = new[] { new { name = "Lione" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, trip.StatusCode);
        return tripId;
    }

    private async Task ChangeExpenseOutsideRequestAsync(Guid expenseId)
    {
        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .Options;
        await using var db = new RoamlyDbContext(options, new TestCurrentUser(_ownerId));
        var expense = await db.Expenses.SingleAsync(
            candidate => candidate.OwnerId == _ownerId && candidate.Id == expenseId,
            TestContext.Current.CancellationToken);
        expense.Description = "Changed outside HTTP";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static object ExpenseRequest(Guid id, decimal value, string description) => new
    {
        id,
        amount = new { amount = value, currency = "EUR" },
        category = 0,
        incurredOnUtc = new DateOnly(2026, 10, 8),
        description,
    };

    private static object ExpenseRequestWithTrip(Guid id, decimal value, Guid tripId) => new
    {
        id,
        amount = new { amount = value, currency = "EUR" },
        category = 0,
        incurredOnUtc = new DateOnly(2026, 10, 8),
        tripId,
    };

    private static object UpdateBody(decimal value, Guid? tripId) => new
    {
        tripId,
        amount = new { amount = value, currency = "EUR" },
        category = 0,
        incurredOnUtc = new DateOnly(2026, 10, 8),
        description = "Modificata",
    };

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
        public bool IsAuthenticated => true;
    }

    private async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string path,
        string? etag,
        object body,
        CancellationToken? cancellationToken = null)
    {
        var requestCancellation = cancellationToken ?? TestContext.Current.CancellationToken;
        using var csrf = await _client.GetAsync("/api/v1/csrf-token", requestCancellation);
        var csrfToken = await csrf.Content.ReadFromJsonAsync<CsrfTokenResponse>(requestCancellation);
        Assert.NotNull(csrfToken);
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add("X-XSRF-TOKEN", csrfToken.Token);
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return await _client.SendAsync(request, requestCancellation);
    }

    private async Task AssertAssociationWaitsForTransitionLockAsync(
        Task<int> transitionSessionIdTask,
        Task<int> associationSessionIdTask,
        CancellationToken cancellationToken)
    {
        var transitionSessionId = await transitionSessionIdTask.WaitAsync(
            TimeSpan.FromSeconds(10),
            cancellationToken);
        var associationSessionId = await associationSessionIdTask.WaitAsync(
            TimeSpan.FromSeconds(10),
            cancellationToken);
        Assert.NotEqual(transitionSessionId, associationSessionId);

        await using var connection = new SqlConnection(_database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [wait_type], [blocking_session_id]
            FROM sys.dm_exec_requests
            WHERE [session_id] = @sessionId AND [blocking_session_id] <> 0;
            """;
        command.CommandTimeout = 2;
        command.Parameters.Add(new SqlParameter("@sessionId", System.Data.SqlDbType.Int)
        {
            Value = associationSessionId,
        });

        var timeout = Stopwatch.StartNew();
        do
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken)
                && !reader.IsDBNull(0)
                && reader.GetString(0).StartsWith("LCK_M_", StringComparison.Ordinal)
                && reader.GetInt16(1) == transitionSessionId)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
        while (timeout.Elapsed < TimeSpan.FromSeconds(10));

        Assert.Fail(
            $"La richiesta SQL {associationSessionId} non risulta in attesa di un lock bloccato "
            + $"dalla sessione {transitionSessionId}.");
    }

    private static async Task CleanupRaceRequestsAsync(
        Task<HttpResponseMessage>? cancellation,
        Task<HttpResponseMessage>? association,
        params HttpResponseMessage?[] receivedResponses)
    {
        var disposedResponses = new HashSet<HttpResponseMessage>(ReferenceEqualityComparer.Instance);
        foreach (var response in receivedResponses)
        {
            DisposeResponse(response);
        }

        var requests = new[] { cancellation, association }
            .Where(request => request is not null)
            .Select(request => request!)
            .ToArray();

        if (requests.Length == 0)
        {
            return;
        }

        var allRequests = Task.WhenAll(requests);
        try
        {
            await allRequests.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (Exception)
        {
            // Cleanup must preserve the assertion/gate failure that brought us here.
            // Request task failures are still explicitly observed below.
        }

        foreach (var request in requests)
        {
            if (request.IsCompleted)
            {
                if (request.IsFaulted)
                {
                    _ = request.Exception;
                }

                if (request.Status == TaskStatus.RanToCompletion)
                {
                    DisposeResponse(request.Result);
                }
            }
            else
            {
                _ = request.ContinueWith(
                    static completed =>
                    {
                        if (completed.IsFaulted)
                        {
                            _ = completed.Exception;
                        }
                        else if (completed.Status == TaskStatus.RanToCompletion)
                        {
                            try
                            {
                                completed.Result.Dispose();
                            }
                            catch (Exception)
                            {
                                // This continuation exists only to finish bounded cleanup.
                            }
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        void DisposeResponse(HttpResponseMessage? response)
        {
            if (response is null || !disposedResponses.Add(response))
            {
                return;
            }

            try
            {
                response.Dispose();
            }
            catch (Exception)
            {
                // Do not replace the original test failure with a cleanup failure.
            }
        }
    }

    private static Guid NewId() => new SequentialGuidGenerator(TimeProvider.System).NewId();

    private sealed record CsrfTokenResponse(string Token);

    private sealed class ExpenseTripAssociationRaceInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentDictionary<DbCommand, int> _tripReads = new();
        private readonly TaskCompletionSource _transitionReadRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _armed;
        private int _tripReadCount;

        public TaskCompletionSource TransitionReadHeld { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AssociationReadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<int> TransitionSessionId { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<int> AssociationSessionId { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Arm()
        {
            Interlocked.Exchange(ref _tripReadCount, 0);
            Interlocked.Exchange(ref _armed, 1);
        }

        public void ReleaseTransitionRead() => _transitionReadRelease.TrySetResult();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _armed) == 1
                && command.CommandText.Contains("FROM [Trips]", StringComparison.OrdinalIgnoreCase))
            {
                var sequence = Interlocked.Increment(ref _tripReadCount);
                _tripReads.TryAdd(command, sequence);
                if (command.Connection is SqlConnection connection)
                {
                    if (sequence == 1)
                    {
                        TransitionSessionId.TrySetResult(connection.ServerProcessId);
                    }
                    else if (sequence == 2)
                    {
                        AssociationSessionId.TrySetResult(connection.ServerProcessId);
                        AssociationReadStarted.TrySetResult();
                    }
                }
            }

            return ValueTask.FromResult(result);
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (_tripReads.TryRemove(command, out var sequence))
            {
                if (sequence == 1)
                {
                    TransitionReadHeld.TrySetResult();
                    await _transitionReadRelease.Task.WaitAsync(cancellationToken);
                }
            }

            return result;
        }
    }
}
