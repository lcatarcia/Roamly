using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roamly.Common;
using Roamly.Common.Storage;
using Roamly.Api.Common.Ownership;
using Roamly.Domain.Abstractions;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;
using Roamly.TestSupport;

namespace Roamly.IntegrationTests.Privacy;

[Collection(DatabaseCollection.Name)]
public sealed class AccountErasureJobTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateTimeOffset ClockNow = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FixedTimeProvider _clock = new(ClockNow);
    private readonly ErasureLockBarrier _lockBarrier = new();
    private readonly ErasureReceiptSaveFailure _receiptSaveFailure = new();
    private DatabaseLease _database = null!;
    private RoamlyApiFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        _database = migrated.Lease;
        await _database.ResetAsync(TestContext.Current.CancellationToken);
        _factory = new RoamlyApiFactory(
            _database.ConnectionString,
            journalImagesAzuriteConnectionString: sqlServer.JournalImageStorageConnectionString,
            configureTestServices: services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_clock);
                services.AddDbContext<RoamlyDbContext>((_, options) =>
                    options.AddInterceptors(_lockBarrier, _receiptSaveFailure));
            });
        await CreateExpiredUserAsync();
        await SeedAllApplicationOwnedTypesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Expired_account_is_erased_once_with_a_pseudonymous_receipt_and_real_counts()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<AccountErasureJob>();
        var cancellationToken = TestContext.Current.CancellationToken;

        await job.RunOnceAsync(cancellationToken);
        await job.RunOnceAsync(cancellationToken);

        await using var verification = CreateVerificationContext();
        Assert.False(await verification.Users.AnyAsync(
            user => user.Id == _userId,
            cancellationToken));

        var receipt = await verification.ErasureReceipts.AsNoTracking().SingleAsync(cancellationToken);
        Assert.Equal(SubjectHash(_userId), receipt.SubjectHash);
        Assert.DoesNotContain(_userId.ToString("D"), receipt.SubjectHash, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ClockNow.UtcDateTime.AddDays(-31), receipt.RequestedAtUtc);
        Assert.Equal(ClockNow.UtcDateTime, receipt.ErasedAtUtc);
        Assert.Equal("1", receipt.SchemaVersion);
        Assert.True(receipt.StorageSweepConfirmed);
        var receiptContent = JsonSerializer.Serialize(receipt);
        Assert.DoesNotContain(_userId.ToString("D"), receiptContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"erasure-{_userId:N}@example.test", receiptContent, StringComparison.OrdinalIgnoreCase);

        using var rowCounts = JsonDocument.Parse(receipt.DeletedRowCounts);
        var counts = rowCounts.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetInt64(), StringComparer.Ordinal);
        var journalImageKey = $"{_userId:D}/journal-entry-attachment/";
        await using (var storageScope = _factory.Services.CreateAsyncScope())
        {
            var imageStorage = storageScope.ServiceProvider.GetRequiredService<IJournalImageStorage>();
            var remainingKeys = new List<string>();
            await foreach (var key in imageStorage.ListKeysAsync(
                journalImageKey,
                cancellationToken))
            {
                remainingKeys.Add(key);
            }

            Assert.Empty(remainingKeys);
        }

        var ownedTypes = verification.Model.GetEntityTypes()
            .Where(entityType => typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType))
            .ToArray();
        Assert.Equal(ownedTypes.Length, counts.Count);
        foreach (var entityType in ownedTypes)
        {
            Assert.Equal(1, counts[entityType.ClrType.Name]);
            Assert.Equal(0, await CountOwnedRowsAsync(entityType, cancellationToken));
        }

        Assert.DoesNotContain(_userId.ToString("D"), receipt.DeletedRowCounts, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await verification.ErasureReceipts.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task one_shot_erasure_job_drains_expired_login_counters_in_batches_and_keeps_active_rows()
    {
        const int expiredRows = 2_505;
        await SeedLoginCounterRowsAsync(expiredRows);

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AccountErasureJob>()
            .RunOnceAsync(TestContext.Current.CancellationToken);

        await using var verification = CreateVerificationContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.False(await verification.Users.AnyAsync(user => user.Id == _userId, cancellationToken));
        Assert.Single(await verification.ErasureReceipts.AsNoTracking().ToListAsync(cancellationToken));
        Assert.Equal(
            0,
            await verification.LoginAttemptCounters.CountAsync(
                counter => counter.ExpiresAtUtc <= ClockNow.UtcDateTime,
                cancellationToken));
        Assert.Equal(
            1,
            await verification.LoginAttemptCounters.CountAsync(
                counter => counter.ExpiresAtUtc > ClockNow.UtcDateTime,
                cancellationToken));
    }

    [Fact]
    public async Task login_counter_cleanup_failure_is_reported_after_successful_account_erasure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using (var setup = _factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<RoamlyDbContext>();
            await db.Database.ExecuteSqlRawAsync("DROP TABLE [dbo].[LoginAttemptCounters]", cancellationToken);
        }

        try
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var job = scope.ServiceProvider.GetRequiredService<AccountErasureJob>();
            var failure = await Assert.ThrowsAsync<AggregateException>(
                () => job.RunOnceAsync(cancellationToken));
            Assert.Contains("job operation(s) failed", failure.Message, StringComparison.Ordinal);

            await using var verification = CreateVerificationContext();
            Assert.False(await verification.Users.AnyAsync(user => user.Id == _userId, cancellationToken));
            Assert.Single(await verification.ErasureReceipts.AsNoTracking().ToListAsync(cancellationToken));
        }
        finally
        {
            await using var repair = CreateVerificationContext();
            await repair.Database.ExecuteSqlRawAsync(
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
                cancellationToken);
        }
    }

    [Fact]
    public async Task Future_and_revoked_accounts_are_skipped_without_receipts_or_owned_deletes()
    {
        var revokedUserId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateAccountAsync(
            revokedUserId,
            ClockNow.UtcDateTime.AddDays(-31),
            deletionScheduledForUtc: null);
        await UpdateScheduleAsync(_userId, ClockNow.UtcDateTime.AddDays(1));

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AccountErasureJob>()
            .RunOnceAsync(cancellationToken);

        await using var verification = CreateVerificationContext();
        Assert.True(await verification.Users.AnyAsync(user => user.Id == _userId, cancellationToken));
        Assert.True(await verification.Users.AnyAsync(user => user.Id == revokedUserId, cancellationToken));
        Assert.Empty(await verification.ErasureReceipts.AsNoTracking()
            .ToListAsync(cancellationToken));
        foreach (var entityType in verification.Model.GetEntityTypes()
            .Where(entityType => typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType)))
        {
            Assert.Equal(
                1,
                await CountOwnedRowsAsync(entityType, cancellationToken));
        }
    }

    [Fact]
    public async Task Account_state_revoked_before_the_row_lock_is_rechecked_by_the_job()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _lockBarrier.PauseBeforeNextErasureLock();
        await using var scope = _factory.Services.CreateAsyncScope();
        var jobTask = scope.ServiceProvider.GetRequiredService<AccountErasureJob>()
            .RunOnceAsync(cancellationToken);

        try
        {
            await _lockBarrier.WaitForLockCommandAsync(cancellationToken);
            // Rappresenta la revoca gia' committata dopo il polling e prima del lock del job.
            Assert.Equal(1, await ClearDeletionScheduleAsync());
        }
        finally
        {
            _lockBarrier.ReleasePausedCommand();
        }

        await jobTask;

        await using var verification = CreateVerificationContext();
        Assert.True(await verification.Users.AnyAsync(user => user.Id == _userId, cancellationToken));
        Assert.Empty(await verification.ErasureReceipts.AsNoTracking().ToListAsync(cancellationToken));
        foreach (var entityType in verification.Model.GetEntityTypes()
            .Where(entityType => typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType)))
        {
            Assert.Equal(1, await CountOwnedRowsAsync(entityType, cancellationToken));
        }
    }

    [Fact]
    public async Task Job_that_holds_the_row_lock_wins_over_a_later_account_state_update()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _lockBarrier.PauseAfterNextErasureLock();
        await using var scope = _factory.Services.CreateAsyncScope();
        var jobTask = scope.ServiceProvider.GetRequiredService<AccountErasureJob>()
            .RunOnceAsync(cancellationToken);
        Task<int> revokeTask;
        try
        {
            await _lockBarrier.WaitForLockCommandAsync(cancellationToken);
            var revokeAtLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            revokeTask = ClearDeletionScheduleAsync(revokeAtLock);
            await revokeAtLock.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            _lockBarrier.ReleasePausedCommand();
        }

        await jobTask;
        Assert.Equal(0, await revokeTask);
        await using var verification = CreateVerificationContext();
        Assert.False(await verification.Users.AnyAsync(user => user.Id == _userId, cancellationToken));
        Assert.Single(await verification.ErasureReceipts.AsNoTracking().ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task Failed_owned_sweep_rolls_back_all_deletes_and_does_not_create_a_receipt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _lockBarrier.FailAfterMaintenanceLogDelete();
        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<AccountErasureJob>();

        var exception = await Assert.ThrowsAsync<AggregateException>(
            () => job.RunOnceAsync(cancellationToken));
        Assert.IsType<InvalidOperationException>(Assert.Single(exception.InnerExceptions));

        await using var verification = CreateVerificationContext();
        Assert.True(await verification.Users.AnyAsync(user => user.Id == _userId, cancellationToken));
        Assert.Empty(await verification.ErasureReceipts.AsNoTracking().ToListAsync(cancellationToken));
        foreach (var entityType in verification.Model.GetEntityTypes()
            .Where(entityType => typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType)))
        {
            Assert.Equal(1, await CountOwnedRowsAsync(entityType, cancellationToken));
        }
    }

    [Fact]
    public async Task Failed_candidate_does_not_block_later_erasure_and_job_reports_the_failure()
    {
        var laterUserId = Guid.NewGuid();
        await CreateAccountAsync(
            laterUserId,
            ClockNow.UtcDateTime.AddDays(-31),
            ClockNow.UtcDateTime.AddHours(-12));
        _lockBarrier.FailAfterMaintenanceLogDelete();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<AccountErasureJob>();

        var exception = await Assert.ThrowsAsync<AggregateException>(
            () => job.RunOnceAsync(cancellationToken));

        Assert.Single(exception.InnerExceptions);
        await using var verification = CreateVerificationContext();
        Assert.True(await verification.Users.AnyAsync(user => user.Id == _userId, cancellationToken));
        Assert.False(await verification.Users.AnyAsync(user => user.Id == laterUserId, cancellationToken));
        var receipt = await verification.ErasureReceipts.AsNoTracking().SingleAsync(cancellationToken);
        Assert.Equal(SubjectHash(laterUserId), receipt.SubjectHash);
        foreach (var entityType in verification.Model.GetEntityTypes()
            .Where(entityType => typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType)))
        {
            Assert.Equal(1, await CountOwnedRowsAsync(entityType, cancellationToken));
        }
    }

    [Fact]
    public async Task Failed_receipt_save_does_not_reuse_tracked_receipt_for_later_candidate()
    {
        var laterUserId = Guid.NewGuid();
        await CreateAccountAsync(
            laterUserId,
            ClockNow.UtcDateTime.AddDays(-31),
            ClockNow.UtcDateTime.AddHours(-12));
        _receiptSaveFailure.FailNextReceiptSave();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<AccountErasureJob>();

        var exception = await Assert.ThrowsAsync<AggregateException>(
            () => job.RunOnceAsync(cancellationToken));

        Assert.IsType<DbUpdateException>(Assert.Single(exception.InnerExceptions));
        await using var verification = CreateVerificationContext();
        Assert.True(await verification.Users.AnyAsync(user => user.Id == _userId, cancellationToken));
        Assert.False(await verification.Users.AnyAsync(user => user.Id == laterUserId, cancellationToken));
        var receipt = await verification.ErasureReceipts.AsNoTracking().SingleAsync(cancellationToken);
        Assert.Equal(SubjectHash(laterUserId), receipt.SubjectHash);
        foreach (var entityType in verification.Model.GetEntityTypes()
            .Where(entityType => typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType)))
        {
            Assert.Equal(1, await CountOwnedRowsAsync(entityType, cancellationToken));
        }
    }

    private async Task CreateExpiredUserAsync()
        => await CreateAccountAsync(
            _userId,
            ClockNow.UtcDateTime.AddDays(-31),
            ClockNow.UtcDateTime.AddDays(-1));

    private async Task CreateAccountAsync(Guid userId, DateTime? requestedAtUtc, DateTime? deletionScheduledForUtc)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await users.CreateAsync(new ApplicationUser
        {
            Id = userId,
            UserName = $"erasure-{userId:N}@example.test",
            Email = $"erasure-{userId:N}@example.test",
            EmailConfirmed = true,
            CreatedAtUtc = ClockNow.UtcDateTime.AddDays(-60),
            DeletionRequestedAtUtc = requestedAtUtc,
            DeletionScheduledForUtc = deletionScheduledForUtc,
        }, "Test!Password123");

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private async Task UpdateScheduleAsync(Guid userId, DateTime? scheduledForUtc)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        user.DeletionScheduledForUtc = scheduledForUtc;
        var result = await users.UpdateAsync(user);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private async Task<int> ClearDeletionScheduleAsync(TaskCompletionSource? aboutToUpdate = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
        aboutToUpdate?.TrySetResult();
        return await db.Users
            .Where(user => user.Id == _userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(user => user.DeletionRequestedAtUtc, (DateTime?)null)
                    .SetProperty(user => user.DeletionScheduledForUtc, (DateTime?)null)
                    .SetProperty(user => user.ConcurrencyStamp, Guid.NewGuid().ToString()),
                TestContext.Current.CancellationToken);
    }

    private async Task SeedAllApplicationOwnedTypesAsync()
    {
        await using var db = CreateOwnerContext(_userId);
        var data = BuilderRegistry.BuildAll(new BuilderContext(_userId, db.Model));
        db.AddRange(data);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var imageStorage = scope.ServiceProvider.GetRequiredService<IJournalImageStorage>();
        foreach (var attachment in data.OfType<JournalEntryAttachment>())
        {
            await using var image = new MemoryStream([0xFF, 0xD8, 0xFF, 0xD9], writable: false);
            await imageStorage.UploadAsync(
                attachment.StorageKey,
                image,
                attachment.ContentType,
                TestContext.Current.CancellationToken);
        }
    }

    private async Task SeedLoginCounterRowsAsync(int expiredRows)
    {
        var expiredAt = ClockNow.UtcDateTime.AddMinutes(-1);
        var activeUntil = ClockNow.UtcDateTime.AddHours(1);
        var windowStart = ClockNow.UtcDateTime.AddHours(-1);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            ;WITH Numbers AS
            (
                SELECT TOP ({expiredRows}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [Number]
                FROM sys.all_objects AS firstObject
                CROSS JOIN sys.all_objects AS secondObject
            )
            INSERT INTO [dbo].[LoginAttemptCounters]
                ([BucketKey], [CounterKind], [WindowStartUtc], [AttemptCount], [ExpiresAtUtc])
            SELECT
                RIGHT(REPLICATE('0', 64) + CONVERT(varchar(64), [Number]), 64),
                'account',
                CAST({windowStart} AS datetime2(3)),
                1,
                CAST({expiredAt} AS datetime2(3))
            FROM Numbers;
            INSERT INTO [dbo].[LoginAttemptCounters]
                ([BucketKey], [CounterKind], [WindowStartUtc], [AttemptCount], [ExpiresAtUtc])
            VALUES
                (REPLICATE('F', 64), 'ip-admission', CAST({windowStart} AS datetime2(3)), 1, CAST({activeUntil} AS datetime2(3)));
            """,
            TestContext.Current.CancellationToken);
    }

    private async Task<long> CountOwnedRowsAsync(IEntityType entityType, CancellationToken cancellationToken)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(_database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT_BIG(*) FROM [dbo].[{entityType.GetTableName()}] WHERE [OwnerId] = @ownerId;";
        command.Parameters.AddWithValue("@ownerId", _userId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private RoamlyDbContext CreateOwnerContext(Guid ownerId)
    {
        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .Options;
        return new RoamlyDbContext(options, new TestCurrentUser(ownerId));
    }

    private RoamlyDbContext CreateVerificationContext() => CreateOwnerContext(Guid.NewGuid());

    private static string SubjectHash(Guid userId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId.ToString("D").ToLowerInvariant())));

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
        public bool IsAuthenticated => true;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ErasureLockBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _commandPaused =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseCommand =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _pauseMode;
        private int _failAfterMaintenanceLogDelete;

        public void PauseBeforeNextErasureLock() => Arm(mode: 1);
        public void PauseAfterNextErasureLock() => Arm(mode: 2);
        public void FailAfterMaintenanceLogDelete() =>
            Interlocked.Exchange(ref _failAfterMaintenanceLogDelete, 1);
        public Task WaitForLockCommandAsync(CancellationToken cancellationToken) =>
            _commandPaused.Task.WaitAsync(cancellationToken);
        public void ReleasePausedCommand() => _releaseCommand.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (IsErasureLockCommand(command) && Interlocked.CompareExchange(ref _pauseMode, 0, 1) == 1)
            {
                await PauseAsync(cancellationToken);
            }

            return result;
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("DELETE", StringComparison.Ordinal)
                && command.CommandText.Contains("[MaintenanceLogs]", StringComparison.Ordinal)
                && Interlocked.Exchange(ref _failAfterMaintenanceLogDelete, 0) == 1)
            {
                throw new InvalidOperationException("Injected failure after an owned delete.");
            }

            return ValueTask.FromResult(result);
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (IsErasureLockCommand(command) && Interlocked.CompareExchange(ref _pauseMode, 0, 2) == 2)
            {
                await PauseAsync(cancellationToken);
            }

            return result;
        }

        private void Arm(int mode)
        {
            if (Interlocked.CompareExchange(ref _pauseMode, mode, 0) != 0)
            {
                throw new InvalidOperationException("Il gate del lock erasure e' gia' armato.");
            }
        }

        private async Task PauseAsync(CancellationToken cancellationToken)
        {
            _commandPaused.TrySetResult();
            await _releaseCommand.Task.WaitAsync(cancellationToken);
        }

        private static bool IsErasureLockCommand(DbCommand command) =>
            command.CommandText.Contains("UPDLOCK, HOLDLOCK", StringComparison.Ordinal);
    }

    private sealed class ErasureReceiptSaveFailure : SaveChangesInterceptor
    {
        private int _failNextReceiptSave;

        public void FailNextReceiptSave() => Interlocked.Exchange(ref _failNextReceiptSave, 1);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _failNextReceiptSave, 0) == 1
                && eventData.Context?.ChangeTracker.Entries<ErasureReceipt>()
                    .Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new DbUpdateException(
                    "Injected failure while saving an erasure receipt.",
                    new InvalidOperationException("Injected receipt persistence failure."));
            }

            return ValueTask.FromResult(result);
        }
    }
}
