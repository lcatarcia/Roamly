using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Trips;

[Collection(DatabaseCollection.Name)]
public sealed class ChecklistApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Email = "trip-checklist@example.test";
    private const string Password = "Test!Password123";
    private static readonly DateTimeOffset ClockNow =
        new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly FixedTimeProvider _clock = new(ClockNow);
    private readonly ChecklistSeedIdentityReadBarrier _seedIdentityReadBarrier = new();
    private readonly ChecklistSeedConcurrencyExceptionInterceptor _seedConcurrencyException = new();
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
                services.ConfigureDbContext<RoamlyDbContext>(
                    options => options.AddInterceptors(
                        _seedIdentityReadBarrier,
                        _seedConcurrencyException));
            });
        _client = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        await CreateUserAsync(_ownerId, Email);
        using var login = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);
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
    public async Task owner_can_create_a_checklist_for_an_owned_trip_add_an_item_and_read_it_back()
    {
        var trip = await CreateTripAsync();
        var checklistId = NewId();
        using var created = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists",
            new { id = checklistId, name = "Prima di partire", kind = 0 },
            trip.ETag);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var tripEtag = HttpEtagAssertions.StrongRowVersion(created.Headers.ETag?.ToString());

        var itemId = NewId();
        using var added = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists/{checklistId:D}/items",
            new { id = itemId, text = "Controllare le finestre" },
            tripEtag);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(
            $"/api/v1/trips/{trip.Id:D}/checklists/{checklistId:D}/items/{itemId:D}",
            added.Headers.Location?.OriginalString);
        var addedEtag = HttpEtagAssertions.StrongRowVersion(added.Headers.ETag?.ToString());
        Assert.NotEqual(tripEtag, addedEtag);

        using var weakUpdate = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/v1/trips/{trip.Id:D}/checklists/{checklistId:D}/items/{itemId:D}",
            new { text = "Controllare le finestre", isDone = true },
            HttpEtagAssertions.AsWeak(addedEtag));
        Assert.Equal(HttpStatusCode.PreconditionFailed, weakUpdate.StatusCode);

        using var itemRead = await _client.GetAsync(
            added.Headers.Location!.OriginalString,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, itemRead.StatusCode);
        HttpEtagAssertions.StrongRowVersion(itemRead.Headers.ETag?.ToString());
        using var itemBody = JsonDocument.Parse(await itemRead.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(itemId, itemBody.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Controllare le finestre", itemBody.RootElement.GetProperty("text").GetString());

        using var read = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists/{checklistId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var body = JsonDocument.Parse(await read.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(checklistId, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Prima di partire", body.RootElement.GetProperty("name").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("kind").GetInt32());
        var item = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(itemId, item.GetProperty("id").GetGuid());
        Assert.Equal("Controllare le finestre", item.GetProperty("text").GetString());
        Assert.False(item.GetProperty("isDone").GetBoolean());
        Assert.Equal(1, item.GetProperty("sortOrder").GetInt32());
    }

    [Fact]
    public async Task checklist_seed_preview_uses_trip_dates_and_equipment_without_mutating_data()
    {
        var trip = await CreateTripAsync();
        await SetSeedContextAsync(
            trip,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 8),
            EquipmentCategory.BikeRack,
            EquipmentCategory.Awning,
            EquipmentCategory.Levellers,
            EquipmentCategory.Battery);
        var etagBeforePreview = await ReadTripEtagAsync(trip.Id);
        Assert.NotNull(etagBeforePreview);

        var expected = new Dictionary<int, string[]>
        {
            [0] =
            [
                "chiudi finestre",
                "chiudi gas",
                "scollega corrente",
                "controlla acqua",
                "controlla gomme",
                "controlla luci",
                "blocca oggetti interni",
                "Pianifica i rifornimenti di acqua e gas per la durata del viaggio",
                "Blocca il carico sul portabici",
                "Chiudi e blocca il tendalino",
            ],
            [1] =
            [
                "livella camper",
                "collega corrente",
                "controlla acqua",
                "configura area esterna",
                "Prepara i livellatori",
            ],
            [2] =
            [
                "scarica acqua",
                "controlla batteria",
                "chiudi gas",
                "controlla frigorifero",
                "proteggi il mezzo",
                "Verifica la batteria servizi",
            ],
        };

        foreach (var (kind, expectedTexts) in expected)
        {
            using var response = await _client.GetAsync(
                $"/api/v1/trips/{trip.Id:D}/checklists/seed-preview?kind={kind}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(etagBeforePreview, response.Headers.ETag?.ToString());
            var preview = await response.Content.ReadFromJsonAsync<ChecklistSeedPreviewSnapshot>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(preview);
            Assert.Equal(kind, preview.Kind);
            Assert.Equal(expectedTexts, preview.Items.Select(item => item.Text));
            Assert.Equal(
                preview.Items.Count,
                preview.Items.Select(item => item.Text).Distinct(StringComparer.Ordinal).Count());
        }

        Assert.Equal(etagBeforePreview, await ReadTripEtagAsync(trip.Id));
        using var checklists = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists?limit=10",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, checklists.StatusCode);
        var page = await checklists.Content.ReadFromJsonAsync<PageResponse<ChecklistSnapshot>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(etagBeforePreview, checklists.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task checklist_seed_preview_does_not_assume_duration_when_planned_dates_are_missing()
    {
        var trip = await CreateTripAsync();
        using var response = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists/seed-preview?kind=0",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<ChecklistSeedPreviewSnapshot>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(preview);
        Assert.DoesNotContain(preview.Items, item => item.TemplateKey == "duration.water-gas");
        Assert.Equal(trip.ETag, response.Headers.ETag?.ToString());
        Assert.Equal(trip.ETag, await ReadTripEtagAsync(trip.Id));
    }

    [Fact]
    public async Task owner_can_confirm_seed_checklist_with_catalog_content_and_idempotent_replay()
    {
        var trip = await CreateTripAsync();
        await SetSeedContextAsync(
            trip,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 8),
            EquipmentCategory.BikeRack);
        trip = trip with { ETag = (await ReadTripEtagAsync(trip.Id))! };
        var manual = await CreateChecklistAsync(trip, NewId(), "La mia partenza", 0);
        using var previewResponse = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists/seed-preview?kind=0",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ChecklistSeedPreviewSnapshot>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(preview);

        var seedId = NewId();
        var itemIdentities = preview.Items.Select(item => new
        {
            id = NewId(),
            templateKey = item.TemplateKey,
            text = "Testo inviato dal client da ignorare",
        }).ToArray();
        var confirmation = new
        {
            id = seedId,
            kind = 0,
            name = "Nome inviato dal client da ignorare",
            items = itemIdentities,
        };

        using var created = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists/seed",
            confirmation,
            previewResponse.Headers.ETag?.ToString());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(
            $"/api/v1/trips/{trip.Id:D}/checklists/{seedId:D}",
            created.Headers.Location?.OriginalString);
        var createdEtag = AssertEtagChanged(
            previewResponse.Headers.ETag?.ToString()!,
            created);
        var createdBody = await created.Content.ReadFromJsonAsync<ChecklistSnapshot>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(createdBody);
        Assert.Equal("Partenza", createdBody.Name);
        Assert.Equal(0, createdBody.Kind);
        Assert.Equal(itemIdentities.Select(item => item.id), createdBody.Items.Select(item => item.Id));
        Assert.Equal(preview.Items.Select(item => item.Text), createdBody.Items.Select(item => item.Text));
        Assert.All(createdBody.Items, item => Assert.NotEqual("Testo inviato dal client da ignorare", item.Text));

        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .Options;
        await using (var db = new RoamlyDbContext(options, new TestCurrentUser(_ownerId)))
        {
            var stored = await db.Checklists.SingleAsync(
                checklist => checklist.OwnerId == _ownerId && checklist.Id == seedId,
                TestContext.Current.CancellationToken);
            Assert.Equal(MaintenanceOrigin.Seed, stored.Origin);
            Assert.Equal("checklist.departure", stored.SeedTemplateKey);
            Assert.Equal("1", stored.SeedCatalogVersion);
        }

        using var replay = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists/seed",
            confirmation,
            previewResponse.Headers.ETag?.ToString());
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(createdEtag, replay.Headers.ETag?.ToString());

        using var conflict = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists/seed",
            new { confirmation.id, kind = 1, confirmation.items },
            previewResponse.Headers.ETag?.ToString());
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        using var missingPrecondition = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists/seed",
            new { id = NewId(), kind = 0, items = itemIdentities },
            null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missingPrecondition.StatusCode);

        Assert.Equal("La mia partenza", (await ReadChecklistAsync(manual)).Name);
        using var listResponse = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists?limit=10",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<PageResponse<ChecklistSnapshot>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        Assert.Equal(2, list.Items.Count);
        Assert.Contains(list.Items, checklist => checklist.Id == seedId);
        Assert.Contains(list.Items, checklist => checklist.Id == manual.Id);
    }

    [Fact]
    public async Task concurrent_seed_confirmations_with_the_same_identity_return_one_created_and_one_replay()
    {
        var trip = await CreateTripAsync();
        using var previewResponse = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists/seed-preview?kind=0",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ChecklistSeedPreviewSnapshot>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(preview);

        var checklistId = NewId();
        var items = preview.Items.Select(item => new
        {
            id = NewId(),
            templateKey = item.TemplateKey,
        }).ToArray();
        var body = new { id = checklistId, kind = 0, items };
        var etag = previewResponse.Headers.ETag?.ToString();
        Assert.NotNull(etag);
        var path = $"/api/v1/trips/{trip.Id:D}/checklists/seed";
        var gateId = NewId();
        await CreateChecklistSeedGateAsync(gateId, checklistId, holdUntilReleased: true);
        _seedIdentityReadBarrier.Arm(checklistId);
        _seedConcurrencyException.Arm();
        Task<HttpResponseMessage>? firstTask = null;
        Task<HttpResponseMessage>? secondTask = null;

        try
        {
            firstTask = SendJsonAsync(HttpMethod.Post, path, body, etag);
            secondTask = SendJsonAsync(HttpMethod.Post, path, body, etag);
            await _seedIdentityReadBarrier.WaitForBothInitialIdentityReadsAsync(
                TestContext.Current.CancellationToken);
            Assert.Equal(2, _seedIdentityReadBarrier.EmptyInitialIdentityReadCount);
            _seedIdentityReadBarrier.ReleaseInitialIdentityReads();

            await WaitForChecklistSeedGateAsync(gateId);
            await _seedConcurrencyException.WaitForSecondSaveAsync(
                TestContext.Current.CancellationToken);
            await ReleaseChecklistSeedGateAsync(gateId, TestContext.Current.CancellationToken);

            var createdTask = await Task.WhenAny(firstTask, secondTask);
            var createdResponse = await createdTask;
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
            _seedConcurrencyException.ReleaseSecondSave();

            var responses = await Task.WhenAll(firstTask, secondTask);
            using var first = responses[0];
            using var second = responses[1];

            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, _seedConcurrencyException.InjectedExceptionCount);
            Assert.Equal(2, _seedIdentityReadBarrier.InitialIdentityReadCount);
            Assert.Equal(first.Headers.ETag?.ToString(), second.Headers.ETag?.ToString());

            using var response = await _client.GetAsync(
                $"/api/v1/trips/{trip.Id:D}/checklists?limit=10",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await response.Content.ReadFromJsonAsync<PageResponse<ChecklistSnapshot>>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(page);
            var onlyChecklist = Assert.Single(page.Items);
            Assert.Equal(checklistId, onlyChecklist.Id);
            Assert.Equal(items.Select(item => item.id), onlyChecklist.Items.Select(item => item.Id));
        }
        finally
        {
            _seedIdentityReadBarrier.ReleaseInitialIdentityReads();
            _seedConcurrencyException.ReleaseSecondSave();
            try
            {
                await ReleaseChecklistSeedGateAsync(gateId, CancellationToken.None);

                if (firstTask is { IsCompleted: false })
                {
                    using var completed = await firstTask;
                }

                if (secondTask is { IsCompleted: false })
                {
                    using var completed = await secondTask;
                }
            }
            finally
            {
                await RemoveChecklistSeedGateAsync(gateId, CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task seed_confirmation_holds_equipment_locks_until_checklist_commit()
    {
        var trip = await CreateTripAsync();
        await SetSeedContextAsync(trip, null, null, EquipmentCategory.BikeRack);
        using var previewResponse = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists/seed-preview?kind=0",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ChecklistSeedPreviewSnapshot>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(preview);

        var seedId = NewId();
        var gateId = NewId();
        var items = preview.Items.Select(item => new { id = NewId(), templateKey = item.TemplateKey }).ToArray();
        await CreateChecklistSeedGateAsync(gateId, seedId);
        Task<HttpResponseMessage>? confirmationTask = null;
        try
        {
            confirmationTask = SendJsonAsync(
                HttpMethod.Post,
                $"/api/v1/trips/{trip.Id:D}/checklists/seed",
                new { id = seedId, kind = 0, items },
                previewResponse.Headers.ETag?.ToString());
            await WaitForChecklistSeedGateAsync(gateId);
            var updateFailure = await Record.ExceptionAsync(
                () => UpdateBikeRackNotesAsync(trip.CamperId, "concurrent update", 500));
            Assert.Equal(1222, Assert.IsType<SqlException>(updateFailure).Number);

            using var created = await confirmationTask;
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal(1, await UpdateBikeRackNotesAsync(trip.CamperId, "after commit", -1));
        }
        finally
        {
            if (confirmationTask is { IsCompleted: false })
            {
                using var completed = await confirmationTask;
            }

            await RemoveChecklistSeedGateAsync(gateId, CancellationToken.None);
        }
    }

    [Fact]
    public async Task seed_confirmation_rejects_changed_context_and_rolls_back_identity_collisions()
    {
        var trip = await CreateTripAsync();
        using var firstPreviewResponse = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists/seed-preview?kind=0",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstPreviewResponse.StatusCode);
        var firstPreview = await firstPreviewResponse.Content.ReadFromJsonAsync<ChecklistSeedPreviewSnapshot>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(firstPreview);
        var proposedItems = firstPreview.Items.Select(item => new
        {
            id = NewId(),
            templateKey = item.TemplateKey,
        }).ToArray();
        var oldEtag = firstPreviewResponse.Headers.ETag?.ToString();
        Assert.NotNull(oldEtag);

        await SetSeedContextAsync(trip, null, null, EquipmentCategory.BikeRack);
        Assert.Equal(oldEtag, await ReadTripEtagAsync(trip.Id));
        using var changedEquipment = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists/seed",
            new { id = NewId(), kind = 0, items = proposedItems },
            oldEtag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, changedEquipment.StatusCode);

        await SetSeedContextAsync(
            trip,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 8));
        using var changedDates = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists/seed",
            new { id = NewId(), kind = 0, items = proposedItems },
            oldEtag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, changedDates.StatusCode);

        var currentEtag = await ReadTripEtagAsync(trip.Id);
        using var noPartialChecklist = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists?limit=10",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, noPartialChecklist.StatusCode);
        var emptyPage = await noPartialChecklist.Content.ReadFromJsonAsync<PageResponse<ChecklistSnapshot>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(emptyPage);
        Assert.Empty(emptyPage.Items);
        Assert.Equal(currentEtag, noPartialChecklist.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task seed_preview_and_confirmation_hide_a_trip_owned_by_another_user()
    {
        const string otherEmail = "other-seed-checklist-owner@example.test";
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
        var foreignTrip = await CreateTripAsync(otherClient);

        using var preview = await _client.GetAsync(
            $"/api/v1/trips/{foreignTrip.Id:D}/checklists/seed-preview?kind=0",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
        using var confirm = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{foreignTrip.Id:D}/checklists/seed",
            new
            {
                id = NewId(),
                kind = 0,
                items = new[] { new { id = NewId(), templateKey = "departure.windows" } },
            },
            foreignTrip.ETag);
        Assert.Equal(HttpStatusCode.NotFound, confirm.StatusCode);
    }

    [Fact]
    public async Task seed_confirmation_rolls_back_checklist_and_items_when_an_item_identity_collides()
    {
        var trip = await CreateTripAsync();
        var existing = await CreateChecklistAsync(trip, NewId(), "Manuale", 0);
        var reservedItemId = NewId();
        existing = await AddItemAsync(existing, reservedItemId, "Voce gia' esistente");
        using var previewResponse = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists/seed-preview?kind=0",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ChecklistSeedPreviewSnapshot>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(preview);
        var itemIdentities = preview.Items.Select((item, index) => new
        {
            id = index == 0 ? reservedItemId : NewId(),
            templateKey = item.TemplateKey,
        }).ToArray();
        var seedId = NewId();
        var etagBeforeConfirm = previewResponse.Headers.ETag?.ToString();
        Assert.NotNull(etagBeforeConfirm);

        using var conflict = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists/seed",
            new { id = seedId, kind = 0, items = itemIdentities },
            etagBeforeConfirm);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(etagBeforeConfirm, await ReadTripEtagAsync(trip.Id));
        using var absentSeed = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists/{seedId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, absentSeed.StatusCode);
        var existingAfter = await ReadChecklistAsync(existing);
        Assert.Collection(existingAfter.Items, item =>
        {
            Assert.Equal(reservedItemId, item.Id);
            Assert.Equal("Voce gia' esistente", item.Text);
        });

        using var listResponse = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists?limit=10",
            TestContext.Current.CancellationToken);
        var list = await listResponse.Content.ReadFromJsonAsync<PageResponse<ChecklistSnapshot>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        Assert.Equal(existing.Id, Assert.Single(list.Items).Id);
    }

    [Fact]
    public async Task owner_can_page_trip_checklists_with_the_trip_etag_without_mutating_the_trip()
    {
        var trip = await CreateTripAsync();
        var first = await CreateChecklistAsync(trip, NewId(), "Prima", 0);
        var second = await CreateChecklistAsync(trip, NewId(), "Seconda", 1, first.ETag);
        second = await AddItemAsync(second, NewId(), "Controllo uno");
        second = await AddItemAsync(second, NewId(), "Controllo due");
        var unrelatedTrip = await CreateTripAsync();
        await CreateChecklistAsync(unrelatedTrip, NewId(), "Altro viaggio", 2);
        var currentTripEtag = await ReadTripEtagAsync(trip.Id);
        Assert.Equal(second.ETag, currentTripEtag);

        using var completePageResponse = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists?limit=2",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, completePageResponse.StatusCode);
        var completePage = await completePageResponse.Content.ReadFromJsonAsync<PageResponse<ChecklistSnapshot>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(completePage);
        Assert.Equal(2, completePage.Items.Count);
        Assert.Null(completePage.NextCursor);
        var secondChecklistDetails = Assert.Single(
            completePage.Items,
            checklist => checklist.Id == second.Id);
        Assert.Collection(
            secondChecklistDetails.Items,
            item =>
            {
                Assert.Equal("Controllo uno", item.Text);
                Assert.Equal(1, item.SortOrder);
            },
            item =>
            {
                Assert.Equal("Controllo due", item.Text);
                Assert.Equal(2, item.SortOrder);
            });

        using var firstPageResponse = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists?limit=1",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
        Assert.Equal(currentTripEtag, firstPageResponse.Headers.ETag?.ToString());
        HttpEtagAssertions.StrongRowVersion(firstPageResponse.Headers.ETag?.ToString());
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<PageResponse<ChecklistSnapshot>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(firstPage);
        var firstChecklist = Assert.Single(firstPage.Items);
        Assert.Contains(firstChecklist.Id, new[] { first.Id, second.Id });
        Assert.Equal(trip.Id, firstChecklist.TripId);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.NextCursor));

        using var secondPageResponse = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists?limit=1&after="
                + Uri.EscapeDataString(firstPage.NextCursor!),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
        Assert.Equal(currentTripEtag, secondPageResponse.Headers.ETag?.ToString());
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<PageResponse<ChecklistSnapshot>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(secondPage);
        var secondChecklist = Assert.Single(secondPage.Items);
        Assert.NotEqual(firstChecklist.Id, secondChecklist.Id);
        Assert.Contains(secondChecklist.Id, new[] { first.Id, second.Id });
        Assert.Null(secondPage.NextCursor);

        Assert.Equal(currentTripEtag, await ReadTripEtagAsync(trip.Id));
    }

    [Fact]
    public async Task checklist_collection_hides_unowned_trips_and_rejects_invalid_pagination()
    {
        var trip = await CreateTripAsync();
        const string otherEmail = "other-checklist-list-owner@example.test";
        var otherOwnerId = Guid.NewGuid();
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
        var foreignTrip = await CreateTripAsync(otherClient);
        await CreateChecklistAsync(
            foreignTrip,
            NewId(),
            "Checklist privata",
            0,
            selectedClient: otherClient);

        using var missing = await _client.GetAsync(
            $"/api/v1/trips/{NewId():D}/checklists",
            TestContext.Current.CancellationToken);
        using var foreign = await _client.GetAsync(
            $"/api/v1/trips/{foreignTrip.Id:D}/checklists",
            TestContext.Current.CancellationToken);
        await AssertSameNotFound(missing, foreign);

        foreach (var path in new[]
        {
            $"/api/v1/trips/{trip.Id:D}/checklists?limit=0",
            $"/api/v1/trips/{trip.Id:D}/checklists?limit=201",
            $"/api/v1/trips/{trip.Id:D}/checklists?after=not-a-cursor",
        })
        {
            using var invalid = await _client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            using var body = JsonDocument.Parse(
                await invalid.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            if (path.Contains("after=", StringComparison.Ordinal))
            {
                Assert.Equal(
                    "https://roamly.dev/problems/invalid-cursor",
                    body.RootElement.GetProperty("type").GetString());
            }
            else
            {
                Assert.Contains(
                    "limit",
                    body.RootElement.GetProperty("errors").EnumerateObject()
                        .Select(error => error.Name),
                    StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task owner_can_complete_reorder_add_and_remove_items_with_the_trip_etag()
    {
        var trip = await CreateTripAsync();
        var checklist = await CreateChecklistAsync(trip, NewId(), "Prima della partenza", 0);
        var firstId = NewId();
        var secondId = NewId();
        var thirdId = NewId();
        checklist = await AddItemAsync(checklist, firstId, "Chiudere il gas");
        checklist = await AddItemAsync(checklist, secondId, "Controllare i cunei");

        using var completed = await SendJsonAsync(
            HttpMethod.Put,
            $"{checklist.Path}/items/{firstId:D}",
            new { text = "Chiudere il gas", isDone = true },
            checklist.ETag);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var completedEtag = AssertEtagChanged(checklist.ETag, completed);
        checklist = checklist with { ETag = completedEtag };

        using var invalidOrder = await SendJsonAsync(
            HttpMethod.Put,
            $"{checklist.Path}/items/order",
            new { itemIds = new[] { secondId, NewId() } },
            checklist.ETag);
        Assert.Equal(HttpStatusCode.BadRequest, invalidOrder.StatusCode);
        var afterInvalidOrder = await ReadChecklistAsync(checklist);
        Assert.Equal(checklist.ETag, afterInvalidOrder.ETag);
        Assert.Collection(
            afterInvalidOrder.Items,
            item =>
            {
                Assert.Equal(firstId, item.Id);
                Assert.Equal(1, item.SortOrder);
                Assert.True(item.IsDone);
            },
            item =>
            {
                Assert.Equal(secondId, item.Id);
                Assert.Equal(2, item.SortOrder);
                Assert.False(item.IsDone);
            });
        Assert.Equal(checklist.ETag, await ReadTripEtagAsync(trip.Id));

        using var reordered = await SendJsonAsync(
            HttpMethod.Put,
            $"{checklist.Path}/items/order",
            new { itemIds = new[] { secondId, firstId } },
            checklist.ETag);
        Assert.Equal(HttpStatusCode.OK, reordered.StatusCode);
        checklist = checklist with { ETag = AssertEtagChanged(checklist.ETag, reordered) };

        checklist = await AddItemAsync(checklist, thirdId, "Riporre il cavo");
        using var removed = await SendJsonAsync(
            HttpMethod.Delete,
            $"{checklist.Path}/items/{secondId:D}",
            new { },
            checklist.ETag);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        checklist = checklist with { ETag = AssertEtagChanged(checklist.ETag, removed) };

        var actual = await ReadChecklistAsync(checklist);
        Assert.Collection(
            actual.Items,
            item =>
            {
                Assert.Equal(firstId, item.Id);
                Assert.True(item.IsDone);
                Assert.Equal(1, item.SortOrder);
            },
            item =>
            {
                Assert.Equal(thirdId, item.Id);
                Assert.False(item.IsDone);
                Assert.Equal(2, item.SortOrder);
            });
    }

    [Fact]
    public async Task checklist_update_and_delete_require_the_trip_precondition()
    {
        var trip = await CreateTripAsync();
        var checklist = await CreateChecklistAsync(trip, NewId(), "Prima", 0);

        using var updated = await SendJsonAsync(
            HttpMethod.Put,
            checklist.Path,
            new { name = "Dopo", kind = 1 },
            checklist.ETag);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        checklist = checklist with { ETag = AssertEtagChanged(checklist.ETag, updated) };
        using var missing = await SendJsonAsync(HttpMethod.Delete, checklist.Path, new { }, null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        using var malformed = await SendJsonAsync(HttpMethod.Delete, checklist.Path, new { }, "etag-invalid");
        Assert.Equal(HttpStatusCode.PreconditionFailed, malformed.StatusCode);

        using var deleted = await SendJsonAsync(HttpMethod.Delete, checklist.Path, new { }, checklist.ETag);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.NotEqual(checklist.ETag, deleted.Headers.ETag?.ToString());
        using var read = await _client.GetAsync(checklist.Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task missing_or_stale_trip_etag_rejects_item_changes_without_mutation()
    {
        var trip = await CreateTripAsync();
        var checklist = await CreateChecklistAsync(trip, NewId(), "Prima", 0);
        var itemId = NewId();

        using var missing = await SendJsonAsync(
            HttpMethod.Post,
            $"{checklist.Path}/items",
            new { id = itemId, text = "Controllare il freno" },
            null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        using var malformed = await SendJsonAsync(
            HttpMethod.Post,
            $"{checklist.Path}/items",
            new { id = itemId, text = "Controllare il freno" },
            "etag-invalid");
        Assert.Equal(HttpStatusCode.PreconditionFailed, malformed.StatusCode);

        checklist = await AddItemAsync(checklist, itemId, "Controllare il freno");
        using var stale = await SendJsonAsync(
            HttpMethod.Put,
            $"{checklist.Path}/items/{itemId:D}",
            new { text = "Controllare il freno", isDone = true },
            trip.ETag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        var actual = await ReadChecklistAsync(checklist);
        Assert.False(Assert.Single(actual.Items).IsDone);
        Assert.Equal(checklist.ETag, actual.ETag);
    }

    [Fact]
    public async Task reorder_and_item_delete_require_a_current_trip_etag_without_mutating_items()
    {
        var trip = await CreateTripAsync();
        var checklist = await CreateChecklistAsync(trip, NewId(), "Prima", 0);
        var firstId = NewId();
        var secondId = NewId();
        checklist = await AddItemAsync(checklist, firstId, "Primo controllo");
        checklist = await AddItemAsync(checklist, secondId, "Secondo controllo");
        var original = await ReadChecklistAsync(checklist);

        var requests = new[]
        {
            (
                Method: HttpMethod.Put,
                Path: $"{checklist.Path}/items/order",
                Body: (object)new { itemIds = new[] { secondId, firstId } }),
            (
                Method: HttpMethod.Delete,
                Path: $"{checklist.Path}/items/{secondId:D}",
                Body: (object)new { }),
        };

        foreach (var request in requests)
        {
            using var missing = await SendJsonAsync(
                request.Method,
                request.Path,
                request.Body,
                null);
            Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);

            using var malformed = await SendJsonAsync(
                request.Method,
                request.Path,
                request.Body,
                "etag-invalid");
            Assert.Equal(HttpStatusCode.PreconditionFailed, malformed.StatusCode);

            using var stale = await SendJsonAsync(
                request.Method,
                request.Path,
                request.Body,
                trip.ETag);
            Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);

            var afterRejectedRequests = await ReadChecklistAsync(checklist);
            Assert.Equal(original.ETag, afterRejectedRequests.ETag);
            Assert.Equal(original.Items, afterRejectedRequests.Items);
        }
    }

    [Fact]
    public async Task checklist_update_rejects_a_trip_etag_staled_by_a_real_sql_server_update()
    {
        var trip = await CreateTripAsync();
        var checklist = await CreateChecklistAsync(trip, NewId(), "Prima", 0);
        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .Options;
        await using (var db = new RoamlyDbContext(options, new TestCurrentUser(_ownerId)))
        {
            var storedTrip = await db.Trips.SingleAsync(
                candidate => candidate.OwnerId == _ownerId && candidate.Id == trip.Id,
                TestContext.Current.CancellationToken);
            storedTrip.Name = "Modificato fuori dalla richiesta";
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var stale = await SendJsonAsync(
            HttpMethod.Put,
            checklist.Path,
            new { name = "Non applicare", kind = 1 },
            checklist.ETag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        var actual = await ReadChecklistAsync(checklist);
        Assert.Equal("Prima", actual.Name);
        Assert.NotEqual(checklist.ETag, actual.ETag);
    }

    [Fact]
    public async Task missing_and_foreign_trips_checklists_and_items_are_uniformly_not_found()
    {
        var trip = await CreateTripAsync();
        var checklist = await CreateChecklistAsync(trip, NewId(), "Prima", 0);
        var otherChecklist = await CreateChecklistAsync(trip, NewId(), "Dopo", 1, checklist.ETag);
        checklist = checklist with { ETag = otherChecklist.ETag };
        var itemId = NewId();
        checklist = await AddItemAsync(checklist, itemId, "Voce privata");

        using var missingTrip = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{NewId():D}/checklists",
            new { id = NewId(), name = "Non esiste", kind = 0 },
            null);
        Assert.Equal(HttpStatusCode.NotFound, missingTrip.StatusCode);

        var otherEmail = "other-checklist-owner@example.test";
        await CreateUserAsync(Guid.NewGuid(), otherEmail);
        using var otherClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        using var login = await otherClient.LoginAsync(otherEmail, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var foreignTrip = await CreateTripAsync(otherClient);
        var foreignChecklist = await CreateChecklistAsync(
            foreignTrip,
            NewId(),
            "Checklist proprietario diverso",
            0,
            selectedClient: otherClient);
        var foreignItemId = NewId();
        foreignChecklist = await AddItemAsync(
            foreignChecklist,
            foreignItemId,
            "Voce di un altro proprietario",
            otherClient);

        using var foreignWrite = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists",
            new { id = NewId(), name = "Non autorizzata", kind = 0 },
            null,
            otherClient);
        Assert.Equal(HttpStatusCode.NotFound, foreignWrite.StatusCode);

        await AssertSameNotFound(
            await _client.GetAsync(
                $"/api/v1/trips/{NewId():D}",
                TestContext.Current.CancellationToken),
            await _client.GetAsync(
                $"/api/v1/trips/{foreignTrip.Id:D}",
                TestContext.Current.CancellationToken));

        using var foreignChecklistRead = await otherClient.GetAsync(
            checklist.Path,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignChecklistRead.StatusCode);
        await AssertSameNotFound(await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists/{NewId():D}",
            TestContext.Current.CancellationToken), foreignChecklistRead);

        using var sameOwnerDifferentChecklistItem = await SendJsonAsync(
            HttpMethod.Put,
            $"{otherChecklist.Path}/items/{itemId:D}",
            new { text = "Modifica non consentita", isDone = true },
            otherChecklist.ETag);
        Assert.Equal(HttpStatusCode.NotFound, sameOwnerDifferentChecklistItem.StatusCode);

        var foreignItemPath = $"{foreignChecklist.Path}/items/{foreignItemId:D}";
        using var foreignItemRead = await _client.GetAsync(
            foreignItemPath,
            TestContext.Current.CancellationToken);
        using var unknownItemRead = await _client.GetAsync(
            $"{checklist.Path}/items/{NewId():D}",
            TestContext.Current.CancellationToken);
        await AssertSameNotFound(unknownItemRead, foreignItemRead);

        using var foreignOwnerPut = await SendJsonAsync(
            HttpMethod.Put,
            foreignItemPath,
            new { text = "Modifica non consentita", isDone = true },
            foreignChecklist.ETag);
        using var unknownItemPut = await SendJsonAsync(
            HttpMethod.Put,
            $"{checklist.Path}/items/{NewId():D}",
            new { text = "ID sconosciuto", isDone = true },
            checklist.ETag);
        await AssertSameNotFound(unknownItemPut, foreignOwnerPut);

        using var foreignOwnerDelete = await SendJsonAsync(
            HttpMethod.Delete,
            foreignItemPath,
            new { },
            foreignChecklist.ETag);
        using var unknownItemDelete = await SendJsonAsync(
            HttpMethod.Delete,
            $"{checklist.Path}/items/{NewId():D}",
            new { },
            checklist.ETag);
        await AssertSameNotFound(unknownItemDelete, foreignOwnerDelete);

        Assert.Equal("Voce privata", Assert.Single((await ReadChecklistAsync(checklist)).Items).Text);
        var foreignAfterRejectedRequests = await ReadChecklistAsync(foreignChecklist, otherClient);
        Assert.Equal("Checklist proprietario diverso", foreignAfterRejectedRequests.Name);
        var foreignItem = Assert.Single(foreignAfterRejectedRequests.Items);
        Assert.Equal(foreignItemId, foreignItem.Id);
        Assert.Equal("Voce di un altro proprietario", foreignItem.Text);
        Assert.False(foreignItem.IsDone);
    }

    [Fact]
    public async Task concurrent_checklist_mutations_with_one_trip_etag_apply_exactly_one_child_change()
    {
        var trip = await CreateTripAsync();
        var checklistToUpdate = await CreateChecklistAsync(trip, NewId(), "Da aggiornare", 0);
        var checklistToDelete = await CreateChecklistAsync(
            trip,
            NewId(),
            "Da eliminare",
            1,
            checklistToUpdate.ETag);
        var sharedTripEtag = checklistToDelete.ETag;

        using var updateRequest = await CreateJsonRequestAsync(
            HttpMethod.Put,
            checklistToUpdate.Path,
            new { name = "Aggiornata", kind = 0 },
            sharedTripEtag);
        using var deleteRequest = await CreateJsonRequestAsync(
            HttpMethod.Delete,
            checklistToDelete.Path,
            new { },
            sharedTripEtag);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var updateTask = SendAfterSignalAsync(_client, updateRequest, start.Task, timeout.Token);
        var deleteTask = SendAfterSignalAsync(_client, deleteRequest, start.Task, timeout.Token);
        start.SetResult();

        var responses = await Task.WhenAll(updateTask, deleteTask).WaitAsync(timeout.Token);
        using var updateResponse = responses[0];
        using var deleteResponse = responses[1];

        Assert.True(
            (updateResponse.StatusCode == HttpStatusCode.OK
                && deleteResponse.StatusCode == HttpStatusCode.PreconditionFailed)
            || (updateResponse.StatusCode == HttpStatusCode.PreconditionFailed
                && deleteResponse.StatusCode == HttpStatusCode.NoContent),
            $"Expected one successful mutation and one stale precondition, got "
                + $"{(int)updateResponse.StatusCode} and {(int)deleteResponse.StatusCode}.");
        AssertEtagChanged(
            sharedTripEtag,
            updateResponse.StatusCode == HttpStatusCode.OK ? updateResponse : deleteResponse);

        var updatedChecklist = await ReadChecklistAsync(checklistToUpdate);
        Assert.Equal(
            updateResponse.StatusCode == HttpStatusCode.OK ? "Aggiornata" : "Da aggiornare",
            updatedChecklist.Name);
        Assert.NotEqual(sharedTripEtag, updatedChecklist.ETag);

        using var deletedChecklistRead = await _client.GetAsync(
            checklistToDelete.Path,
            TestContext.Current.CancellationToken);
        if (deleteResponse.StatusCode == HttpStatusCode.NoContent)
        {
            Assert.Equal(HttpStatusCode.NotFound, deletedChecklistRead.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, deletedChecklistRead.StatusCode);
            var remaining = await deletedChecklistRead.Content.ReadFromJsonAsync<ChecklistSnapshot>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(remaining);
            Assert.Equal("Da eliminare", remaining.Name);
            Assert.Empty(remaining.Items);
        }
    }

    [Fact]
    public async Task invalid_checklist_fields_are_rejected_without_advancing_the_trip_etag()
    {
        var trip = await CreateTripAsync();
        var checklistId = NewId();
        using var invalidName = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists",
            new { id = checklistId, name = "   ", kind = 0 },
            trip.ETag);
        Assert.Equal(HttpStatusCode.BadRequest, invalidName.StatusCode);
        using var invalidKind = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists",
            new { id = checklistId, name = "Valida", kind = 9 },
            trip.ETag);
        Assert.Equal(HttpStatusCode.BadRequest, invalidKind.StatusCode);

        var currentTripEtag = await ReadTripEtagAsync(trip.Id);
        Assert.Equal(trip.ETag, currentTripEtag);
        using var absent = await _client.GetAsync(
            $"/api/v1/trips/{trip.Id:D}/checklists/{checklistId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);

        var checklist = await CreateChecklistAsync(trip, NewId(), "Valida", 0);
        using var invalidItem = await SendJsonAsync(
            HttpMethod.Post,
            $"{checklist.Path}/items",
            new { id = NewId(), text = "   " },
            checklist.ETag);
        Assert.Equal(HttpStatusCode.BadRequest, invalidItem.StatusCode);
        Assert.Equal(checklist.ETag, await ReadTripEtagAsync(trip.Id));
        Assert.Empty((await ReadChecklistAsync(checklist)).Items);
    }

    [Fact]
    public async Task create_posts_replay_by_identity_and_reject_different_content()
    {
        var trip = await CreateTripAsync();
        var checklistId = NewId();
        var checklistPath = $"/api/v1/trips/{trip.Id:D}/checklists";
        using var created = await SendJsonAsync(
            HttpMethod.Post,
            checklistPath,
            new { id = checklistId, name = "Prima", kind = 0 },
            trip.ETag);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var currentEtag = created.Headers.ETag?.ToString();

        using var replay = await SendJsonAsync(
            HttpMethod.Post,
            checklistPath,
            new { id = checklistId, name = " Prima ", kind = 0 },
            trip.ETag);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(currentEtag, replay.Headers.ETag?.ToString());
        using var conflict = await SendJsonAsync(
            HttpMethod.Post,
            checklistPath,
            new { id = checklistId, name = "Diversa", kind = 0 },
            trip.ETag);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(currentEtag, await ReadTripEtagAsync(trip.Id));

        var itemId = NewId();
        var checklistPathWithId = $"{checklistPath}/{checklistId:D}";
        using var itemCreated = await SendJsonAsync(
            HttpMethod.Post,
            $"{checklistPathWithId}/items",
            new { id = itemId, text = "Chiudere il gas" },
            currentEtag);
        Assert.Equal(HttpStatusCode.Created, itemCreated.StatusCode);
        var itemEtag = itemCreated.Headers.ETag?.ToString();
        using var itemReplay = await SendJsonAsync(
            HttpMethod.Post,
            $"{checklistPathWithId}/items",
            new { id = itemId, text = " Chiudere il gas " },
            currentEtag);
        Assert.Equal(HttpStatusCode.OK, itemReplay.StatusCode);
        Assert.Equal(itemEtag, itemReplay.Headers.ETag?.ToString());
        using var itemConflict = await SendJsonAsync(
            HttpMethod.Post,
            $"{checklistPathWithId}/items",
            new { id = itemId, text = "Lasciare il gas aperto" },
            currentEtag);
        Assert.Equal(HttpStatusCode.Conflict, itemConflict.StatusCode);
    }

    private async Task<TripHandle> CreateTripAsync(RoamlyClient? selectedClient = null)
    {
        var client = selectedClient ?? _client;
        var camperId = NewId();
        using var camper = await client.PostJsonAsync("/api/v1/campers", new
        {
            id = camperId,
            name = "Il Grigio",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, camper.StatusCode);

        var tripId = NewId();
        using var created = await client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            new { id = tripId, name = "Viaggio di prova", stops = new[] { new { name = "Bologna" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var response = await client.GetAsync(
            $"/api/v1/trips/{tripId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var etag = response.Headers.ETag?.ToString();
        Assert.NotNull(etag);
        return new TripHandle(tripId, camperId, etag);
    }

    private async Task SetSeedContextAsync(
        TripHandle trip,
        DateOnly? plannedStartOnUtc,
        DateOnly? plannedEndOnUtc,
        params EquipmentCategory[] categories)
    {
        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .Options;
        await using var db = new RoamlyDbContext(options, new TestCurrentUser(_ownerId));
        var storedTrip = await db.Trips.SingleAsync(
            candidate => candidate.OwnerId == _ownerId && candidate.Id == trip.Id,
            TestContext.Current.CancellationToken);
        storedTrip.PlannedStartOnUtc = plannedStartOnUtc;
        storedTrip.PlannedEndOnUtc = plannedEndOnUtc;
        var now = ClockNow.UtcDateTime;
        db.Equipment.AddRange(categories.Select(category => new Equipment
        {
            Id = NewId(),
            OwnerId = _ownerId,
            CamperId = trip.CamperId,
            Name = category.ToString(),
            Category = category,
            CreatedAtUtc = now,
        }));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task CreateChecklistSeedGateAsync(
        Guid gateId,
        Guid checklistId,
        bool holdUntilReleased = false)
    {
        var gateTable = $"ChecklistSeedGate_{gateId:N}";
        var trigger = $"ChecklistSeedGateTrigger_{gateId:N}";
        await using var connection = new SqlConnection(_database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using (var createTable = connection.CreateCommand())
        {
            createTable.CommandText = $"""
                CREATE TABLE dbo.[{gateTable}]
                    (Id int NOT NULL PRIMARY KEY, IsEntered bit NOT NULL,
                     HoldUntilReleased bit NOT NULL, IsReleased bit NOT NULL);
                INSERT INTO dbo.[{gateTable}] (Id, IsEntered, HoldUntilReleased, IsReleased)
                VALUES (1, 0, {(holdUntilReleased ? 1 : 0)}, 0),
                       (2, 0, {(holdUntilReleased ? 1 : 0)}, 0);
                """;
            await createTable.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var createTrigger = connection.CreateCommand();
        createTrigger.CommandText = $"""
            CREATE TRIGGER dbo.[{trigger}] ON dbo.ChecklistItems
            AFTER INSERT
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted WHERE ChecklistId = '{checklistId:D}')
                BEGIN
                    UPDATE dbo.[{gateTable}] SET IsEntered = 1 WHERE Id = 1;
                    IF EXISTS
                    (
                        SELECT 1 FROM dbo.[{gateTable}]
                        WHERE Id = 1 AND HoldUntilReleased = 1
                    )
                    BEGIN
                        WHILE EXISTS
                        (
                            SELECT 1 FROM dbo.[{gateTable}] WITH (NOLOCK)
                            WHERE Id = 2 AND IsReleased = 0
                        )
                        BEGIN
                            WAITFOR DELAY '00:00:00.050';
                        END
                    END
                    ELSE
                    BEGIN
                        WAITFOR DELAY '00:00:04';
                    END
                END
            END;
            """;
        await createTrigger.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task WaitForChecklistSeedGateAsync(Guid gateId)
    {
        var gateTable = $"ChecklistSeedGate_{gateId:N}";
        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        await using var connection = new SqlConnection(_database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        while (System.Diagnostics.Stopwatch.GetElapsedTime(startedAt) < TimeSpan.FromSeconds(10))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT IsEntered FROM dbo.[{gateTable}] WITH (NOLOCK) WHERE Id = 1";
            if (Equals(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), true))
            {
                return;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("La conferma seed non ha raggiunto il gate SQL atteso.");
    }

    private async Task ReleaseChecklistSeedGateAsync(Guid gateId, CancellationToken cancellationToken)
    {
        var gateTable = $"ChecklistSeedGate_{gateId:N}";
        await using var connection = new SqlConnection(_database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE dbo.[{gateTable}] SET IsReleased = 1 WHERE Id = 2";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> UpdateBikeRackNotesAsync(Guid camperId, string notes, int lockTimeoutMs)
    {
        await using var connection = new SqlConnection(_database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SET LOCK_TIMEOUT {lockTimeoutMs};
            UPDATE dbo.Equipment
            SET Notes = @notes
            WHERE OwnerId = @ownerId AND CamperId = @camperId AND Name = N'BikeRack';
            """;
        command.Parameters.AddWithValue("@notes", notes);
        command.Parameters.AddWithValue("@ownerId", _ownerId);
        command.Parameters.AddWithValue("@camperId", camperId);
        return await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task RemoveChecklistSeedGateAsync(Guid gateId, CancellationToken cancellationToken)
    {
        var gateTable = $"ChecklistSeedGate_{gateId:N}";
        var trigger = $"ChecklistSeedGateTrigger_{gateId:N}";
        await using var connection = new SqlConnection(_database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            DROP TRIGGER IF EXISTS dbo.[{trigger}];
            DROP TABLE IF EXISTS dbo.[{gateTable}];
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<ChecklistHandle> CreateChecklistAsync(
        TripHandle trip,
        Guid id,
        string name,
        int kind,
        string? etag = null,
        RoamlyClient? selectedClient = null)
    {
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            $"/api/v1/trips/{trip.Id:D}/checklists",
            new { id, name, kind },
            etag ?? trip.ETag,
            selectedClient);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var currentEtag = response.Headers.ETag?.ToString();
        Assert.NotNull(currentEtag);
        Assert.NotEqual(etag ?? trip.ETag, currentEtag);
        var path = $"/api/v1/trips/{trip.Id:D}/checklists/{id:D}";
        return new ChecklistHandle(id, path, currentEtag);
    }

    private async Task<ChecklistHandle> AddItemAsync(
        ChecklistHandle checklist,
        Guid itemId,
        string text,
        RoamlyClient? selectedClient = null)
    {
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            $"{checklist.Path}/items",
            new { id = itemId, text },
            checklist.ETag,
            selectedClient);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var currentEtag = response.Headers.ETag?.ToString();
        Assert.NotNull(currentEtag);
        Assert.NotEqual(checklist.ETag, currentEtag);
        return checklist with { ETag = currentEtag };
    }

    private async Task<ChecklistSnapshot> ReadChecklistAsync(
        ChecklistHandle checklist,
        RoamlyClient? selectedClient = null)
    {
        var client = selectedClient ?? _client;
        using var response = await client.GetAsync(checklist.Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ChecklistSnapshot>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        return result with { ETag = response.Headers.ETag?.ToString() };
    }

    private async Task<string?> ReadTripEtagAsync(Guid tripId)
    {
        using var response = await _client.GetAsync(
            $"/api/v1/trips/{tripId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.ETag?.ToString();
    }

    private static string AssertEtagChanged(string oldEtag, HttpResponseMessage response)
    {
        var newEtag = response.Headers.ETag?.ToString();
        Assert.NotNull(newEtag);
        Assert.NotEqual(oldEtag, newEtag);
        return newEtag;
    }

    private static async Task AssertSameNotFound(
        HttpResponseMessage missing,
        HttpResponseMessage foreign)
    {
        using (missing)
        using (foreign)
        {
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            using var missingBody = JsonDocument.Parse(await missing.Content.ReadAsStringAsync(
                TestContext.Current.CancellationToken));
            using var foreignBody = JsonDocument.Parse(await foreign.Content.ReadAsStringAsync(
                TestContext.Current.CancellationToken));
            var missingProperties = missingBody.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var foreignProperties = foreignBody.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(missingProperties, foreignProperties);
            Assert.Equal(missingBody.RootElement.GetProperty("type").GetString(),
                foreignBody.RootElement.GetProperty("type").GetString());
            Assert.Equal(missingBody.RootElement.GetProperty("title").GetString(),
                foreignBody.RootElement.GetProperty("title").GetString());
            Assert.Equal(
                missingBody.RootElement.GetProperty("status").GetInt32(),
                foreignBody.RootElement.GetProperty("status").GetInt32());
        }
    }

    private async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string path,
        object body,
        string? etag,
        RoamlyClient? selectedClient = null)
    {
        var client = selectedClient ?? _client;
        using var request = await CreateJsonRequestAsync(method, path, body, etag, client);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpRequestMessage> CreateJsonRequestAsync(
        HttpMethod method,
        string path,
        object body,
        string? etag,
        RoamlyClient? selectedClient = null)
    {
        var client = selectedClient ?? _client;
        using var csrf = await client.GetAsync(
            "/api/v1/csrf-token",
            TestContext.Current.CancellationToken);
        var token = await csrf.Content.ReadFromJsonAsync<CsrfTokenResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(token);

        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add("X-XSRF-TOKEN", token.Token);
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return request;
    }

    private static async Task<HttpResponseMessage> SendAfterSignalAsync(
        RoamlyClient client,
        HttpRequestMessage request,
        Task start,
        CancellationToken cancellationToken)
    {
        await start.WaitAsync(cancellationToken);
        return await client.SendAsync(request, cancellationToken);
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

    private static Guid NewId() => new SequentialGuidGenerator(TimeProvider.System).NewId();

    private sealed record TripHandle(Guid Id, Guid CamperId, string ETag);

    private sealed record ChecklistHandle(Guid Id, string Path, string ETag);

    private sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);

    private sealed record CsrfTokenResponse(string Token);

    private sealed record ChecklistSnapshot(
        Guid Id,
        Guid TripId,
        string Name,
        int Kind,
        IReadOnlyList<ChecklistItemSnapshot> Items,
        string? ETag = null);

    private sealed record ChecklistItemSnapshot(Guid Id, string Text, bool IsDone, int SortOrder);

    private sealed record ChecklistSeedPreviewSnapshot(
        int Kind,
        string Name,
        IReadOnlyList<ChecklistSeedPreviewItemSnapshot> Items);

    private sealed record ChecklistSeedPreviewItemSnapshot(string TemplateKey, string Text);

    private sealed class ChecklistSeedIdentityReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _bothInitialIdentityReads =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseInitialIdentityReads =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _bothChecklistInsertAttempts =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Guid _checklistId;
        private int _armed;
        private int _identityReadCount;
        private int _emptyIdentityReadCount;

        public int InitialIdentityReadCount => Math.Min(Volatile.Read(ref _identityReadCount), 2);
        public int EmptyInitialIdentityReadCount => Volatile.Read(ref _emptyIdentityReadCount);

        public void Arm(Guid checklistId)
        {
            _checklistId = checklistId;
            if (Interlocked.CompareExchange(ref _armed, 1, 0) != 0)
            {
                throw new InvalidOperationException("La barriera delle letture checklist e' gia' armata.");
            }
        }

        public Task WaitForBothInitialIdentityReadsAsync(CancellationToken cancellationToken) =>
            _bothInitialIdentityReads.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        public void ReleaseInitialIdentityReads() => _releaseInitialIdentityReads.TrySetResult();

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (IsArmed && IsInitialChecklistIdentityRead(command))
            {
                var sequence = Interlocked.Increment(ref _identityReadCount);
                if (sequence <= 2)
                {
                    if (!result.HasRows)
                    {
                        Interlocked.Increment(ref _emptyIdentityReadCount);
                    }

                    if (sequence == 2)
                    {
                        _bothInitialIdentityReads.TrySetResult();
                    }

                    await _releaseInitialIdentityReads.Task.WaitAsync(cancellationToken);
                }
            }

            return result;
        }

        private bool IsArmed => Volatile.Read(ref _armed) == 1;

        private bool IsInitialChecklistIdentityRead(DbCommand command) =>
            command.CommandText.Contains("FROM [Checklists]", StringComparison.OrdinalIgnoreCase)
            && command.CommandText.Contains("[OwnerId]", StringComparison.OrdinalIgnoreCase)
            && command.CommandText.Contains("[Id]", StringComparison.OrdinalIgnoreCase)
            && HasChecklistIdParameter(command);

        private bool HasChecklistIdParameter(DbCommand command) =>
            command.Parameters
                .Cast<DbParameter>()
                .Any(parameter => parameter.Value is Guid value && value == _checklistId);
    }

    private sealed class ChecklistSeedConcurrencyExceptionInterceptor : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _secondSaveStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseSecondSave =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _armed;
        private int _seedSaveCount;
        private int _injectedExceptionCount;

        public int InjectedExceptionCount => Volatile.Read(ref _injectedExceptionCount);

        public void Arm()
        {
            if (Interlocked.CompareExchange(ref _armed, 1, 0) != 0)
            {
                throw new InvalidOperationException("L'interceptor di concorrenza seed e' gia' armato.");
            }
        }

        public Task WaitForSecondSaveAsync(CancellationToken cancellationToken) =>
            _secondSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        public void ReleaseSecondSave() => _releaseSecondSave.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _armed) != 1
                || eventData.Context is not RoamlyDbContext context
                || !context.ChangeTracker.Entries<Checklist>().Any())
            {
                return result;
            }

            if (Interlocked.Increment(ref _seedSaveCount) != 2)
            {
                return result;
            }

            _secondSaveStarted.TrySetResult();
            await _releaseSecondSave.Task.WaitAsync(cancellationToken);
            Interlocked.Increment(ref _injectedExceptionCount);
            throw new DbUpdateConcurrencyException("Concorrenza seed simulata dal test.");
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
        public bool IsAuthenticated => true;
    }
}
