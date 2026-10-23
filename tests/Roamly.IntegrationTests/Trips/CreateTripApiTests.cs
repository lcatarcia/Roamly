using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Common;
using Roamly.Infrastructure.Identity;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Trips;

[Collection(DatabaseCollection.Name)]
public sealed class CreateTripApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
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

        await CreateUserAsync(_ownerId, "trip-owner@example.test");
        using var login = await _client.LoginAsync(
            "trip-owner@example.test",
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
    public async Task traveller_can_create_a_trip_with_a_first_stop_and_read_it_back()
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
        var request = new
        {
            id = tripId,
            name = "Weekend in Bretagna",
            stops = new[]
            {
                new { name = "Saint-Malo" },
            },
        };
        using var created = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(
            $"/api/v1/campers/{camperId:D}/trips/{tripId:D}",
            created.Headers.Location?.OriginalString);

        using var replayed = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            request,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);

        var location = created.Headers.Location;
        Assert.NotNull(location);
        using var read = await _client.GetAsync(
            location.OriginalString,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var details = await read.Content.ReadFromJsonAsync<TripDetailsResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(details);
        Assert.Equal(tripId, details.Id);
        Assert.Equal(camperId, details.CamperId);
        Assert.Equal("Weekend in Bretagna", details.Name);
        Assert.Equal(0, details.Status);
        HttpEtagAssertions.StrongRowVersion(read.Headers.ETag?.ToString());
        Assert.Collection(
            details.Stops,
            stop =>
            {
                Assert.Equal(1, stop.SequenceNo);
                Assert.Equal("Saint-Malo", stop.Name);
            });
    }

    [Fact]
    public async Task owner_can_replace_trip_metadata_and_reorder_edit_add_and_remove_stops()
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
            new
            {
                id = tripId,
                name = "Prima versione",
                stops = new[]
                {
                    new { name = "Saint-Malo" },
                    new { name = "Rennes" },
                    new { name = "Nantes" },
                },
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var initialRead = await _client.GetAsync(
            $"/api/v1/trips/{tripId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, initialRead.StatusCode);
        var initial = await initialRead.Content.ReadFromJsonAsync<TripDetailsResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(initial);
        var initialStopIds = initial.Stops.Select(stop => stop.Id).ToArray();
        Assert.Equal(3, initialStopIds.Length);
        var etag = initialRead.Headers.ETag?.ToString();
        Assert.NotNull(etag);

        using var update = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/v1/trips/{tripId:D}",
            etag,
            new
            {
                name = "Bretagna in treno",
                camperId = NewId(),
                status = 3,
                actualStartOnUtc = "2026-08-01",
                actualEndOnUtc = "2026-08-10",
                plannedStartOnUtc = "2026-08-01",
                plannedEndOnUtc = "2026-08-10",
                stops = new object[]
                {
                    new
                    {
                        id = initialStopIds[1],
                        name = "Rennes centre",
                        arrivalOnUtc = "2026-08-02",
                        departureOnUtc = "2026-08-03",
                        position = new { latitude = 48.1113m, longitude = -1.6800m },
                        notes = "Seconda tappa",
                        savedPlaceId = NewId(),
                    },
                    new
                    {
                        name = "Dinan",
                        arrivalOnUtc = "2026-08-04",
                        departureOnUtc = "2026-08-05",
                        position = new { latitude = 48.4544m, longitude = -2.0487m },
                        notes = "Nuova tappa",
                        savedPlaceId = NewId(),
                    },
                    new
                    {
                        id = initialStopIds[0],
                        name = "Saint-Malo modificata",
                        arrivalOnUtc = "2026-08-06",
                        departureOnUtc = "2026-08-07",
                        position = new { latitude = 48.6493m, longitude = -2.0257m },
                        notes = "Prima tappa dopo il riordino",
                        savedPlaceId = NewId(),
                    },
                },
            });

        Assert.True(
            update.StatusCode == HttpStatusCode.OK,
            await update.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.NotEqual(etag, update.Headers.ETag?.ToString());
        var details = await update.Content.ReadFromJsonAsync<TripDetailsResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(details);
        Assert.Equal(camperId, details.CamperId);
        Assert.Equal("Bretagna in treno", details.Name);
        Assert.Equal(0, details.Status);
        Assert.Equal(new DateOnly(2026, 8, 1), details.PlannedStartOnUtc);
        Assert.Equal(new DateOnly(2026, 8, 10), details.PlannedEndOnUtc);
        Assert.Collection(
            details.Stops,
            stop =>
            {
                Assert.Equal(initialStopIds[1], stop.Id);
                Assert.Equal(1, stop.SequenceNo);
                Assert.Equal("Rennes centre", stop.Name);
                Assert.Equal(new DateOnly(2026, 8, 2), stop.ArrivalOnUtc);
                Assert.Equal(new DateOnly(2026, 8, 3), stop.DepartureOnUtc);
                Assert.Equal(48.1113m, stop.Position!.Latitude);
                Assert.Equal(-1.6800m, stop.Position.Longitude);
                Assert.Equal("Seconda tappa", stop.Notes);
            },
            stop =>
            {
                Assert.NotEqual(initialStopIds[0], stop.Id);
                Assert.NotEqual(initialStopIds[1], stop.Id);
                Assert.NotEqual(initialStopIds[2], stop.Id);
                Assert.Equal(2, stop.SequenceNo);
                Assert.Equal("Dinan", stop.Name);
                Assert.Equal(new DateOnly(2026, 8, 4), stop.ArrivalOnUtc);
                Assert.Equal(new DateOnly(2026, 8, 5), stop.DepartureOnUtc);
                Assert.Equal(48.4544m, stop.Position!.Latitude);
                Assert.Equal(-2.0487m, stop.Position.Longitude);
                Assert.Equal("Nuova tappa", stop.Notes);
            },
            stop =>
            {
                Assert.Equal(initialStopIds[0], stop.Id);
                Assert.Equal(3, stop.SequenceNo);
                Assert.Equal("Saint-Malo modificata", stop.Name);
                Assert.Equal(new DateOnly(2026, 8, 6), stop.ArrivalOnUtc);
                Assert.Equal(new DateOnly(2026, 8, 7), stop.DepartureOnUtc);
                Assert.Equal(48.6493m, stop.Position!.Latitude);
                Assert.Equal(-2.0257m, stop.Position.Longitude);
                Assert.Equal("Prima tappa dopo il riordino", stop.Notes);
            });

        Assert.DoesNotContain(initialStopIds[2], details.Stops.Select(stop => stop.Id));
        Assert.Equal(
            new[] { initialStopIds[1], details.Stops[1].Id, initialStopIds[0] },
            details.Stops.Select(stop => stop.Id));
    }

    [Fact]
    public async Task trip_idempotency_returns_conflict_when_the_same_id_has_different_content()
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
            new { id = tripId, name = "Primo nome", stops = new[] { new { name = "Saint-Malo" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var conflict = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            new { id = tripId, name = "Nome diverso", stops = new[] { new { name = "Saint-Malo" } } },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task trip_creation_rejects_null_stop_items()
    {
        var camperId = NewId();
        using var camper = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id = camperId,
            name = "Il Grigio",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, camper.StatusCode);

        using var response = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            new { id = NewId(), name = "Weekend", stops = new object?[] { null } },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task traveller_cannot_read_a_trip_owned_by_another_user()
    {
        var otherOwnerId = Guid.NewGuid();
        var otherEmail = "other-trip-owner@example.test";
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

        var camperId = NewId();
        using var camper = await otherClient.PostJsonAsync("/api/v1/campers", new
        {
            id = camperId,
            name = "Camper di un altro utente",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, camper.StatusCode);

        var tripId = NewId();
        using var trip = await otherClient.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            new { id = tripId, name = "Viaggio privato", stops = new[] { new { name = "Carnac" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, trip.StatusCode);

        using var read = await _client.GetAsync(
            $"/api/v1/trips/{tripId:D}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        using var update = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/v1/trips/{tripId:D}",
            null,
            new { name = "Tentativo", stops = new[] { new { name = "Luogo" } } });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        using var unchanged = await otherClient.GetAsync(
            $"/api/v1/trips/{tripId:D}",
            TestContext.Current.CancellationToken);
        var details = await unchanged.Content.ReadFromJsonAsync<TripDetailsResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(details);
        Assert.Equal("Viaggio privato", details.Name);
    }

    [Fact]
    public async Task trip_update_requires_if_match_and_rejects_malformed_or_stale_etags()
    {
        var trip = await CreateTripAsync("Versione iniziale", "Carnac");
        var path = $"/api/v1/trips/{trip.Id:D}";
        var body = ReplacementBody(trip.Details, "Prima sostituzione", "Carnac rinnovata");

        using var missing = await SendJsonAsync(HttpMethod.Put, path, null, body);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);

        using var weak = await SendJsonAsync(HttpMethod.Put, path, HttpEtagAssertions.AsWeak(trip.ETag), body);
        Assert.Equal(HttpStatusCode.PreconditionFailed, weak.StatusCode);
        Assert.Equal("application/problem+json", weak.Content.Headers.ContentType?.MediaType);

        using var updated = await SendJsonAsync(HttpMethod.Put, path, trip.ETag, body);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var nextEtag = updated.Headers.ETag?.ToString();
        HttpEtagAssertions.StrongRowVersion(nextEtag);
        Assert.NotEqual(trip.ETag, nextEtag);
        var updatedDetails = await updated.Content.ReadFromJsonAsync<TripDetailsResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(updatedDetails);

        using var stopOnlyUpdate = await SendJsonAsync(
            HttpMethod.Put,
            path,
            nextEtag,
            ReplacementBody(updatedDetails, updatedDetails.Name, "Solo modifica tappa"));
        Assert.Equal(HttpStatusCode.OK, stopOnlyUpdate.StatusCode);
        Assert.NotEqual(
            nextEtag,
            HttpEtagAssertions.StrongRowVersion(stopOnlyUpdate.Headers.ETag?.ToString()));

        using var stale = await SendJsonAsync(HttpMethod.Put, path, trip.ETag, body);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using var unchanged = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        var current = await unchanged.Content.ReadFromJsonAsync<TripDetailsResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(current);
        Assert.Equal("Prima sostituzione", current.Name);
        Assert.Equal("Solo modifica tappa", Assert.Single(current.Stops).Name);
        Assert.Equal(stopOnlyUpdate.Headers.ETag?.ToString(), unchanged.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task trip_update_rejects_duplicate_unknown_and_other_trip_stop_ids_atomically()
    {
        var trip = await CreateTripAsync("Viaggio da preservare", "Carnac", "Quiberon");
        var otherTrip = await CreateTripAsync("Altro viaggio", "Rennes");
        var path = $"/api/v1/trips/{trip.Id:D}";
        var invalidRequests = new object[]
        {
            new
            {
                name = "Nome da non salvare",
                stops = new[]
                {
                    new { id = trip.Details.Stops[0].Id, name = "Tappa duplicata" },
                    new { id = trip.Details.Stops[0].Id, name = "Tappa duplicata ancora" },
                },
            },
            new
            {
                name = "Nome da non salvare",
                stops = new[]
                {
                    new { id = trip.Details.Stops[0].Id, name = "Tappa esistente" },
                    new { id = NewId(), name = "ID sconosciuto" },
                },
            },
            new
            {
                name = "Nome da non salvare",
                stops = new[]
                {
                    new { id = otherTrip.Details.Stops[0].Id, name = "Tappa di un altro viaggio" },
                },
            },
            new
            {
                name = "Nome da non salvare",
                stops = new[] { new { id = Guid.Empty, name = "ID vuoto" } },
            },
        };

        for (var index = 0; index < invalidRequests.Length; index++)
        {
            using var response = await SendJsonAsync(HttpMethod.Put, path, trip.ETag, invalidRequests[index]);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var problem = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(problem.RootElement.TryGetProperty("errors", out _), problem.RootElement.GetRawText());
            Assert.True(
                problem.RootElement.GetProperty("errors").TryGetProperty("stops", out _),
                problem.RootElement.GetRawText());
        }

        using var unchanged = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        var current = await unchanged.Content.ReadFromJsonAsync<TripDetailsResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(current);
        Assert.Equal(trip.ETag, unchanged.Headers.ETag?.ToString());
        Assert.Equal("Viaggio da preservare", current.Name);
        Assert.Equal(
            trip.Details.Stops.Select(stop => stop.Id),
            current.Stops.Select(stop => stop.Id));
    }

    [Fact]
    public async Task trip_update_rejects_invalid_names_dates_stops_and_coordinates_without_partial_writes()
    {
        var trip = await CreateTripAsync("Viaggio valido", "Carnac");
        var existingStopId = trip.Details.Stops[0].Id;
        var path = $"/api/v1/trips/{trip.Id:D}";
        var invalidRequests = new object[]
        {
            new { name = " ", stops = new[] { new { id = existingStopId, name = "Carnac" } } },
            new
            {
                name = "Nome da non salvare",
                plannedStartOnUtc = "2026-08-11",
                plannedEndOnUtc = "2026-08-10",
                stops = new[] { new { id = existingStopId, name = "Carnac" } },
            },
            new { name = "Nome da non salvare", stops = Array.Empty<object>() },
            new { name = "Nome da non salvare", stops = new object?[] { null } },
            new { name = "Nome da non salvare", stops = new[] { new { id = existingStopId, name = " " } } },
            new
            {
                name = "Nome da non salvare",
                stops = new[]
                {
                    new
                    {
                        id = existingStopId,
                        name = "Carnac",
                        arrivalOnUtc = "2026-08-11",
                        departureOnUtc = "2026-08-10",
                    },
                },
            },
            new
            {
                name = "Nome da non salvare",
                stops = new[] { new { id = existingStopId, name = "Carnac", position = new { latitude = 90.1m, longitude = 0m } } },
            },
            new
            {
                name = "Nome da non salvare",
                stops = new[] { new { id = existingStopId, name = "Carnac", position = new { latitude = 45m, longitude = -180.1m } } },
            },
            new
            {
                name = "Nome da non salvare",
                stops = new[] { new { id = existingStopId, name = "Carnac", position = new { latitude = 45m, longitude = (decimal?)null } } },
            },
            new
            {
                name = "Nome da non salvare",
                stops = new[] { new { id = existingStopId, name = "Carnac", notes = new string('x', 1001) } },
            },
        };

        for (var index = 0; index < invalidRequests.Length; index++)
        {
            using var response = await SendJsonAsync(HttpMethod.Put, path, trip.ETag, invalidRequests[index]);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var problem = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(problem.RootElement.TryGetProperty("errors", out _), problem.RootElement.GetRawText());
            if (index == 0)
            {
                Assert.True(
                    problem.RootElement.GetProperty("errors").TryGetProperty("name", out _),
                    problem.RootElement.GetRawText());
            }
        }

        using var unchanged = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        var current = await unchanged.Content.ReadFromJsonAsync<TripDetailsResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(current);
        Assert.Equal(trip.ETag, unchanged.Headers.ETag?.ToString());
        Assert.Equal("Viaggio valido", current.Name);
        Assert.Equal("Carnac", Assert.Single(current.Stops).Name);
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

    private async Task<TripFixture> CreateTripAsync(string name, params string[] stopNames)
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
            new { id = tripId, name, stops = stopNames.Select(stopName => new { name = stopName }).ToArray() },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var read = await _client.GetAsync(
            $"/api/v1/trips/{tripId:D}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var details = await read.Content.ReadFromJsonAsync<TripDetailsResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(details);
        var etag = read.Headers.ETag?.ToString();
        Assert.NotNull(etag);
        return new TripFixture(tripId, etag, details);
    }

    private static object ReplacementBody(TripDetailsResponse details, string name, string stopName)
    {
        return new
        {
            name,
            plannedStartOnUtc = (DateOnly?)null,
            plannedEndOnUtc = (DateOnly?)null,
            stops = new[]
            {
                new { id = details.Stops[0].Id, name = stopName, arrivalOnUtc = (DateOnly?)null,
                    departureOnUtc = (DateOnly?)null, position = (object?)null, notes = (string?)null },
            },
        };
    }

    private async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string path,
        string? etag,
        object body)
    {
        using var csrf = await _client.GetAsync("/api/v1/csrf-token", TestContext.Current.CancellationToken);
        var token = await csrf.Content.ReadFromJsonAsync<CsrfTokenResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(token);
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add("X-XSRF-TOKEN", token.Token);
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Guid NewId() => new SequentialGuidGenerator(TimeProvider.System).NewId();

    private sealed record CsrfTokenResponse(string Token);
    private sealed record TripFixture(Guid Id, string ETag, TripDetailsResponse Details);
    private sealed record PositionResponse(decimal Latitude, decimal Longitude);
    private sealed record TripDetailsResponse(
        Guid Id,
        Guid CamperId,
        string Name,
        int Status,
        DateOnly? PlannedStartOnUtc,
        DateOnly? PlannedEndOnUtc,
        IReadOnlyList<TripStopResponse> Stops);
    private sealed record TripStopResponse(
        Guid Id,
        int SequenceNo,
        string Name,
        DateOnly? ArrivalOnUtc,
        DateOnly? DepartureOnUtc,
        PositionResponse? Position,
        string? Notes);
}
