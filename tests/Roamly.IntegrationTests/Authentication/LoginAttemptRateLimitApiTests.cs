using System.Net;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Roamly.Infrastructure.Identity;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Authentication;

[Collection(DatabaseCollection.Name)]
public sealed class LoginAttemptRateLimitApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Password = "Test!Password123";
    private static readonly DateTimeOffset InitialTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private readonly MutableTimeProvider _clock = new(InitialTime);
    private readonly TrackingPasswordHasher _passwordHasher = new();
    private DatabaseLease _database = null!;
    private RoamlyApiFactory _factory = null!;
    private RoamlyClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        _database = migrated.Lease;
        await _database.ResetAsync(TestContext.Current.CancellationToken);

        _factory = CreateFactory("Testing");
        _client = CreateClient(_factory);
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
    public async Task failed_account_attempts_are_available_again_after_the_fifteen_minute_window()
    {
        const string email = "window-expiry@example.test";
        var user = await CreateUserAsync(email);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await _client.LoginAsync(
                email,
                "Wrong!Password123",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        _clock.Advance(TimeSpan.FromMinutes(16));
        await ClearIdentityLockoutAsync(user);

        using var retry = await _client.LoginAsync(
            email,
            "Wrong!Password123",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, retry.StatusCode);
    }

    [Fact]
    public async Task successful_logins_count_toward_the_shared_thirty_attempt_ip_window()
    {
        const string email = "ip-window@example.test";
        await CreateUserAsync(email);

        for (var attempt = 0; attempt < 31; attempt++)
        {
            using var response = await _client.LoginAsync(
                email,
                Password,
                TestContext.Current.CancellationToken);

            Assert.Equal(
                attempt < 30 ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests,
                response.StatusCode);
        }
    }

    [Fact]
    public async Task ip_hourly_window_blocks_the_101st_request_across_short_windows()
    {
        const string email = "hour-window@example.test";
        await CreateUserAsync(email);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (attempt > 0 && attempt % 30 == 0)
            {
                _clock.Advance(TimeSpan.FromMinutes(5));
            }

            using var response = await _client.LoginAsync(
                email,
                Password,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var rejected = await _client.LoginAsync(
            email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task two_api_hosts_share_the_same_ip_window()
    {
        const string email = "multi-host@example.test";
        await CreateUserAsync(email);

        await using var secondFactory = CreateFactory("Testing");
        using var secondClient = CreateClient(secondFactory);

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var client = attempt % 2 == 0 ? _client : secondClient;
            using var response = await client.LoginWithForwardedForAsync(
                email,
                Password,
                $"198.51.100.{attempt + 1}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var limited = await secondClient.LoginWithForwardedForAsync(
            email,
            Password,
            "198.51.100.99",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task x_forwarded_for_values_before_the_aca_appended_address_do_not_change_the_ip_bucket()
    {
        const string email = "forwarded-for@example.test";
        await CreateUserAsync(email);

        await using var productionFactory = CreateFactory(Environments.Production);
        using var productionClient = CreateClient(productionFactory);

        for (var attempt = 0; attempt < 31; attempt++)
        {
            var clientSuppliedValues = $"198.51.100.{attempt + 1}";
            using var response = await productionClient.LoginWithForwardedForAsync(
                email,
                Password,
                $"{clientSuppliedValues}, 203.0.113.44",
                TestContext.Current.CancellationToken);
            Assert.Equal(
                attempt < 30 ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests,
                response.StatusCode);
        }
    }

    [Fact]
    public async Task missing_or_invalid_aca_ip_returns_generic_503_before_password_validation()
    {
        const string email = "missing-ip@example.test";
        var user = await CreateUserAsync(email);
        await using var productionFactory = CreateFactory(Environments.Production);
        using var productionClient = CreateClient(productionFactory);

        using var missingIp = await productionClient.LoginWithForwardedForAsync(
            email,
            "Wrong!Password123",
            null,
            TestContext.Current.CancellationToken);
        using var invalidIp = await productionClient.LoginWithForwardedForAsync(
            email,
            "Wrong!Password123",
            "198.51.100.9, not-an-ip",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, missingIp.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, invalidIp.StatusCode);
        Assert.Equal(await ProblemIdentityAsync(missingIp), await ProblemIdentityAsync(invalidIp));
        Assert.Equal(0, await GetAccessFailedCountAsync(user.Id));
    }

    [Fact]
    public async Task sql_counter_failure_returns_generic_503_before_password_validation()
    {
        const string email = "sql-failure@example.test";
        var user = await CreateUserAsync(email);
        await using var contextScope = _factory.Services.CreateAsyncScope();
        var context = contextScope.ServiceProvider.GetRequiredService<Roamly.Infrastructure.Persistence.RoamlyDbContext>();
        await context.Database.ExecuteSqlRawAsync(
            "DROP TABLE [dbo].[LoginAttemptCounters]",
            TestContext.Current.CancellationToken);
        try
        {
            using var response = await _client.LoginAsync(
                email,
                "Wrong!Password123",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(0, await GetAccessFailedCountAsync(user.Id));
            Assert.DoesNotContain(email, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE [dbo].[LoginAttemptCounters] (
                    [BucketKey] char(64) NOT NULL,
                    [CounterKind] varchar(24) NOT NULL CONSTRAINT [DF_LoginAttemptCounters_CounterKind] DEFAULT ('legacy'),
                    [WindowStartUtc] datetime2(3) NOT NULL,
                    [AttemptCount] int NOT NULL,
                    [ExpiresAtUtc] datetime2(3) NOT NULL,
                    CONSTRAINT [PK_LoginAttemptCounters] PRIMARY KEY CLUSTERED ([BucketKey], [WindowStartUtc])
                );
                CREATE INDEX [IX_LoginAttemptCounters_ExpiresAtUtc]
                    ON [dbo].[LoginAttemptCounters] ([ExpiresAtUtc]);
                CREATE INDEX [IX_LoginAttemptCounters_CounterKind_ExpiresAtUtc]
                    ON [dbo].[LoginAttemptCounters] ([CounterKind], [ExpiresAtUtc]);
                """,
                TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task identity_locks_account_after_ten_consecutive_password_failures()
    {
        const string email = "identity-lockout@example.test";
        var user = await CreateUserAsync(email);

        for (var attempt = 0; attempt < 9; attempt++)
        {
            using var response = await _client.LoginAsync(
                email,
                "Wrong!Password123",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        await using (var beforeThresholdScope = _factory.Services.CreateAsyncScope())
        {
            var beforeThresholdManager = beforeThresholdScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var userBeforeThreshold = await beforeThresholdManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(userBeforeThreshold);
            Assert.False(await beforeThresholdManager.IsLockedOutAsync(userBeforeThreshold));
        }

        using (var tenthFailure = await _client.LoginAsync(
                   email,
                   "Wrong!Password123",
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, tenthFailure.StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var lockedUser = await userManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(lockedUser);
        Assert.True(await userManager.IsLockedOutAsync(lockedUser));
        var lockoutEnd = await userManager.GetLockoutEndDateAsync(lockedUser);
        Assert.NotNull(lockoutEnd);
        Assert.InRange(
            lockoutEnd.Value - TimeProvider.System.GetUtcNow(),
            TimeSpan.FromMinutes(4),
            TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task successful_login_does_not_consume_an_account_failure_slot()
    {
        const string email = "success-does-not-count@example.test";
        await CreateUserAsync(email);

        using var success = await _client.LoginAsync(email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var failure = await _client.LoginAsync(
                email,
                "Wrong!Password123",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        using var blocked = await _client.LoginAsync(
            email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
    }

    [Fact]
    public async Task account_case_variants_share_the_identity_normalized_failure_bucket()
    {
        const string email = "normalized-account@example.test";
        await CreateUserAsync(email);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suppliedEmail = attempt % 2 == 0 ? email : email.ToUpperInvariant();
            using var response = await _client.LoginAsync(
                suppliedEmail,
                "Wrong!Password123",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var blocked = await _client.LoginAsync(
            email.ToUpperInvariant(),
            "Wrong!Password123",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
    }

    [Fact]
    public async Task nonexistent_and_wrong_password_responses_do_not_reveal_account_existence()
    {
        const string email = "generic-response@example.test";
        await CreateUserAsync(email);

        using var wrongPassword = await _client.LoginAsync(
            email,
            "Wrong!Password123",
            TestContext.Current.CancellationToken);
        using var missingAccount = await _client.LoginAsync(
            "absent@example.test",
            "Wrong!Password123",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missingAccount.StatusCode);
        Assert.Equal(await ProblemIdentityAsync(wrongPassword), await ProblemIdentityAsync(missingAccount));
    }

    [Fact]
    public async Task repeated_unknown_usernames_are_limited_only_by_the_ip_bucket()
    {
        const string unknownEmail = "arbitrary-unknown@example.test";

        for (var attempt = 0; attempt < 31; attempt++)
        {
            using var response = await _client.LoginAsync(
                unknownEmail,
                "Wrong!Password123",
                TestContext.Current.CancellationToken);

            Assert.Equal(
                attempt < 30 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests,
                response.StatusCode);
        }
    }

    [Fact]
    public async Task ip_limited_request_does_not_query_identity()
    {
        const string unknownEmail = "ip-limited-no-identity@example.test";
        var identityLookupTracker = new IdentityUserLookupTracker();
        await using var trackedFactory = CreateFactory(
            "Testing",
            identityUserLookupTracker: identityLookupTracker);
        using var trackedClient = CreateClient(trackedFactory);

        for (var attempt = 0; attempt < 30; attempt++)
        {
            using var response = await trackedClient.LoginAsync(
                unknownEmail,
                "Wrong!Password123",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        Assert.Equal(30, identityLookupTracker.LookupCount);
        identityLookupTracker.Reset();

        using var limited = await trackedClient.LoginAsync(
            unknownEmail,
            "Wrong!Password123",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(0, identityLookupTracker.LookupCount);
    }

    [Fact]
    public async Task account_failure_bucket_follows_identity_user_id_after_username_change()
    {
        const string originalEmail = "identity-bucket-before@example.test";
        const string newEmail = "identity-bucket-after@example.test";
        var user = await CreateUserAsync(originalEmail);

        for (var attempt = 0; attempt < 9; attempt++)
        {
            using var response = await _client.LoginAsync(
                originalEmail,
                "Wrong!Password123",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var currentUser = await userManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(currentUser);
            currentUser.UserName = newEmail;
            currentUser.Email = newEmail;
            Assert.True((await userManager.UpdateAsync(currentUser)).Succeeded);
        }

        using var tenthFailure = await _client.LoginAsync(
            newEmail,
            "Wrong!Password123",
            TestContext.Current.CancellationToken);
        using var blocked = await _client.LoginAsync(
            newEmail,
            "Wrong!Password123",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, tenthFailure.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
    }

    [Fact]
    public async Task unknown_account_verifies_a_cached_dummy_hash_without_changing_real_lockout_state()
    {
        const string existingEmail = "known-for-dummy-hash@example.test";
        var existingUser = await CreateUserAsync(existingEmail);
        _passwordHasher.ResetCounts();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await _client.LoginAsync(
                $"unknown-{attempt}@example.test",
                "Wrong!Password123",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        Assert.Equal(2, _passwordHasher.VerifyCount);
        Assert.Equal(1, _passwordHasher.HashCount);
        Assert.Equal(0, await GetAccessFailedCountAsync(existingUser.Id));

        using var knownAccountResponse = await _client.LoginAsync(
            existingEmail,
            "Wrong!Password123",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, knownAccountResponse.StatusCode);
        Assert.Equal(3, _passwordHasher.VerifyCount);
        Assert.Equal(1, _passwordHasher.HashCount);
        Assert.Equal(1, await GetAccessFailedCountAsync(existingUser.Id));
    }

    [Fact]
    public async Task account_throttling_uses_the_generic_invalid_credentials_response()
    {
        const string existingEmail = "account-throttle-response@example.test";
        var existingUser = await CreateUserAsync(existingEmail);
        _passwordHasher.ResetCounts();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await _client.LoginAsync(
                existingEmail,
                "Wrong!Password123",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var accountThrottled = await _client.LoginAsync(
            existingEmail,
            "Wrong!Password123",
            TestContext.Current.CancellationToken);
        using var unknownAccount = await _client.LoginAsync(
            "unregistered-account@example.test",
            "Wrong!Password123",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, accountThrottled.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownAccount.StatusCode);
        Assert.Equal(await ProblemIdentityAsync(accountThrottled), await ProblemIdentityAsync(unknownAccount));
        Assert.False(accountThrottled.Headers.Contains("Retry-After"));
        Assert.Equal(12, _passwordHasher.VerifyCount);
        Assert.Equal(1, _passwordHasher.HashCount);
        Assert.Equal(0, await GetAccessFailedCountAsync(existingUser.Id));
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var lockedUser = await userManager.FindByIdAsync(existingUser.Id.ToString());
        Assert.NotNull(lockedUser);
        Assert.True(await userManager.IsLockedOutAsync(lockedUser));
    }

    [Fact]
    public async Task concurrent_account_reservations_never_exceed_ten_on_sql_server()
    {
        var requests = Enumerable.Range(0, 40).Select(async attempt =>
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var limiter = scope.ServiceProvider.GetRequiredService<Roamly.Common.Security.ILoginAttemptRateLimiter>();
            var result = await limiter.BeginAccountAsync(
                Guid.Parse("477c90b5-1e8b-4ad9-932f-5a68b6fe9b0f"),
                TestContext.Current.CancellationToken);
            if (result.Reservation is { } reservation)
            {
                await using (reservation)
                {
                    Assert.True(await reservation.CompleteAsync(
                        credentialsInvalid: true,
                        TestContext.Current.CancellationToken));
                }
            }

            return result.Status;
        });

        var results = await Task.WhenAll(requests);
        Assert.Equal(10, results.Count(status => status == Roamly.Common.Security.LoginAttemptLimitStatus.Allowed));
        Assert.Equal(30, results.Count(status => status == Roamly.Common.Security.LoginAttemptLimitStatus.AccountTooManyAttempts));
        Assert.DoesNotContain(results, status => status == Roamly.Common.Security.LoginAttemptLimitStatus.Unavailable);
    }

    [Fact]
    public async Task concurrent_ip_requests_never_exceed_thirty_on_sql_server()
    {
        var requests = Enumerable.Range(0, 40).Select(async attempt =>
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var limiter = scope.ServiceProvider.GetRequiredService<Roamly.Common.Security.ILoginAttemptRateLimiter>();
            var result = await limiter.CheckIpAsync(
                "203.0.113.77",
                TestContext.Current.CancellationToken);
            return result;
        });

        var results = await Task.WhenAll(requests);
        Assert.Equal(30, results.Count(status => status == Roamly.Common.Security.LoginAttemptLimitStatus.Allowed));
        Assert.Equal(10, results.Count(status => status == Roamly.Common.Security.LoginAttemptLimitStatus.IpTooManyAttempts));
        Assert.DoesNotContain(results, status => status == Roamly.Common.Security.LoginAttemptLimitStatus.Unavailable);
    }

    [Fact]
    public async Task concurrent_new_ips_cannot_exceed_ten_thousand_active_identities()
    {
        await SeedIpAdmissionRowsAsync(9_990);

        var requests = Enumerable.Range(0, 40).Select(async attempt =>
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var limiter = scope.ServiceProvider.GetRequiredService<Roamly.Common.Security.ILoginAttemptRateLimiter>();
            var ipAddress = new IPAddress([198, 19, (byte)(attempt / 256), (byte)attempt]);
            return await limiter.CheckIpAsync(
                ipAddress.ToString(),
                TestContext.Current.CancellationToken);
        });

        var results = await Task.WhenAll(requests);
        Assert.Equal(10, results.Count(status => status == Roamly.Common.Security.LoginAttemptLimitStatus.Allowed));
        Assert.Equal(30, results.Count(status => status == Roamly.Common.Security.LoginAttemptLimitStatus.Unavailable));
        Assert.DoesNotContain(results, status => status == Roamly.Common.Security.LoginAttemptLimitStatus.IpTooManyAttempts);
        Assert.Equal(10_000, await CountActiveIpAdmissionsAsync());
    }

    [Fact]
    public async Task capacity_rejects_new_ip_before_password_check_but_keeps_admitted_ip_allowed()
    {
        const string email = "ip-capacity@example.test";
        var user = await CreateUserAsync(email);
        _passwordHasher.ResetCounts();

        using var firstRequest = await _client.LoginAsync(
            email,
            "Wrong!Password123",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, firstRequest.StatusCode);

        await MarkCurrentIpCountersAsLegacyAsync();
        await SeedIpAdmissionRowsAsync(9_999);
        using var admittedIpRequest = await _client.LoginAsync(
            email,
            "Wrong!Password123",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, admittedIpRequest.StatusCode);

        var rejectedHasher = new TrackingPasswordHasher();
        var identityLookupTracker = new IdentityUserLookupTracker();
        await using var newIpFactory = CreateFactory(
            "Testing",
            IPAddress.Parse("198.18.0.11"),
            rejectedHasher,
            identityLookupTracker);
        using var newIpClient = CreateClient(newIpFactory);
        using var rejectedRequest = await newIpClient.LoginAsync(
            email,
            "Wrong!Password123",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, rejectedRequest.StatusCode);
        Assert.Equal(0, rejectedHasher.VerifyCount);
        Assert.Equal(0, identityLookupTracker.LookupCount);
        Assert.Equal(2, await GetAccessFailedCountAsync(user.Id));
    }

    private RoamlyApiFactory CreateFactory(
        string environmentName,
        IPAddress? controlledTestIpAddress = null,
        TrackingPasswordHasher? passwordHasher = null,
        IdentityUserLookupTracker? identityUserLookupTracker = null)
    {
        return new RoamlyApiFactory(
            _database.ConnectionString,
            environmentName,
            services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_clock);
                services.RemoveAll<IPasswordHasher<ApplicationUser>>();
                services.AddSingleton<IPasswordHasher<ApplicationUser>>(passwordHasher ?? _passwordHasher);
                if (identityUserLookupTracker is not null)
                {
                    services.AddDbContext<Roamly.Infrastructure.Persistence.RoamlyDbContext>(
                        (_, options) => options.AddInterceptors(identityUserLookupTracker));
                }
            },
            controlledTestIpAddress ?? IPAddress.Parse("198.18.0.10"));
    }

    private sealed class IdentityUserLookupTracker : DbCommandInterceptor
    {
        private int _lookupCount;

        public int LookupCount => Volatile.Read(ref _lookupCount);

        public void Reset() => Interlocked.Exchange(ref _lookupCount, 0);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            CountIdentityLookup(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CountIdentityLookup(command);
            return ValueTask.FromResult(result);
        }

        private void CountIdentityLookup(DbCommand command)
        {
            if (command.CommandText.Contains("[AspNetUsers]", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _lookupCount);
            }
        }
    }

    private async Task SeedIpAdmissionRowsAsync(int count)
    {
        if (count == 0)
        {
            return;
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var windowStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var expiresAt = windowStart.AddHours(1);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Roamly.Infrastructure.Persistence.RoamlyDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            ;WITH Numbers AS
            (
                SELECT TOP ({count}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [Number]
                FROM sys.all_objects AS firstObject
                CROSS JOIN sys.all_objects AS secondObject
            )
            INSERT INTO [dbo].[LoginAttemptCounters]
                ([BucketKey], [CounterKind], [WindowStartUtc], [AttemptCount], [ExpiresAtUtc])
            SELECT
                RIGHT(REPLICATE('0', 64) + CONVERT(varchar(64), [Number]), 64),
                'ip-admission',
                CAST({windowStart} AS datetime2(3)),
                1,
                CAST({expiresAt} AS datetime2(3))
            FROM Numbers;
            """,
            TestContext.Current.CancellationToken);
    }

    private async Task<int> CountActiveIpAdmissionsAsync()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Roamly.Infrastructure.Persistence.RoamlyDbContext>();
        return await context.LoginAttemptCounters
            .CountAsync(
                counter => counter.CounterKind == "ip-admission" && counter.ExpiresAtUtc > now,
                TestContext.Current.CancellationToken);
    }

    private async Task MarkCurrentIpCountersAsLegacyAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Roamly.Infrastructure.Persistence.RoamlyDbContext>();
        await context.Database.ExecuteSqlRawAsync(
            """
            UPDATE [dbo].[LoginAttemptCounters]
            SET [CounterKind] = 'legacy'
            WHERE [CounterKind] IN ('ip-5m', 'ip-1h');
            DELETE FROM [dbo].[LoginAttemptCounters]
            WHERE [CounterKind] = 'ip-admission';
            """,
            TestContext.Current.CancellationToken);
    }

    private static RoamlyClient CreateClient(RoamlyApiFactory factory)
    {
        return new RoamlyClient(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
    }

    private async Task<ApplicationUser> CreateUserAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAtUtc = InitialTime.UtcDateTime,
        };
        var result = await userManager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
        return user;
    }

    private async Task ClearIdentityLockoutAsync(ApplicationUser user)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var trackedUser = await userManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(trackedUser);
        Assert.True((await userManager.SetLockoutEndDateAsync(trackedUser, null)).Succeeded);
        Assert.True((await userManager.ResetAccessFailedCountAsync(trackedUser)).Succeeded);
    }

    private async Task<int> GetAccessFailedCountAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        return await userManager.GetAccessFailedCountAsync(user);
    }

    private static async Task<(string? Type, string? Title, int? Status)> ProblemIdentityAsync(
        HttpResponseMessage response)
    {
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(payload);
        var root = payload.RootElement;
        var type = root.TryGetProperty("type", out var typeValue) ? typeValue.GetString() : null;
        var title = root.TryGetProperty("title", out var titleValue) ? titleValue.GetString() : null;
        int? status = root.TryGetProperty("status", out var statusValue) ? statusValue.GetInt32() : null;
        return (type, title, status);
    }

    private sealed class MutableTimeProvider(DateTimeOffset initialTime) : TimeProvider
    {
        private DateTimeOffset _now = initialTime;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    }

    private sealed class TrackingPasswordHasher : IPasswordHasher<ApplicationUser>
    {
        private readonly PasswordHasher<ApplicationUser> _inner = new();
        private int _hashCount;
        private int _verifyCount;

        public int HashCount => Volatile.Read(ref _hashCount);

        public int VerifyCount => Volatile.Read(ref _verifyCount);

        public string HashPassword(ApplicationUser user, string password)
        {
            Interlocked.Increment(ref _hashCount);
            return _inner.HashPassword(user, password);
        }

        public PasswordVerificationResult VerifyHashedPassword(
            ApplicationUser user,
            string hashedPassword,
            string providedPassword)
        {
            Interlocked.Increment(ref _verifyCount);
            return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }

        public void ResetCounts()
        {
            Interlocked.Exchange(ref _hashCount, 0);
            Interlocked.Exchange(ref _verifyCount, 0);
        }
    }
}
