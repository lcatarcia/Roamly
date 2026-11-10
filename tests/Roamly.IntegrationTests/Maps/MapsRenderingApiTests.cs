using System.Net;
using System.Net.Http.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roamly.Api.Features.Identity.Consents;
using Roamly.Api.Features.Maps;
using Roamly.Common;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Maps;

[Collection(DatabaseCollection.Name)]
public sealed class MapsRenderingApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Password = "Test!Password123";
    private const string Email = "maps-render@example.test";
    private const string AccountClientId = "bf15a837-935e-43cf-8868-1ad104b83441";
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _owner = Guid.NewGuid();
    private readonly FakeCredential _credential = new();
    private RoamlyApiFactory _factory = null!;
    private RoamlyClient _client = null!;
    private DatabaseLease _database = null!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        _database = migrated.Lease;
        await _database.ResetAsync(Ct);
        _factory = NewFactory();
        _client = Client(_factory);
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await manager.CreateAsync(new ApplicationUser
        {
            Id = _owner, UserName = Email, Email = Email, EmailConfirmed = true,
        }, Password);
        Assert.True(result.Succeeded);
        using var login = await _client.LoginAsync(Email, Password, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Configured_host_exposes_account_id_not_credential_and_activates_versioned_definition()
    {
        using var response = await _client.GetAsync("/api/v1/maps/config", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var config = await response.Content.ReadFromJsonAsync<MapsConfigResponse>(Ct);
        Assert.Equal(new MapsConfigResponse(true, AccountClientId, "eu.atlas.microsoft.com"), config);
        using var consent = await _client.GetAsync("/api/v1/me/consents/azure-maps-render", Ct);
        var state = await consent.Content.ReadFromJsonAsync<ConsentStateResponse>(Ct);
        Assert.True(state!.IsActive);
        Assert.False(state.IsGranted);
        Assert.Equal("v1", state.Version);
        Assert.Equal(0, _credential.Calls);
    }

    [Fact]
    public async Task Disabled_host_keeps_draft_and_never_calls_Entra()
    {
        await using var factory = NewFactory(false);
        using var client = Client(factory);
        using var login = await client.LoginAsync(Email, Password, Ct);
        using var config = await client.GetAsync("/api/v1/maps/config", Ct);
        Assert.False((await config.Content.ReadFromJsonAsync<MapsConfigResponse>(Ct))!.IsConfigured);
        using var consent = await client.GetAsync("/api/v1/me/consents/azure-maps-render", Ct);
        Assert.Equal("DRAFT-1", (await consent.Content.ReadFromJsonAsync<ConsentStateResponse>(Ct))!.Version);
        using var token = await client.PostJsonAsync("/api/v1/maps/token", new { }, Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, token.StatusCode);
        Assert.Equal(0, _credential.Calls);
    }

    [Fact]
    public async Task Token_needs_cookie_and_CSRF_but_not_a_consent_transition()
    {
        using var anonymous = Client(_factory);
        using var noCookie = await anonymous.PostJsonAsync("/api/v1/maps/token", new { }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, noCookie.StatusCode);
        using var noCsrf = await _client.PostJsonWithCsrfHeadersAsync(
            "/api/v1/maps/token", new { }, true, false, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        Assert.Equal(0, _credential.Calls);
        using var noGrant = await TokenAsync();
        Assert.Equal(HttpStatusCode.OK, noGrant.StatusCode);
        Assert.Equal(1, _credential.Calls);
        using var consent = await _client.GetAsync("/api/v1/me/consents/azure-maps-render", Ct);
        Assert.Equal(0, (await consent.Content.ReadFromJsonAsync<ConsentStateResponse>(Ct))!.Revision);
    }

    [Fact]
    public async Task Consented_personal_rendering_works_without_inactive_evidence_gate_and_is_not_cached()
    {
        await TransitionAsync(true);
        using var token = await TokenAsync();
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        Assert.Equal("no-store", token.Headers.CacheControl?.ToString());
        Assert.Equal(new MapsTokenResponse("fake-render-token", Now.AddMinutes(30)),
            await token.Content.ReadFromJsonAsync<MapsTokenResponse>(Ct));
        Assert.Equal(1, _credential.Calls);
        Assert.Equal(["https://atlas.microsoft.com/.default"], _credential.Scopes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Restricted_account_does_not_acquire_token(bool passwordChange)
    {
        await TransitionAsync(true);
        await RestrictAsync(passwordChange);
        using var token = await TokenAsync();
        Assert.Equal(passwordChange ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, token.StatusCode);
        Assert.Equal(0, _credential.Calls);
    }

    [Fact]
    public async Task Persisted_economic_suspension_still_denies_without_remote_call()
    {
        await TransitionAsync(true);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
        db.MapsEconomicStates.Add(new MapsEconomicState
        {
            ResourceId = "synthetic-resource", Revision = 1, SuspendedAtUtc = Now.UtcDateTime,
            SuspensionReason = "ThresholdReached", PeriodStartUtc = Now.UtcDateTime,
            PeriodEndUtc = Now.AddMonths(1).UtcDateTime,
            AlertPending = true, ReportedAmountEur = 1, ObservedAtUtc = Now.UtcDateTime,
            CoveredThroughUtc = Now.UtcDateTime,
        });
        await db.SaveChangesAsync(Ct);
        using var token = await TokenAsync();
        Assert.Equal(HttpStatusCode.Forbidden, token.StatusCode);
        Assert.Equal(0, _credential.Calls);
    }

    [Fact]
    public async Task Identity_failure_is_explicit_and_never_leaks_provider_exception()
    {
        await TransitionAsync(true);
        _credential.Acquire = () => throw new AuthenticationFailedException("sensitive-provider-diagnostic");
        using var token = await TokenAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, token.StatusCode);
        Assert.DoesNotContain("sensitive-provider", await token.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historical_consent_changes_do_not_gate_personal_rendering(bool regrant)
    {
        await TransitionAsync(true);
        _credential.Acquire = async () =>
        {
            await TransitionAsync(false);
            if (regrant) await TransitionAsync(true);
            return new AccessToken("personal-render-token", Now.AddMinutes(30));
        };
        using var token = await TokenAsync();
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        Assert.Equal("personal-render-token", (await token.Content.ReadFromJsonAsync<MapsTokenResponse>(Ct))!.AccessToken);
        _credential.Acquire = () => Task.FromResult(new AccessToken("next-render-token", Now.AddMinutes(30)));
        using var next = await TokenAsync();
        Assert.Equal(HttpStatusCode.OK, next.StatusCode); // Existing granted/revoked state is irrelevant too.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Account_restriction_during_network_call_prevents_token_release(bool passwordChange)
    {
        await TransitionAsync(true);
        _credential.Acquire = async () =>
        {
            await RestrictAsync(passwordChange);
            return new AccessToken("must-not-release", Now.AddMinutes(30));
        };
        using var token = await TokenAsync();
        Assert.Equal(HttpStatusCode.Forbidden, token.StatusCode);
        Assert.DoesNotContain("must-not-release", await token.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Economic_suspension_during_network_call_prevents_token_release()
    {
        _credential.Acquire = async () =>
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
            db.MapsEconomicStates.Add(new MapsEconomicState
            {
                ResourceId = "synthetic-resource", Revision = 1, SuspendedAtUtc = Now.UtcDateTime,
                SuspensionReason = "ThresholdReached", PeriodStartUtc = Now.UtcDateTime,
                PeriodEndUtc = Now.AddMonths(1).UtcDateTime, AlertPending = true,
                ReportedAmountEur = 1, ObservedAtUtc = Now.UtcDateTime, CoveredThroughUtc = Now.UtcDateTime,
            });
            await db.SaveChangesAsync(Ct);
            return new AccessToken("must-not-release", Now.AddMinutes(30));
        };
        using var token = await TokenAsync();
        Assert.Equal(HttpStatusCode.Forbidden, token.StatusCode);
        Assert.DoesNotContain("must-not-release", await token.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    private Task<HttpResponseMessage> TokenAsync() =>
        _client.PostJsonAsync("/api/v1/maps/token", new { }, Ct);

    private async Task TransitionAsync(bool grant)
    {
        var definition = ConsentPurposeDefinition.AzureMapsV1;
        using var response = await _client.PostJsonAsync(
            "/api/v1/me/consents/azure-maps-render/" + (grant ? "grants" : "revocations"),
            new { id = _factory.Services.GetRequiredService<IIdGenerator>().NewId(),
                definition.Version, definition.TextReference }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task RestrictAsync(bool passwordChange)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
        await db.Users.Where(u => u.Id == _owner).ExecuteUpdateAsync(setters => setters
            .SetProperty(u => u.RequiresPasswordChange, passwordChange)
            .SetProperty(u => u.DeletionRequestedAtUtc, passwordChange ? null : (DateTime?)Now.UtcDateTime)
            .SetProperty(u => u.DeletionScheduledForUtc, passwordChange ? null : (DateTime?)Now.AddDays(30).UtcDateTime), Ct);
    }

    private RoamlyApiFactory NewFactory(bool enabled = true) => new(_database.ConnectionString,
        journalImagesAzuriteConnectionString: sqlServer.JournalImageStorageConnectionString,
        configureTestServices: services =>
        {
            services.Configure<MapsRenderOptions>(options =>
            {
                options.Enabled = enabled;
                options.AccountClientId = AccountClientId;
            });
            services.RemoveAll<TokenCredential>();
            services.AddKeyedSingleton<TokenCredential>("MapsRendering", _credential);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock());
        });

    private static RoamlyClient Client(RoamlyApiFactory factory) => new(factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true }));

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeCredential : TokenCredential
    {
        public int Calls { get; private set; }
        public string[] Scopes { get; private set; } = [];
        public Func<Task<AccessToken>> Acquire { get; set; } =
            () => Task.FromResult(new AccessToken("fake-render-token", Now.AddMinutes(30)));
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public override async ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Calls++;
            Scopes = requestContext.Scopes;
            return await Acquire();
        }
    }
}
