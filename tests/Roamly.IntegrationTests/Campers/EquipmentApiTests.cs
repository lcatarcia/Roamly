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
public sealed class EquipmentApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Password = "Test!Password123";
    private readonly Guid _ownerId = Guid.NewGuid();
    private RoamlyApiFactory _factory = null!;
    private RoamlyClient _client = null!;
    private DatabaseLease _database = null!;
    private Guid _camperId;

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

        await CreateUserAsync(_ownerId, "equipment-owner@example.test");
        using var login = await _client.LoginAsync(
            "equipment-owner@example.test",
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        _camperId = await CreateCamperAsync(_client, "Porta bici");
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
    public async Task owner_can_create_list_edit_and_physically_remove_equipment()
    {
        var equipmentId = NewId();
        using var created = await _client.PostJsonAsync(EquipmentCollectionPath, new
        {
            id = equipmentId,
            name = "Pannello solare",
            category = 0,
            notes = "Sul tetto",
            installedOnUtc = "2025-04-10",
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var list = await _client.GetAsync(EquipmentCollectionPath, TestContext.Current.CancellationToken);
        var page = await list.Content.ReadFromJsonAsync<PageResponse<EquipmentResponse>>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.NotNull(page);
        var equipment = Assert.Single(page.Items);
        Assert.Equal(equipmentId, equipment.Id);
        Assert.Equal("Pannello solare", equipment.Name);
        Assert.Equal("2025-04-10", equipment.InstalledOnUtc);

        using var read = await _client.GetAsync(ItemPath(equipmentId), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var etag = HttpEtagAssertions.StrongRowVersion(read.Headers.ETag?.ToString());

        using var weakUpdate = await SendJsonAsync(
            HttpMethod.Put,
            ItemPath(equipmentId),
            HttpEtagAssertions.AsWeak(etag),
            new { name = "ETag debole", category = 0 });
        Assert.Equal(HttpStatusCode.PreconditionFailed, weakUpdate.StatusCode);

        using var updated = await SendJsonAsync(
            HttpMethod.Put,
            ItemPath(equipmentId),
            etag,
            new
            {
                name = "Pannello solare 200 W",
                category = 0,
                notes = "Aggiornato",
                installedOnUtc = "2025-04-11",
            });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedItem = await updated.Content.ReadFromJsonAsync<EquipmentResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(updatedItem);
        Assert.Equal("Pannello solare 200 W", updatedItem.Name);
        Assert.Equal("Aggiornato", updatedItem.Notes);
        Assert.NotEqual(etag, HttpEtagAssertions.StrongRowVersion(updated.Headers.ETag?.ToString()));

        using var staleUpdate = await SendJsonAsync(
            HttpMethod.Put,
            ItemPath(equipmentId),
            etag,
            new { name = "Vecchio aggiornamento", category = 0 });
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleUpdate.StatusCode);
        using var missingPrecondition = await SendDeleteAsync(ItemPath(equipmentId), null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missingPrecondition.StatusCode);
        using var staleDelete = await SendDeleteAsync(ItemPath(equipmentId), etag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleDelete.StatusCode);

        using var removed = await SendDeleteAsync(ItemPath(equipmentId), updated.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var missing = await _client.GetAsync(ItemPath(equipmentId), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task equipment_is_owner_scoped_for_list_read_create_update_and_delete()
    {
        var equipmentId = NewId();
        using var created = await _client.PostJsonAsync(EquipmentCollectionPath, new
        {
            id = equipmentId,
            name = "Inverter",
            category = 2,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var otherOwnerId = Guid.NewGuid();
        const string otherEmail = "equipment-other-owner@example.test";
        await CreateUserAsync(otherOwnerId, otherEmail);
        using var otherClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        using var login = await otherClient.LoginAsync(otherEmail, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var list = await otherClient.GetAsync(EquipmentCollectionPath, TestContext.Current.CancellationToken);
        using var read = await otherClient.GetAsync(ItemPath(equipmentId), TestContext.Current.CancellationToken);
        using var create = await otherClient.PostJsonAsync(EquipmentCollectionPath, new
        {
            id = NewId(),
            name = "Non autorizzato",
            category = 7,
        }, TestContext.Current.CancellationToken);
        using var update = await SendJsonAsync(
            otherClient,
            HttpMethod.Put,
            ItemPath(equipmentId),
            "W/\"AAAAAAAAAAA\"",
            new { name = "Modificato", category = 7 });
        using var delete = await SendDeleteAsync(otherClient, ItemPath(equipmentId), "W/\"AAAAAAAAAAA\"");

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task equipment_rejects_invalid_names_and_categories()
    {
        using var response = await _client.PostJsonAsync(EquipmentCollectionPath, new
        {
            id = NewId(),
            name = "   ",
            category = 99,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _), errors.GetRawText());
        Assert.True(errors.TryGetProperty("category", out _), errors.GetRawText());
    }

    [Fact]
    public async Task equipment_creation_replays_matching_content_and_rejects_conflicting_content()
    {
        var id = NewId();
        using var created = await _client.PostJsonAsync(EquipmentCollectionPath, new
        {
            id,
            name = "  Tendalino  ",
            category = 5,
            notes = " Laterale ",
            installedOnUtc = "2025-04-10",
        }, TestContext.Current.CancellationToken);
        using var replay = await _client.PostJsonAsync(EquipmentCollectionPath, new
        {
            id,
            name = "Tendalino",
            category = 5,
            notes = "Laterale",
            installedOnUtc = "2025-04-10",
        }, TestContext.Current.CancellationToken);
        using var conflict = await _client.PostJsonAsync(EquipmentCollectionPath, new
        {
            id,
            name = "Tendalino diverso",
            category = 5,
            notes = "Laterale",
            installedOnUtc = "2025-04-10",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType?.MediaType);

        var otherCamperId = await CreateCamperAsync(_client, "Secondo camper");
        using var crossCamperConflict = await _client.PostJsonAsync(
            $"/api/v1/campers/{otherCamperId:D}/equipment",
            new
            {
                id,
                name = "Tendalino",
                category = 5,
                notes = "Laterale",
                installedOnUtc = "2025-04-10",
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, crossCamperConflict.StatusCode);
    }

    [Fact]
    public async Task equipment_collection_returns_keyset_pages_without_duplicates()
    {
        foreach (var name in new[] { "Pannello", "Batteria", "Inverter" })
        {
            using var response = await _client.PostJsonAsync(EquipmentCollectionPath, new
            {
                id = NewId(),
                name,
                category = 7,
            }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var seenIds = new List<Guid>();
        string? cursor = null;
        string path;
        do
        {
            path = cursor is null
                ? $"{EquipmentCollectionPath}?limit=1"
                : $"{EquipmentCollectionPath}?limit=1&after={Uri.EscapeDataString(cursor)}";
            using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);
            var page = await response.Content.ReadFromJsonAsync<PageResponse<EquipmentResponse>>(
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(page);
            seenIds.Add(Assert.Single(page.Items).Id);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal(3, seenIds.Distinct().Count());
    }

    private string EquipmentCollectionPath => $"/api/v1/campers/{_camperId:D}/equipment";

    private string ItemPath(Guid id) => $"{EquipmentCollectionPath}/{id:D}";

    private static async Task<Guid> CreateCamperAsync(RoamlyClient client, string name)
    {
        var id = NewId();
        using var response = await client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name,
            vehicleKind = (int)VehicleKind.Van,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return id;
    }

    private Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string? etag, object body)
    {
        return SendJsonAsync(_client, method, path, etag, body);
    }

    private Task<HttpResponseMessage> SendDeleteAsync(string path, string? etag)
    {
        return SendDeleteAsync(_client, path, etag);
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        RoamlyClient client,
        HttpMethod method,
        string path,
        string? etag,
        object body)
    {
        var token = await ReadCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        AddMutationHeaders(request, token, etag);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> SendDeleteAsync(RoamlyClient client, string path, string? etag)
    {
        var token = await ReadCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        AddMutationHeaders(request, token, etag);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<string> ReadCsrfTokenAsync(RoamlyClient client)
    {
        using var response = await client.GetAsync("/api/v1/csrf-token", TestContext.Current.CancellationToken);
        var token = await response.Content.ReadFromJsonAsync<CsrfTokenResponse>(
            TestContext.Current.CancellationToken);
        return token?.Token ?? throw new InvalidOperationException("Token CSRF non restituito.");
    }

    private static void AddMutationHeaders(HttpRequestMessage request, string token, string? etag)
    {
        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add("X-XSRF-TOKEN", token);
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }
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

    private sealed record CsrfTokenResponse(string Token);
    private sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
    private sealed record EquipmentResponse(
        Guid Id,
        string Name,
        int Category,
        string? Notes,
        string? InstalledOnUtc);

    private static Guid NewId() => new SequentialGuidGenerator(TimeProvider.System).NewId();
}
