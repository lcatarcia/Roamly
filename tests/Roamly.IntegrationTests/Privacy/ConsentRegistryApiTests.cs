using System.Net;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roamly.Api.Features.Identity.Consents;
using Roamly.Common;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Privacy;

[Collection(DatabaseCollection.Name)]
public sealed partial class ConsentRegistryApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Password = "Test!Password123";
    private const string Email = "consent-owner@example.test";
    private readonly Guid _ownerId = Guid.NewGuid();
    private RoamlyApiFactory _factory = null!;
    private RoamlyClient _client = null!;
    private DatabaseLease _database = null!;
    private static readonly DateTimeOffset ServerNow = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        _database = migrated.Lease;
        await _database.ResetAsync(Ct);
        _factory = NewFactory();
        _client = CreateClient(_factory);
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await manager.CreateAsync(new ApplicationUser
        {
            Id = _ownerId, UserName = Email, Email = Email, EmailConfirmed = true,
        }, Password);
        Assert.True(result.Succeeded);
        using var login = await _client.LoginAsync(Email, Password, Ct);
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
    public async Task Maps_purpose_is_explicitly_draft_and_never_granted()
    {
        using var response = await _client.GetAsync("/api/v1/me/consents/azure-maps-render", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<ConsentState>(Ct);
        Assert.NotNull(state);
        Assert.False(state.IsActive);
        Assert.False(state.IsGranted);
        Assert.Equal("DRAFT-1", state.Version);
        Assert.Equal(0, state.Revision);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Draft_purpose_refuses_effective_grant_without_recording_apparent_consent()
    {
        using var grant = await PostAsync("azure-maps-render", "grants", NewId(),
            "DRAFT-1", "DRAFT:azure-maps-render:DRAFT-1");
        Assert.Equal(HttpStatusCode.Conflict, grant.StatusCode);
        Assert.Contains("consent-purpose-inactive", await grant.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(0, (await ReadStateAsync("azure-maps-render")).Revision);
    }

    [Fact]
    public async Task Synthetic_grant_is_recorded_once_and_old_retry_cannot_undo_revocation()
    {
        await UseSyntheticDefinitionAsync();
        var id = NewId();
        using var grant = await PostAsync("synthetic-render", "grants", id);
        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);
        var receipt = await grant.Content.ReadFromJsonAsync<Receipt>(Ct);
        Assert.NotNull(receipt);
        Assert.Equal(1, receipt.Revision);
        Assert.Equal(ServerNow.UtcDateTime, receipt.RecordedAtUtc);
        Assert.True((await ReadStateAsync()).IsGranted);

        using var retry = await PostAsync("synthetic-render", "grants", id);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(receipt, await retry.Content.ReadFromJsonAsync<Receipt>(Ct));

        using var revoke = await PostAsync("synthetic-render", "revocations", NewId());
        Assert.Equal(HttpStatusCode.Created, revoke.StatusCode);
        using var oldRetry = await PostAsync("synthetic-render", "grants", id);
        Assert.Equal(HttpStatusCode.OK, oldRetry.StatusCode);
        var current = await ReadStateAsync();
        Assert.False(current.IsGranted);
        Assert.Equal(2, current.Revision);
    }

    [Theory]
    [InlineData("revocations", "test-v1", "synthetic-text-1")]
    [InlineData("grants", "different", "synthetic-text-1")]
    [InlineData("grants", "test-v1", "different")]
    public async Task Reusing_an_operation_id_with_different_payload_conflicts(
        string action, string version, string textReference)
    {
        await UseSyntheticDefinitionAsync();
        var id = NewId();
        using var created = await PostAsync("synthetic-render", "grants", id);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var conflict = await PostAsync("synthetic-render", action, id, version, textReference);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(1, (await ReadStateAsync()).Revision);
    }

    [Fact]
    public async Task A_new_version_requires_fresh_grant_and_revisions_span_versions()
    {
        await UseSyntheticDefinitionAsync();
        var oldId = NewId();
        using var old = await PostAsync("synthetic-render", "grants", oldId);
        Assert.Equal(HttpStatusCode.Created, old.StatusCode);
        await UseSyntheticDefinitionAsync("test-v2", "synthetic-text-2");
        Assert.False((await ReadStateAsync()).IsGranted);
        using var replay = await PostAsync("synthetic-render", "grants", oldId);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var stale = await PostAsync("synthetic-render", "grants", NewId());
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var fresh = await PostAsync("synthetic-render", "grants", NewId(), "test-v2", "synthetic-text-2");
        Assert.Equal(HttpStatusCode.Created, fresh.StatusCode);
        Assert.True((await ReadStateAsync()).IsGranted);
        using var revoke = await PostAsync("synthetic-render", "revocations", NewId(), "test-v2", "synthetic-text-2");
        Assert.Equal(HttpStatusCode.Created, revoke.StatusCode);
        var current = await ReadStateAsync();
        Assert.Equal(3, current.Revision);
        Assert.False(current.IsGranted);
    }

    [Fact]
    public async Task Whole_history_is_exported_without_network_data()
    {
        await UseSyntheticDefinitionAsync();
        using var grant = await PostAsync("synthetic-render", "grants", NewId());
        using var revoke = await PostAsync("synthetic-render", "revocations", NewId());
        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);
        Assert.Equal(HttpStatusCode.Created, revoke.StatusCode);
        using var response = await _client.GetAsync("/api/v1/me/export", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var zipStream = await response.Content.ReadAsStreamAsync(Ct);
        using var zip = new ZipArchive(zipStream);
        var entry = zip.GetEntry("entities/ConsentTransition.json");
        Assert.NotNull(entry);
        using var records = await JsonDocument.ParseAsync(entry.Open(), cancellationToken: Ct);
        var history = records.RootElement.EnumerateArray().OrderBy(r => r.GetProperty("revision").GetInt64()).ToArray();
        Assert.Equal(2, history.Length);
        Assert.Equal(["grant", "revoke"], history.Select(r => r.GetProperty("action").GetString()).ToArray());
        foreach (var record in history)
        {
            Assert.Equal(_ownerId, record.GetProperty("ownerId").GetGuid());
            Assert.Equal(["action", "createdAtUtc", "id", "ownerId", "purpose", "revision", "textReference", "updatedAtUtc", "version"],
                record.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Writes_require_both_csrf_barriers(bool customHeader, bool antiforgeryHeader)
    {
        await UseSyntheticDefinitionAsync();
        using var response = await _client.PostJsonWithCsrfHeadersAsync(
            "/api/v1/me/consents/synthetic-render/grants",
            new { id = NewId(), version = "test-v1", textReference = "synthetic-text-1" },
            customHeader, antiforgeryHeader, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, (await ReadStateAsync()).Revision);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Password_change_and_deletion_restrictions_remain_in_force(bool passwordChange)
    {
        await UseSyntheticDefinitionAsync();
        await SetAccountRestrictedAsync(passwordChange);
        using var grant = await PostAsync("synthetic-render", "grants", NewId());
        using var revoke = await PostAsync("synthetic-render", "revocations", NewId());
        using var read = await _client.GetAsync("/api/v1/me/consents/synthetic-render", Ct);
        var expected = passwordChange ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized;
        Assert.Equal(expected, grant.StatusCode);
        Assert.Equal(expected, revoke.StatusCode);
        Assert.Equal(expected, read.StatusCode);
    }

    [Fact]
    public async Task Empty_operation_id_is_rejected_by_native_validation()
    {
        await UseSyntheticDefinitionAsync();
        using var response = await PostAsync("synthetic-render", "grants", Guid.Empty);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"id\"", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_reads_and_writes_are_not_allowed()
    {
        using var anonymous = CreateClient(_factory);
        using var read = await anonymous.GetAsync("/api/v1/me/consents/azure-maps-render", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        using var write = await anonymous.PostJsonAsync("/api/v1/me/consents/azure-maps-render/grants",
            new { id = NewId(), version = "DRAFT-1", textReference = "DRAFT:azure-maps-render:DRAFT-1" }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
    }

    [Fact]
    public async Task Operation_identity_and_latest_state_are_isolated_between_owners()
    {
        await UseSyntheticDefinitionAsync();
        var id = NewId();
        using var ownerGrant = await PostAsync("synthetic-render", "grants", id);
        Assert.Equal(HttpStatusCode.Created, ownerGrant.StatusCode);
        using var otherClient = await CreateOtherOwnerClientAsync();
        using var initial = await otherClient.GetAsync("/api/v1/me/consents/synthetic-render", Ct);
        Assert.Equal(0, (await initial.Content.ReadFromJsonAsync<ConsentState>(Ct))!.Revision);
        using var otherRevoke = await otherClient.PostJsonAsync(
            "/api/v1/me/consents/synthetic-render/revocations",
            new { id, version = "test-v1", textReference = "synthetic-text-1", ownerId = _ownerId }, Ct);
        Assert.Equal(HttpStatusCode.Created, otherRevoke.StatusCode);
        Assert.Equal(1, (await otherRevoke.Content.ReadFromJsonAsync<Receipt>(Ct))!.Revision);
        Assert.True((await ReadStateAsync()).IsGranted);
        using var export = await otherClient.GetAsync("/api/v1/me/export", Ct);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        await using var zipStream = await export.Content.ReadAsStreamAsync(Ct);
        using var zip = new ZipArchive(zipStream);
        using var history = await JsonDocument.ParseAsync(
            zip.GetEntry("entities/ConsentTransition.json")!.Open(), cancellationToken: Ct);
        var only = Assert.Single(history.RootElement.EnumerateArray());
        Assert.Equal("revoke", only.GetProperty("action").GetString());
        Assert.NotEqual(_ownerId, only.GetProperty("ownerId").GetGuid());
    }

    [Fact]
    public async Task Deletion_limited_session_cannot_access_the_registry()
    {
        await UseSyntheticDefinitionAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
            await db.Users.Where(u => u.Id == _ownerId).ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.DeletionRequestedAtUtc, (DateTime?)ServerNow.UtcDateTime)
                .SetProperty(u => u.DeletionScheduledForUtc, (DateTime?)ServerNow.UtcDateTime.AddDays(30)), Ct);
        }

        using var limited = CreateClient(_factory);
        using var login = await limited.LoginAsync(Email, Password, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var read = await limited.GetAsync("/api/v1/me/consents/synthetic-render", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        using var write = await limited.PostJsonAsync("/api/v1/me/consents/synthetic-render/revocations",
            new { id = NewId(), version = "test-v1", textReference = "synthetic-text-1" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    private async Task<RoamlyClient> CreateOtherOwnerClientAsync()
    {
        const string otherEmail = "consent-other@example.test";
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await manager.CreateAsync(new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = otherEmail, Email = otherEmail, EmailConfirmed = true,
        }, Password);
        Assert.True(result.Succeeded);
        var other = CreateClient(_factory);
        using var login = await other.LoginAsync(otherEmail, Password, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return other;
    }

    private Task<HttpResponseMessage> PostAsync(
        string purpose, string action, Guid id, string version = "test-v1", string textReference = "synthetic-text-1") =>
        _client.PostJsonAsync($"/api/v1/me/consents/{purpose}/{action}",
            new { id, version, textReference }, Ct);

    private async Task<ConsentState> ReadStateAsync(string purpose = "synthetic-render")
    {
        using var read = await _client.GetAsync($"/api/v1/me/consents/{purpose}", Ct);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        return (await read.Content.ReadFromJsonAsync<ConsentState>(Ct))!;
    }

    private async Task UseSyntheticDefinitionAsync(string version = "test-v1", string textReference = "synthetic-text-1")
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        _factory = NewFactory(services =>
        {
            services.RemoveAll<ConsentPurposeDefinition>();
            services.AddSingleton(new ConsentPurposeDefinition("synthetic-render", version, textReference, true));
        });
        _client = CreateClient(_factory);
        using var login = await _client.LoginAsync(Email, Password, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private RoamlyApiFactory NewFactory(Action<IServiceCollection>? configure = null) =>
        new(_database.ConnectionString,
            journalImagesAzuriteConnectionString: sqlServer.JournalImageStorageConnectionString,
            configureTestServices: services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedClock());
                services.AddDbContext<RoamlyDbContext>((_, options) => options.AddInterceptors(_locks));
                configure?.Invoke(services);
            });

    private async Task SetAccountRestrictedAsync(bool passwordChange)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
        await db.Users.Where(u => u.Id == _ownerId).ExecuteUpdateAsync(setters => setters
            .SetProperty(u => u.RequiresPasswordChange, passwordChange)
            .SetProperty(u => u.DeletionRequestedAtUtc, passwordChange ? null : (DateTime?)ServerNow.UtcDateTime)
            .SetProperty(u => u.DeletionScheduledForUtc, passwordChange ? null : (DateTime?)ServerNow.UtcDateTime.AddDays(-1)), Ct);
    }

    private static RoamlyClient CreateClient(RoamlyApiFactory factory) =>
        new(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), HandleCookies = true,
        }));

    private Guid NewId() => _factory.Services.GetRequiredService<IIdGenerator>().NewId();

    private sealed record ConsentState(
        string Purpose, string Version, string TextReference, bool IsActive, bool IsGranted, long Revision);

    private sealed record Receipt(
        Guid Id, string Purpose, string Version, string TextReference, string Action, long Revision, DateTime RecordedAtUtc);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ServerNow;
    }
}
