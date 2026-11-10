using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Api.Common.Security;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Places;

[Collection(DatabaseCollection.Name)]
public sealed class SavedPlacesApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
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

        const string email = "saved-place-owner@example.test";
        await CreateUserAsync(_ownerId, email);
        using var login = await _client.LoginAsync(
            email,
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
    public async Task Link_coordinates_are_used_for_create_replay_and_update_but_manual_position_wins()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = NewId();
        const string url = "https://www.google.com/maps/place/Bunker/@42.2370291,12.5091909,805m/data=!3d42.2370252!4d12.5117658";
        var input = new { id, name = "Bunker Soratte", category = 8, googleMapsUrl = url };
        using var created = await _client.PostJsonAsync("/api/v1/saved-places", input, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var place = await created.Content.ReadFromJsonAsync<SavedPlaceResponse>(ct);
        Assert.Equal(42.2370252m, place!.Position?.Latitude);
        Assert.Equal(12.5117658m, place.Position?.Longitude);
        Assert.Equal(url, place.GoogleMapsUrl);
        using var replay = await _client.PostJsonAsync("/api/v1/saved-places", input, ct);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var updated = await _client.SendUnsafeWithCsrfAsync(
            HttpMethod.Put, $"/api/v1/saved-places/{id}", new
            {
                name = "Bunker", category = 8, googleMapsUrl = "https://maps.google.com/?q=1,2",
            }, ETagOf(created), ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(1m, (await updated.Content.ReadFromJsonAsync<SavedPlaceResponse>(ct))!.Position?.Latitude);
        using var manual = await _client.SendUnsafeWithCsrfAsync(
            HttpMethod.Put, $"/api/v1/saved-places/{id}", new
            {
                name = "Bunker", category = 8, googleMapsUrl = url,
                position = new { latitude = 9m, longitude = 8m },
            }, ETagOf(updated), ct);
        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);
        Assert.Equal(9m, (await manual.Content.ReadFromJsonAsync<SavedPlaceResponse>(ct))!.Position?.Latitude);
        using var manualCreate = await _client.PostJsonAsync("/api/v1/saved-places", new
        {
            id = NewId(), name = "Posizione manuale", category = 8, googleMapsUrl = url,
            position = new { latitude = 7m, longitude = 6m },
        }, ct);
        Assert.Equal(HttpStatusCode.Created, manualCreate.StatusCode);
        using var manualRead = await _client.GetAsync(manualCreate.Headers.Location!.OriginalString, ct);
        Assert.Equal(7m, (await manualRead.Content.ReadFromJsonAsync<SavedPlaceResponse>(ct))!.Position?.Latitude);
    }

    [Fact]
    public async Task Historical_null_position_is_derived_on_reads_without_mutating_the_resource()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = NewId();
        await using (var db = CreateOwnerContext())
        {
            db.SavedPlaces.Add(new SavedPlace
            {
                Id = id, OwnerId = _ownerId, Name = "Bunker Soratte", Category = SavedPlaceCategory.PointOfInterest,
                GoogleMapsUrl = "https://www.google.com/maps/place/Bunker/@42.2370291,12.5091909,805m/data=!3d42.2370252!4d12.5117658",
                CreatedAtUtc = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            });
            db.SavedPlaces.Add(new SavedPlace
            {
                Id = NewId(), OwnerId = _ownerId, Name = "Link breve", Category = SavedPlaceCategory.City,
                GoogleMapsUrl = "https://maps.app.goo.gl/example",
                CreatedAtUtc = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            });
            db.SavedPlaces.Add(new SavedPlace
            {
                Id = NewId(), OwnerId = _ownerId, Name = "Senza posizione", Category = SavedPlaceCategory.City,
                CreatedAtUtc = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            });
            await db.SaveChangesAsync(ct);
        }
        using var read = await _client.GetAsync($"/api/v1/saved-places/{id}", ct);
        Assert.Equal(42.2370252m, (await read.Content.ReadFromJsonAsync<SavedPlaceResponse>(ct))!.Position?.Latitude);
        using var listed = await _client.GetAsync("/api/v1/saved-places", ct);
        var page = await listed.Content.ReadFromJsonAsync<SavedPlacePage>(ct);
        Assert.Equal(3, page!.Items.Count);
        Assert.Equal(12.5117658m, Assert.Single(page.Items, p => p.Id == id).Position?.Longitude);
        Assert.All(page.Items.Where(p => p.Id != id), place => Assert.Null(place.Position));
        await using var verify = CreateOwnerContext();
        var stored = await verify.SavedPlaces.AsNoTracking().SingleAsync(p => p.OwnerId == _ownerId && p.Id == id, ct);
        Assert.Null(stored.Position);
        Assert.Equal(ETagOf(read), Roamly.Api.Common.Http.RowVersionEtag.Format(stored.RowVersion));
    }

    [Fact]
    public async Task traveller_can_create_a_saved_place_and_find_its_manual_details_in_the_list()
    {
        var id = NewId();
        using var created = await _client.PostJsonAsync(
            "/api/v1/saved-places",
            new
            {
                id,
                name = "Museo Egizio",
                category = 11,
                position = new { latitude = 45.0694m, longitude = 7.6869m },
                address = "Via Accademia delle Scienze, Torino",
                notes = "Prenotare i biglietti.",
                googleMapsUrl = "https://maps.google.com/?q=Museo+Egizio",
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/v1/saved-places/{id:D}", created.Headers.Location?.OriginalString);

        using var listed = await _client.GetAsync(
            "/api/v1/saved-places",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        var page = await listed.Content.ReadFromJsonAsync<SavedPlacePage>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(page);
        var place = Assert.Single(page.Items);
        Assert.Equal(id, place.Id);
        Assert.Equal("Museo Egizio", place.Name);
        Assert.Equal(11, place.Category);
        Assert.Equal(45.0694m, place.Position?.Latitude);
        Assert.Equal(7.6869m, place.Position?.Longitude);
        Assert.Equal("Via Accademia delle Scienze, Torino", place.Address);
        Assert.Equal("Prenotare i biglietti.", place.Notes);
        Assert.Equal("https://maps.google.com/?q=Museo+Egizio", place.GoogleMapsUrl);
        Assert.Null(page.NextCursor);

        using var read = await _client.GetAsync(
            created.Headers.Location!.OriginalString,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var details = await read.Content.ReadFromJsonAsync<SavedPlaceResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(details);
        Assert.Equal(id, details.Id);
    }

    [Fact]
    public async Task traveller_only_lists_and_reads_places_owned_by_the_authenticated_user()
    {
        var ownId = NewId();
        using var ownPlace = await CreatePlaceAsync(_client, ownId, "Luogo personale");
        Assert.Equal(HttpStatusCode.Created, ownPlace.StatusCode);

        var otherOwnerId = Guid.NewGuid();
        const string otherEmail = "other-saved-place-owner@example.test";
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

        var otherId = NewId();
        using var otherPlace = await CreatePlaceAsync(otherClient, otherId, "Luogo altrui");
        Assert.Equal(HttpStatusCode.Created, otherPlace.StatusCode);

        using var ownList = await _client.GetAsync(
            "/api/v1/saved-places",
            TestContext.Current.CancellationToken);
        var ownPage = await ownList.Content.ReadFromJsonAsync<SavedPlacePage>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(ownPage);
        Assert.Equal(ownId, Assert.Single(ownPage.Items).Id);

        using var inaccessible = await _client.GetAsync(
            $"/api/v1/saved-places/{otherId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, inaccessible.StatusCode);
    }

    [Fact]
    public async Task saved_place_creation_replays_the_same_id_and_rejects_different_content()
    {
        var id = NewId();
        var request = new { id, name = "Borgo medievale", category = 10 };

        using var created = await _client.PostJsonAsync(
            "/api/v1/saved-places",
            request,
            TestContext.Current.CancellationToken);
        using var replayed = await _client.PostJsonAsync(
            "/api/v1/saved-places",
            request,
            TestContext.Current.CancellationToken);
        using var conflict = await _client.PostJsonAsync(
            "/api/v1/saved-places",
            new { id, name = "Nome diverso", category = 10 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task saved_place_creation_rejects_invalid_coordinates_and_non_https_links()
    {
        using var invalidCoordinates = await _client.PostJsonAsync(
            "/api/v1/saved-places",
            new
            {
                id = NewId(),
                name = "Coordinate fuori intervallo",
                category = 9,
                position = new { latitude = 90.000001m, longitude = 12m },
            },
            TestContext.Current.CancellationToken);
        using var invalidLink = await _client.PostJsonAsync(
            "/api/v1/saved-places",
            new
            {
                id = NewId(),
                name = "Link non sicuro",
                category = 9,
                googleMapsUrl = "http://maps.google.com/?q=Roma",
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, invalidCoordinates.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidLink.StatusCode);
    }

    [Fact]
    public async Task saved_place_pages_continue_without_repeating_or_skipping_places()
    {
        var ids = new[] { NewId(), NewId() };
        foreach (var id in ids)
        {
            using var created = await CreatePlaceAsync(_client, id, $"Luogo {id}");
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        using var firstResponse = await _client.GetAsync(
            "/api/v1/saved-places?limit=1",
            TestContext.Current.CancellationToken);
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<SavedPlacePage>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstPage);
        Assert.Single(firstPage.Items);
        Assert.NotNull(firstPage.NextCursor);

        using var secondResponse = await _client.GetAsync(
            $"/api/v1/saved-places?limit=1&after={Uri.EscapeDataString(firstPage.NextCursor)}",
            TestContext.Current.CancellationToken);
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<SavedPlacePage>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondPage);
        Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextCursor);
        Assert.Equal(ids, firstPage.Items.Concat(secondPage.Items).Select(place => place.Id));
    }

    [Fact]
    public async Task edit_requires_if_match_and_a_stale_etag_is_rejected_after_a_successful_edit()
    {
        var id = NewId();
        using var created = await CreatePlaceAsync(_client, id, "Area sosta");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var firstEtag = ETagOf(created);

        using var withoutIfMatch = await _client.SendUnsafeWithCsrfAsync(
            HttpMethod.Put,
            $"/api/v1/saved-places/{id:D}",
            EditBody("Area sosta modificata"),
            ifMatch: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.PreconditionRequired, withoutIfMatch.StatusCode);

        using var edited = await _client.SendUnsafeWithCsrfAsync(
            HttpMethod.Put,
            $"/api/v1/saved-places/{id:D}",
            EditBody("Area sosta modificata"),
            firstEtag,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var secondEtag = ETagOf(edited);
        Assert.NotEqual(firstEtag, secondEtag);

        using var read = await _client.GetAsync(
            $"/api/v1/saved-places/{id:D}",
            TestContext.Current.CancellationToken);
        var details = await read.Content.ReadFromJsonAsync<SavedPlaceResponse>(
            TestContext.Current.CancellationToken);
        Assert.Equal("Area sosta modificata", details?.Name);
        Assert.Equal(secondEtag, ETagOf(read));

        using var stale = await _client.SendUnsafeWithCsrfAsync(
            HttpMethod.Put,
            $"/api/v1/saved-places/{id:D}",
            EditBody("Modifica con versione vecchia"),
            firstEtag,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
    }

    [Fact]
    public async Task delete_requires_if_match_and_removes_the_place_only_with_the_current_etag()
    {
        var id = NewId();
        using var created = await CreatePlaceAsync(_client, id, "Da cancellare");
        var firstEtag = ETagOf(created);
        var path = $"/api/v1/saved-places/{id:D}";

        using var withoutIfMatch = await _client.SendUnsafeWithCsrfAsync(
            HttpMethod.Delete, path, body: null, ifMatch: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.PreconditionRequired, withoutIfMatch.StatusCode);

        using var edited = await _client.SendUnsafeWithCsrfAsync(
            HttpMethod.Put, path, EditBody("Da cancellare dopo modifica"), firstEtag, TestContext.Current.CancellationToken);
        var currentEtag = ETagOf(edited);

        using var stale = await _client.SendUnsafeWithCsrfAsync(
            HttpMethod.Delete, path, body: null, ifMatch: firstEtag, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);

        using var deleted = await _client.SendUnsafeWithCsrfAsync(
            HttpMethod.Delete, path, body: null, ifMatch: currentEtag, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var missing = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task another_owners_place_returns_the_same_not_found_as_a_missing_id_for_read_edit_and_delete()
    {
        var otherOwnerId = Guid.NewGuid();
        const string otherEmail = "uniform-404-other-owner@example.test";
        await CreateUserAsync(otherOwnerId, otherEmail);
        using var otherClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        using var login = await otherClient.LoginAsync(otherEmail, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var otherId = NewId();
        using var otherPlace = await CreatePlaceAsync(otherClient, otherId, "Luogo di un altro utente");
        Assert.Equal(HttpStatusCode.Created, otherPlace.StatusCode);
        var missingId = NewId();
        const string anyEtag = "\"AAAAAAAAAAE=\"";

        var foreignProblems = new List<string?>();
        var missingProblems = new List<string?>();
        foreach (var (candidate, problems) in new[] { (otherId, foreignProblems), (missingId, missingProblems) })
        {
            var path = $"/api/v1/saved-places/{candidate:D}";
            using var read = await _client.GetAsync(path, TestContext.Current.CancellationToken);
            using var edit = await _client.SendUnsafeWithCsrfAsync(
                HttpMethod.Put, path, EditBody("Tentativo"), anyEtag, TestContext.Current.CancellationToken);
            using var delete = await _client.SendUnsafeWithCsrfAsync(
                HttpMethod.Delete, path, body: null, ifMatch: anyEtag, TestContext.Current.CancellationToken);

            foreach (var response in new[] { read, edit, delete })
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                problems.Add(await ProblemTypeAsync(response));
            }
        }

        Assert.Equal(missingProblems, foreignProblems);

        using var stillThere = await otherClient.GetAsync(
            $"/api/v1/saved-places/{otherId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task deleting_a_place_used_by_a_trip_stop_returns_conflict_and_keeps_the_place()
    {
        var id = NewId();
        using var created = await CreatePlaceAsync(_client, id, "Usato in tappa");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        await using (var db = CreateOwnerContext())
        {
            var camperId = Guid.NewGuid();
            var tripId = Guid.NewGuid();
            var createdAt = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
            db.Campers.Add(new Camper
            {
                Id = camperId,
                OwnerId = _ownerId,
                Name = "Camper di prova",
                VehicleKind = VehicleKind.Coachbuilt,
                Dimensions = new Roamly.Domain.ValueObjects.Dimensions(7400, 2300, 2900),
                CreatedAtUtc = createdAt,
            });
            db.Trips.Add(new Trip
            {
                Id = tripId,
                OwnerId = _ownerId,
                CamperId = camperId,
                Name = "Viaggio di prova",
                CreatedAtUtc = createdAt,
            });
            db.TripStops.Add(new TripStop
            {
                Id = Guid.NewGuid(),
                OwnerId = _ownerId,
                TripId = tripId,
                SavedPlaceId = id,
                SequenceNo = 1,
                Name = "Tappa collegata",
                CreatedAtUtc = createdAt,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var read = await _client.GetAsync($"/api/v1/saved-places/{id:D}", TestContext.Current.CancellationToken);
        using var deleted = await _client.SendUnsafeWithCsrfAsync(
            HttpMethod.Delete,
            $"/api/v1/saved-places/{id:D}",
            body: null,
            ifMatch: ETagOf(read),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Equal("https://roamly.dev/problems/saved-place-referenced", await ProblemTypeAsync(deleted));

        using var stillThere = await _client.GetAsync($"/api/v1/saved-places/{id:D}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    private static object EditBody(string name) => new
    {
        name,
        category = 12,
        position = new { latitude = 45.0694m, longitude = 7.6869m },
        address = "Via Accademia delle Scienze, Torino",
        notes = "Modificato",
        googleMapsUrl = "https://maps.google.com/?q=Torino",
    };

    private static string ETagOf(HttpResponseMessage response) =>
        response.Headers.ETag?.ToString() ?? throw new InvalidOperationException("ETag mancante.");

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.TryGetProperty("type", out var type) ? type.GetString() : null;
    }

    private static Task<HttpResponseMessage> CreatePlaceAsync(
        RoamlyClient client,
        Guid id,
        string name)
    {
        return client.PostJsonAsync(
            "/api/v1/saved-places",
            new { id, name, category = 9 },
            TestContext.Current.CancellationToken);
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

    private sealed record SavedPlacePage(IReadOnlyList<SavedPlaceResponse> Items, string? NextCursor);

    private sealed record SavedPlaceResponse(
        Guid Id,
        string Name,
        int Category,
        PositionResponse? Position,
        string? Address,
        string? Notes,
        string? GoogleMapsUrl);

    private sealed record PositionResponse(decimal Latitude, decimal Longitude);
}
