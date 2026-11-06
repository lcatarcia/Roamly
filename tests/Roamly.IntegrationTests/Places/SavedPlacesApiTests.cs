using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Common;
using Roamly.Infrastructure.Identity;
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
