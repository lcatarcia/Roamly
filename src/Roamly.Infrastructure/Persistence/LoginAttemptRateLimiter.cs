using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Roamly.Common.Security;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Rate limiter SQL condiviso. L'app lock distribuito serializza la creazione e l'UPDATE atomico
/// del bucket; la prenotazione account resta in transazione fino all'esito delle credenziali.
/// </summary>
public sealed partial class LoginAttemptRateLimiter :
    ILoginAttemptRateLimiter,
    ILoginAttemptCounterMaintenance
{
    private const int CleanupBatchSize = 100;
    private const int DailyCleanupBatchSize = 1_000;
    private const int MaximumTrackedIpIdentities = 10_000;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);
    private const string AccountCounterKind = "account";
    private const string FiveMinuteIpCounterKind = "ip-5m";
    private const string HourlyIpCounterKind = "ip-1h";
    private const string IpAdmissionCounterKind = "ip-admission";
    private const string LegacyCounterKind = "legacy";
    private static long _nextCleanupUtcTicks;

    private readonly RoamlyDbContext _context;
    private readonly LoginAttemptRateLimiterOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<LoginAttemptRateLimiter> _logger;
    private readonly byte[] _hmacKey;

    /// <summary>Costruisce il rate limiter SQL.</summary>
    public LoginAttemptRateLimiter(
        RoamlyDbContext context,
        IOptions<LoginAttemptRateLimiterOptions> options,
        TimeProvider clock,
        ILogger<LoginAttemptRateLimiter> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
        _hmacKey = Convert.FromBase64String(_options.HmacSecret!);
    }

    /// <inheritdoc />
    public async Task<LoginAttemptLimitStatus> CheckIpAsync(
        string clientIpAddress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientIpAddress);

        var now = _clock.GetUtcNow().UtcDateTime;
        await TryCleanupExpiredAsync(now, cancellationToken).ConfigureAwait(false);

        try
        {
            if (!await TryAdmitIpAsync(clientIpAddress, now, cancellationToken).ConfigureAwait(false))
            {
                LogIpCapacityExhausted(_logger);
                return LoginAttemptLimitStatus.Unavailable;
            }

            if (!await IsIpWithinLimitsAsync(clientIpAddress, now, cancellationToken).ConfigureAwait(false))
            {
                return LoginAttemptLimitStatus.IpTooManyAttempts;
            }

            return LoginAttemptLimitStatus.Allowed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            LogStorageFailure(_logger);
            return LoginAttemptLimitStatus.Unavailable;
        }
    }

    /// <inheritdoc />
    public async Task<LoginAttemptStartResult> BeginAccountAsync(
        Guid identityUserId,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? accountTransaction = null;
        try
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            accountTransaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);

            var accountWindow = _options.AccountWindow;
            var accountStart = FixedWindowStart(now, accountWindow);
            var accountKey = BucketKey("account", identityUserId.ToString("D"));
            var accountAttempts = await IncrementWindowAsync(
                AccountCounterKind,
                accountKey,
                accountStart,
                accountWindow,
                _options.MaxFailedAttemptsPerAccount,
                cancellationToken).ConfigureAwait(false);

            if (accountAttempts == 0)
            {
                await accountTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                await accountTransaction.DisposeAsync().ConfigureAwait(false);
                accountTransaction = null;
                return new LoginAttemptStartResult(LoginAttemptLimitStatus.AccountTooManyAttempts);
            }

            var reservation = new AccountAttemptReservation(
                _context,
                accountTransaction,
                accountKey,
                accountStart,
                _logger);
            accountTransaction = null;

            return new LoginAttemptStartResult(LoginAttemptLimitStatus.Allowed, reservation);
        }
        catch (OperationCanceledException)
        {
            await RollbackQuietlyAsync(accountTransaction).ConfigureAwait(false);
            throw;
        }
        catch (Exception)
        {
            LogStorageFailure(_logger);
            await RollbackQuietlyAsync(accountTransaction).ConfigureAwait(false);
            return new LoginAttemptStartResult(LoginAttemptLimitStatus.Unavailable);
        }
    }

    private async Task<bool> IsIpWithinLimitsAsync(
        string ipAddress,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var normalizedIp = NormalizeIpAddress(ipAddress);
        var shortWindow = _options.IpFiveMinuteWindow;
        var shortAllowed = await ConsumeWindowAsync(
            FiveMinuteIpCounterKind,
            BucketKey("ip-5m", normalizedIp),
            now,
            shortWindow,
            _options.MaxAttemptsPerIpPerFiveMinutes,
            cancellationToken).ConfigureAwait(false);
        var hourlyWindow = _options.IpHourlyWindow;
        var hourlyAllowed = await ConsumeWindowAsync(
            HourlyIpCounterKind,
            BucketKey("ip-1h", normalizedIp),
            now,
            hourlyWindow,
            _options.MaxAttemptsPerIpPerHour,
            cancellationToken).ConfigureAwait(false);

        return shortAllowed && hourlyAllowed;
    }

    private async Task<bool> TryAdmitIpAsync(
        string ipAddress,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var normalizedIp = NormalizeIpAddress(ipAddress);
        var window = _options.IpHourlyWindow;
        var windowStart = FixedWindowStart(now, window);
        var key = BucketKey("ip-admission", normalizedIp);

        if (await HasActiveIpAdmissionAsync(key, now, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }
        if (await HasActiveLegacyIpCounterAsync(normalizedIp, now, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await AcquireIpCapacityLockAsync(cancellationToken).ConfigureAwait(false);

        if (await HasActiveIpAdmissionAsync(key, now, cancellationToken).ConfigureAwait(false))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        if (await HasActiveLegacyIpCounterAsync(normalizedIp, now, cancellationToken).ConfigureAwait(false))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        var activeAdmissionCount = await _context.LoginAttemptCounters
            .AsNoTracking()
            .LongCountAsync(
                counter => counter.CounterKind == IpAdmissionCounterKind && counter.ExpiresAtUtc > now,
                cancellationToken)
            .ConfigureAwait(false);
        var activeLegacyCounterCount = await _context.LoginAttemptCounters
            .AsNoTracking()
            .LongCountAsync(
                counter => counter.CounterKind == LegacyCounterKind && counter.ExpiresAtUtc > now,
                cancellationToken)
            .ConfigureAwait(false);
        var activeIpCount = activeAdmissionCount + activeLegacyCounterCount;
        if (activeIpCount >= MaximumTrackedIpIdentities)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        _context.LoginAttemptCounters.Add(new LoginAttemptCounter
        {
            BucketKey = key,
            CounterKind = IpAdmissionCounterKind,
            WindowStartUtc = windowStart,
            AttemptCount = 1,
            ExpiresAtUtc = windowStart.Add(window),
        });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private Task<bool> HasActiveIpAdmissionAsync(
        string key,
        DateTime now,
        CancellationToken cancellationToken) =>
        _context.LoginAttemptCounters
            .AsNoTracking()
            .AnyAsync(counter =>
                counter.CounterKind == IpAdmissionCounterKind
                && counter.BucketKey == key
                && counter.ExpiresAtUtc > now,
                cancellationToken);

    private Task<bool> HasActiveLegacyIpCounterAsync(
        string normalizedIp,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var shortWindowKey = BucketKey("ip-5m", normalizedIp);
        var hourlyWindowKey = BucketKey("ip-1h", normalizedIp);
        return _context.LoginAttemptCounters
            .AsNoTracking()
            .AnyAsync(counter =>
                counter.CounterKind == LegacyCounterKind
                && (counter.BucketKey == shortWindowKey || counter.BucketKey == hourlyWindowKey)
                && counter.ExpiresAtUtc > now,
                cancellationToken);
    }

    private async Task<bool> ConsumeWindowAsync(
        string counterKind,
        string key,
        DateTime now,
        TimeSpan window,
        int maximum,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        var count = await IncrementWindowAsync(
            counterKind,
            key,
            FixedWindowStart(now, window),
            window,
            maximum,
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return count > 0;
    }

    private async Task<int> IncrementWindowAsync(
        string counterKind,
        string key,
        DateTime windowStart,
        TimeSpan window,
        int maximum,
        CancellationToken cancellationToken)
    {
        await AcquireBucketLockAsync(key, windowStart, cancellationToken).ConfigureAwait(false);
        var expiresAt = windowStart.Add(window);
        var updated = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [dbo].[LoginAttemptCounters] SET [AttemptCount] = [AttemptCount] + 1 WHERE [BucketKey] = CAST({key} AS char(64)) AND [WindowStartUtc] = CAST({windowStart} AS datetime2(3)) AND [AttemptCount] < {maximum}",
            cancellationToken).ConfigureAwait(false);
        if (updated == 1)
        {
            return 1;
        }

        var exists = await _context.LoginAttemptCounters
            .AsNoTracking()
            .AnyAsync(counter => counter.BucketKey == key && counter.WindowStartUtc == windowStart, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return 0;
        }

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [dbo].[LoginAttemptCounters] ([BucketKey], [CounterKind], [WindowStartUtc], [AttemptCount], [ExpiresAtUtc]) VALUES (CAST({key} AS char(64)), {counterKind}, CAST({windowStart} AS datetime2(3)), 1, CAST({expiresAt} AS datetime2(3)))",
            cancellationToken).ConfigureAwait(false);
        return 1;
    }

    private async Task AcquireIpCapacityLockAsync(CancellationToken cancellationToken)
    {
        var lockResults = await _context.Database.SqlQuery<int>($"""
            DECLARE @lockResult int;
            EXEC @lockResult = sys.sp_getapplock
                @Resource = 'RoamlyLogin:IpAdmissionCapacity',
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 5000;
            SELECT @lockResult AS [Value];
            """).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (lockResults.FirstOrDefault() < 0)
        {
            throw new InvalidOperationException("The login IP capacity lock could not be acquired.");
        }
    }

    private async Task AcquireBucketLockAsync(
        string key,
        DateTime windowStart,
        CancellationToken cancellationToken)
    {
        var resource = "RoamlyLogin:" + key + ":" + windowStart.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lockResults = await _context.Database.SqlQuery<int>($"""
            DECLARE @lockResult int;
            EXEC @lockResult = sys.sp_getapplock
                @Resource = {resource},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 5000;
            SELECT @lockResult AS [Value];
            """).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (lockResults.FirstOrDefault() < 0)
        {
            throw new InvalidOperationException("The login counter lock could not be acquired.");
        }
    }

    private string BucketKey(string scope, string identifier)
    {
        var bytes = Encoding.UTF8.GetBytes(scope + "\0" + identifier);
        return Convert.ToHexString(HMACSHA256.HashData(_hmacKey, bytes));
    }

    private static string NormalizeIpAddress(string value)
    {
        if (!System.Net.IPAddress.TryParse(value, out var address))
        {
            throw new ArgumentException("IP address is invalid.", nameof(value));
        }

        return address.IsIPv4MappedToIPv6
            ? address.MapToIPv4().ToString()
            : address.ToString();
    }

    private static DateTime FixedWindowStart(DateTime now, TimeSpan window)
    {
        var ticks = now.Ticks - (now.Ticks % window.Ticks);
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private async Task TryCleanupExpiredAsync(DateTime now, CancellationToken cancellationToken)
    {
        var nowTicks = now.Ticks;
        var nextCleanup = Volatile.Read(ref _nextCleanupUtcTicks);
        if (nowTicks < nextCleanup
            || Interlocked.CompareExchange(
                ref _nextCleanupUtcTicks,
                now.Add(CleanupInterval).Ticks,
                nextCleanup) != nextCleanup)
        {
            return;
        }

        try
        {
            _ = await DeleteExpiredBatchAsync(now, CleanupBatchSize, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            LogCleanupFailure(_logger);
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var deletedTotal = 0;
        int deleted;
        do
        {
            deleted = await DeleteExpiredBatchAsync(now, DailyCleanupBatchSize, cancellationToken)
                .ConfigureAwait(false);
            deletedTotal = checked(deletedTotal + deleted);
        }
        while (deleted == DailyCleanupBatchSize);

        return deletedTotal;
    }

    private Task<int> DeleteExpiredBatchAsync(DateTime now, int batchSize, CancellationToken cancellationToken) =>
        _context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE TOP ({batchSize}) FROM [dbo].[LoginAttemptCounters] WHERE [ExpiresAtUtc] <= {now}",
            cancellationToken);

    private async Task RollbackQuietlyAsync(IDbContextTransaction? transaction)
    {
        if (transaction is null)
        {
            return;
        }

        try
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            LogStorageFailure(_logger);
        }

        await transaction.DisposeAsync().ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Login throttling storage is unavailable; the login request was rejected.")]
    private static partial void LogStorageFailure(ILogger logger);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Error,
        Message = "login_ip_capacity_exhausted: new IP identity admission was rejected.")]
    private static partial void LogIpCapacityExhausted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Expired login throttling counters could not be cleaned up.")]
    private static partial void LogCleanupFailure(ILogger logger);

    private sealed class AccountAttemptReservation(
        RoamlyDbContext context,
        IDbContextTransaction transaction,
        string bucketKey,
        DateTime windowStart,
        ILogger logger) : ILoginAttemptReservation
    {
        private bool _completed;

        public async Task<bool> CompleteAsync(bool credentialsInvalid, CancellationToken cancellationToken)
        {
            if (_completed)
            {
                return false;
            }

            try
            {
                if (!credentialsInvalid)
                {
                    var updated = await context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE [dbo].[LoginAttemptCounters] SET [AttemptCount] = [AttemptCount] - 1 WHERE [BucketKey] = CAST({bucketKey} AS char(64)) AND [WindowStartUtc] = CAST({windowStart} AS datetime2(3)) AND [AttemptCount] > 0",
                        cancellationToken).ConfigureAwait(false);
                    if (updated != 1)
                    {
                        throw new InvalidOperationException("The login counter reservation was not found.");
                    }
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _completed = true;
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                LogStorageFailure(logger);
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    LogStorageFailure(logger);
                }
            }

            await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }
}
