using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Roamly.Common;
using Roamly.Common.Storage;
using Roamly.Common.Security;
using Roamly.Domain.Abstractions;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Common.Ownership;

/// <summary>Job one-shot di hard-delete, confinato al perimetro autorizzato di R5.</summary>
public sealed partial class AccountErasureJob
{
    private const string ReceiptSchemaVersion = "1";
    private static readonly MethodInfo CountMethod = typeof(AccountErasureJob)
        .GetMethod(nameof(CountOwnedAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo DeleteMethod = typeof(AccountErasureJob)
        .GetMethod(nameof(DeleteOwnedAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly RoamlyDbContext _db;
    private readonly ICurrentUserScope _userScope;
    private readonly IIdGenerator _idGenerator;
    private readonly ILoginAttemptCounterMaintenance _loginAttemptCounterMaintenance;
    private readonly IJournalImageStorage _imageStorage;
    private readonly TimeProvider _clock;
    private readonly ILogger<AccountErasureJob> _logger;

    /// <summary>Costruisce il job con i servizi dello scope one-shot.</summary>
    public AccountErasureJob(
        RoamlyDbContext db,
        ICurrentUserScope userScope,
        IIdGenerator idGenerator,
        ILoginAttemptCounterMaintenance loginAttemptCounterMaintenance,
        IJournalImageStorage imageStorage,
        TimeProvider clock,
        ILogger<AccountErasureJob> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(userScope);
        ArgumentNullException.ThrowIfNull(idGenerator);
        ArgumentNullException.ThrowIfNull(loginAttemptCounterMaintenance);
        ArgumentNullException.ThrowIfNull(imageStorage);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _userScope = userScope;
        _idGenerator = idGenerator;
        _loginAttemptCounterMaintenance = loginAttemptCounterMaintenance;
        _imageStorage = imageStorage;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Processa una volta i candidati scaduti; lo scheduler ACA richiama questa operazione.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var candidates = await _userScope.RunAsSystemAsync(
            "poll account-erasure candidates",
            () => FindCandidatesAsync(nowUtc, cancellationToken)).ConfigureAwait(false);
        var deletionOrder = AccountErasurePlan.GetDeletionOrder(_db.Model);
        var failures = new List<Exception>();

        foreach (var candidateId in candidates)
        {
            try
            {
                await ProcessCandidateAsync(candidateId, deletionOrder, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is DbUpdateException
                    or DbException
                    or InvalidOperationException
                    or RequestFailedException
                    or HttpRequestException)
            {
                failures.Add(exception);
            }
        }

        try
        {
            _ = await _loginAttemptCounterMaintenance.CleanupExpiredAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            failures.Add(exception);
            LoginCounterCleanupFailed(_logger, exception);
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"{failures.Count} account-erasure job operation(s) failed; see the preceding job log entries.",
                failures);
        }
    }

    private Task<List<Guid>> FindCandidatesAsync(DateTime nowUtc, CancellationToken cancellationToken) =>
        _db.Users
            .AsNoTracking()
            .Where(user => user.DeletionRequestedAtUtc != null && user.DeletionScheduledForUtc <= nowUtc)
            .OrderBy(user => user.DeletionScheduledForUtc)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

    private async Task ProcessCandidateAsync(
        Guid candidateId,
        IReadOnlyList<IEntityType> deletionOrder,
        CancellationToken cancellationToken)
    {
        try
        {
            await ProcessCandidateCoreAsync(candidateId, deletionOrder, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is DbUpdateException or DbException or InvalidOperationException)
        {
            _db.ChangeTracker.Clear();
            LogFailure(candidateId, exception);
            throw;
        }
    }

    private async Task ProcessCandidateCoreAsync(
        Guid candidateId,
        IReadOnlyList<IEntityType> deletionOrder,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var user = await _userScope.RunAsSystemAsync(
            "lock and recheck account-erasure candidate",
            () => LockCandidateAsync(candidateId, cancellationToken)).ConfigureAwait(false);

        if (!IsDue(user, _clock.GetUtcNow().UtcDateTime))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            LogSkipped(candidateId);
            return;
        }

        var deletedBlobCount = await _imageStorage.DeleteByPrefixAsync(
            candidateId.ToString("D") + "/",
            cancellationToken).ConfigureAwait(false);
        var subjectHash = HashSubject(candidateId);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            AccountErasureStorageSweepConfirmed(_logger, subjectHash, deletedBlobCount);
        }

        var deletedCounts = await _userScope.RunAsUserAsync(
            candidateId,
            "erase owned resources for account-erasure",
            () => EraseOwnedResourcesAsync(candidateId, deletionOrder, cancellationToken)).ConfigureAwait(false);
        await PersistReceiptAndDeleteUserAsync(
            user!,
            deletedCounts,
            deletedBlobCount,
            candidateId,
            transaction,
            cancellationToken).ConfigureAwait(false);
    }

    private Task<ApplicationUser?> LockCandidateAsync(Guid candidateId, CancellationToken cancellationToken) =>
        _db.Users
            .FromSqlInterpolated(
                $"SELECT * FROM [dbo].[AspNetUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {candidateId}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    private static bool IsDue(ApplicationUser? user, DateTime nowUtc) =>
        user?.DeletionRequestedAtUtc is not null
        && user.DeletionScheduledForUtc is { } scheduledForUtc
        && scheduledForUtc <= nowUtc;

    private async Task<SortedDictionary<string, long>> EraseOwnedResourcesAsync(
        Guid ownerId,
        IReadOnlyList<IEntityType> deletionOrder,
        CancellationToken cancellationToken)
    {
        var deletedCounts = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var entityType in deletionOrder)
        {
            var count = await CountEntityAsync(entityType, ownerId, cancellationToken).ConfigureAwait(false);
            var deleted = await DeleteEntityAsync(entityType, ownerId, cancellationToken).ConfigureAwait(false);
            if (deleted != count)
            {
                throw new InvalidOperationException(
                    $"Lo sweep di {entityType.ClrType.Name} ha contato {count} righe e ne ha eliminate {deleted}.");
            }

            deletedCounts.Add(entityType.ClrType.Name, deleted);
        }

        await VerifySweepAsync(ownerId, deletionOrder, cancellationToken).ConfigureAwait(false);
        return deletedCounts;
    }

    private async Task VerifySweepAsync(
        Guid ownerId,
        IReadOnlyList<IEntityType> deletionOrder,
        CancellationToken cancellationToken)
    {
        foreach (var entityType in deletionOrder)
        {
            if (await CountEntityAsync(entityType, ownerId, cancellationToken).ConfigureAwait(false) != 0)
            {
                throw new InvalidOperationException(
                    $"Lo sweep di {entityType.ClrType.Name} e' incompleto; nessuna ricevuta verra' emessa.");
            }
        }
    }

    private async Task PersistReceiptAndDeleteUserAsync(
        ApplicationUser user,
        SortedDictionary<string, long> deletedCounts,
        long deletedBlobCount,
        Guid candidateId,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        var erasedAtUtc = _clock.GetUtcNow().UtcDateTime;
        var receipt = new ErasureReceipt
        {
            Id = _idGenerator.NewId(),
            SubjectHash = HashSubject(candidateId),
            RequestedAtUtc = user.DeletionRequestedAtUtc!.Value,
            ErasedAtUtc = erasedAtUtc,
            SchemaVersion = ReceiptSchemaVersion,
            DeletedRowCounts = JsonSerializer.Serialize(deletedCounts),
            StorageSweepConfirmed = true,
        };

        await _userScope.RunAsSystemAsync(
            "persist pseudonymous receipt and delete account",
            async () =>
            {
                _db.ErasureReceipts.Add(receipt);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                var deletedUsers = await _db.Users
                    .Where(account => account.Id == candidateId)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (deletedUsers != 1)
                {
                    throw new InvalidOperationException(
                        "L'account e' cambiato durante lo sweep; la ricevuta e le cancellazioni verranno annullate.");
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);

        AccountErasureCompleted(_logger, receipt.SubjectHash, deletedCounts.Count, deletedBlobCount);
    }

    private async Task<long> CountEntityAsync(
        IEntityType entityType,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var countTask = (Task<long>)CountMethod.MakeGenericMethod(entityType.ClrType)
            .Invoke(null, [_db, ownerId, cancellationToken])!;
        return await countTask.ConfigureAwait(false);
    }

    private async Task<int> DeleteEntityAsync(
        IEntityType entityType,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var deleteTask = (Task<int>)DeleteMethod.MakeGenericMethod(entityType.ClrType)
            .Invoke(null, [_db, ownerId, cancellationToken])!;
        return await deleteTask.ConfigureAwait(false);
    }

    private static Task<long> CountOwnedAsync<TEntity>(
        RoamlyDbContext db,
        Guid ownerId,
        CancellationToken cancellationToken)
        where TEntity : class =>
        db.Set<TEntity>()
            .AsNoTracking()
            .Where(entity => EF.Property<Guid>(entity, nameof(IOwnedResource.OwnerId)) == ownerId)
            .LongCountAsync(cancellationToken);

    private static Task<int> DeleteOwnedAsync<TEntity>(
        RoamlyDbContext db,
        Guid ownerId,
        CancellationToken cancellationToken)
        where TEntity : class =>
        db.Set<TEntity>()
            .Where(entity => EF.Property<Guid>(entity, nameof(IOwnedResource.OwnerId)) == ownerId)
            .ExecuteDeleteAsync(cancellationToken);

    private static string HashSubject(Guid userId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId.ToString("D").ToLowerInvariant())));

    private void LogSkipped(Guid candidateId)
    {
        var subjectHash = HashSubject(candidateId);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            AccountErasureSkipped(_logger, subjectHash);
        }
    }

    private void LogFailure(Guid candidateId, Exception exception)
    {
        var subjectHash = HashSubject(candidateId);
        AccountErasureFailed(_logger, exception, subjectHash);
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Expired login counter cleanup failed during the account-erasure one-shot job.")]
    private static partial void LoginCounterCleanupFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Account erasure completed for subject {SubjectHash}; removed {OwnedTypeCount} owned resource types and {BlobCount} Blob objects.")]
    private static partial void AccountErasureCompleted(
        ILogger logger,
        string subjectHash,
        int ownedTypeCount,
        long blobCount);

    [LoggerMessage(
        Level = LogLevel.Information,
        EventId = 4104,
        Message = "Account-erasure Blob prefix sweep confirmed for subject {SubjectHash}; removed {BlobCount} objects.")]
    private static partial void AccountErasureStorageSweepConfirmed(
        ILogger logger,
        string subjectHash,
        long blobCount);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Account erasure skipped after locked schedule recheck for subject {SubjectHash}.")]
    private static partial void AccountErasureSkipped(ILogger logger, string subjectHash);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Account erasure transaction rolled back for subject {SubjectHash}.")]
    private static partial void AccountErasureFailed(ILogger logger, Exception exception, string subjectHash);
}
