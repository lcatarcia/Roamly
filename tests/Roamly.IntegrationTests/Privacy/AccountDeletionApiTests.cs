using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roamly.Infrastructure.Identity;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Privacy;

[Collection(DatabaseCollection.Name)]
public sealed class AccountDeletionApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Email = "pending@example.test";
    private const string NormalEmail = "normal@example.test";
    private const string Password = "Test!Password123";
    private static readonly DateTime RequestedAtUtc = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset ClockNow = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _normalUserId = Guid.NewGuid();
    private readonly FixedTimeProvider _clock = new(ClockNow);
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
            });
        _client = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        await CreatePendingUserAsync();
        await CreateUserAsync(_normalUserId, NormalEmail);
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
    public async Task pending_account_login_returns_a_recognizable_limited_session()
    {
        using var response = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal("limited", body.RootElement.GetProperty("sessionMode").GetString());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains(
            ".AspNetCore.Identity.Application=",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task wrong_password_for_pending_account_does_not_reveal_deletion_state()
    {
        using var pending = await _client.LoginAsync(
            Email,
            "Wrong!Password123",
            TestContext.Current.CancellationToken);
        using var unknown = await _client.LoginAsync(
            "unknown@example.test",
            "Wrong!Password123",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, pending.StatusCode);
        Assert.Equal(unknown.StatusCode, pending.StatusCode);
        using var pendingBody = JsonDocument.Parse(await pending.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        using var unknownBody = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(pendingBody.RootElement.GetProperty("type").GetString(),
            unknownBody.RootElement.GetProperty("type").GetString());
        Assert.Equal(pendingBody.RootElement.GetProperty("title").GetString(),
            unknownBody.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("deletion", pendingBody.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionMode", pendingBody.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("2026-10-31", pendingBody.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task previously_issued_normal_cookie_is_blocked_after_deletion_request_commits()
    {
        using var login = await _client.LoginAsync(
            NormalEmail,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var scheduled = await _client.PostJsonAsync(
            "/api/v1/me/deletion",
            new { password = Password },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, scheduled.StatusCode);

        using var profile = await _client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
    }

    [Fact]
    public async Task deletion_request_rejects_a_stale_identity_update_that_would_clear_the_schedule()
    {
        var staleUser = await LoadUserAsync(_normalUserId);
        using var login = await _client.LoginAsync(
            NormalEmail,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var scheduled = await _client.PostJsonAsync(
            "/api/v1/me/deletion",
            new { password = Password },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, scheduled.StatusCode);

        await AssertStaleUpdateRejectedAndDeletionStatePreservedAsync(
            staleUser,
            ClockNow.UtcDateTime,
            ClockNow.AddDays(30).UtcDateTime);
    }

    [Fact]
    public async Task deletion_revocation_rejects_a_stale_identity_update_that_would_restore_the_schedule()
    {
        var staleUser = await LoadUserAsync(_userId);
        using var login = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var revoked = await _client.DeleteWithCsrfAsync(
            "/api/v1/me/deletion",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        await AssertStaleUpdateRejectedAndDeletionStatePreservedAsync(staleUser, null, null);
    }

    [Fact]
    public async Task limited_session_cannot_read_ordinary_api_but_can_revoke_and_stays_limited()
    {
        using var login = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var profileBeforeRevocation = await _client.GetAsync(
            "/api/v1/me",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, profileBeforeRevocation.StatusCode);

        using var revocation = await _client.DeleteWithCsrfAsync(
            "/api/v1/me/deletion",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, revocation.StatusCode);

        using var profileAfterRevocation = await _client.GetAsync(
            "/api/v1/me",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, profileAfterRevocation.StatusCode);

        _clock.Advance(TimeSpan.FromMinutes(6));
        using var renewedSession = await _client.GetAsync(
            "/api/v1/me",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, renewedSession.StatusCode);
    }

    [Fact]
    public async Task repeated_delete_without_if_match_always_returns_no_content()
    {
        using var login = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var first = await _client.DeleteWithCsrfAsync(
            "/api/v1/me/deletion",
            TestContext.Current.CancellationToken);
        using var second = await _client.DeleteWithCsrfAsync(
            "/api/v1/me/deletion",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.False(first.RequestMessage!.Headers.Contains("If-Match"));
        Assert.False(second.RequestMessage!.Headers.Contains("If-Match"));
    }

    [Fact]
    public async Task deletion_request_rejects_an_invalid_reauthentication_password()
    {
        using var login = await _client.LoginAsync(
            NormalEmail,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using (var loginBody = JsonDocument.Parse(await login.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken)))
        {
            Assert.False(loginBody.RootElement.TryGetProperty("sessionMode", out _));
        }

        using var response = await _client.PostJsonAsync(
            "/api/v1/me/deletion",
            new { password = "Wrong!Password123" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var profile = await _client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task request_uses_persisted_server_clock_and_thirty_day_grace_without_resetting_deadline()
    {
        using var login = await _client.LoginAsync(
            NormalEmail,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var response = await _client.PostJsonAsync(
            "/api/v1/me/deletion",
            new { password = Password },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scheduled = await response.Content.ReadFromJsonAsync<DeletionScheduleResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(scheduled);
        Assert.Equal(ClockNow.UtcDateTime, scheduled.DeletionRequestedAtUtc);
        Assert.Equal(ClockNow.AddDays(30).UtcDateTime, scheduled.DeletionScheduledForUtc);

        using var replay = await _client.PostJsonAsync(
            "/api/v1/me/deletion",
            new { password = Password },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(_normalUserId.ToString());
        Assert.NotNull(user);
        Assert.Equal(ClockNow.UtcDateTime, user.DeletionRequestedAtUtc);
        Assert.Equal(ClockNow.AddDays(30).UtcDateTime, user.DeletionScheduledForUtc);
    }

    [Fact]
    public async Task both_csrf_barriers_are_required_for_non_safe_requests()
    {
        using var login = await _client.LoginAsync(
            NormalEmail,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var missingCustomHeader = await _client.PostJsonWithCsrfHeadersAsync(
            "/api/v1/auth/logout",
            new { },
            includeCustomRequestHeader: false,
            includeAntiforgeryHeader: true,
            TestContext.Current.CancellationToken);
        using var missingAntiforgeryHeader = await _client.PostJsonWithCsrfHeadersAsync(
            "/api/v1/auth/logout",
            new { },
            includeCustomRequestHeader: true,
            includeAntiforgeryHeader: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, missingCustomHeader.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingAntiforgeryHeader.StatusCode);
    }

    private async Task CreatePendingUserAsync()
    {
        await CreateUserAsync(_userId, Email, pending: true);
    }

    private async Task<ApplicationUser> LoadUserAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        return Assert.IsType<ApplicationUser>(user);
    }

    private async Task AssertStaleUpdateRejectedAndDeletionStatePreservedAsync(
        ApplicationUser staleUser,
        DateTime? expectedRequestedAtUtc,
        DateTime? expectedScheduledForUtc)
    {
        IdentityResult update;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            staleUser.UserName = $"stale-{staleUser.Id:N}@example.test";
            staleUser.Email = staleUser.UserName;
            update = await userManager.UpdateAsync(staleUser);
        }

        Assert.False(update.Succeeded);
        Assert.Contains(update.Errors, error => error.Code == "ConcurrencyFailure");

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationUserManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var persistedUser = await verificationUserManager.FindByIdAsync(staleUser.Id.ToString());
        Assert.NotNull(persistedUser);
        Assert.Equal(expectedRequestedAtUtc, persistedUser.DeletionRequestedAtUtc);
        Assert.Equal(expectedScheduledForUtc, persistedUser.DeletionScheduledForUtc);
    }

    private async Task CreateUserAsync(Guid userId, string email, bool pending = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAtUtc = ClockNow.AddDays(-60).UtcDateTime,
            DeletionRequestedAtUtc = pending ? RequestedAtUtc : null,
            DeletionScheduledForUtc = pending ? RequestedAtUtc.AddDays(30) : null,
        }, Password);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private sealed record DeletionScheduleResponse(
        DateTime DeletionRequestedAtUtc,
        DateTime DeletionScheduledForUtc);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
