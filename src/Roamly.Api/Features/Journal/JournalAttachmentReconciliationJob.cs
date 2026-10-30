using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Ownership;
using Roamly.Common;
using Roamly.Common.Storage;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Journal;

/// <summary>Verificatore one-shot F2; non elimina dati e non ha scheduler registrato.</summary>
public sealed partial class JournalAttachmentReconciliationJob(
    RoamlyDbContext db,
    ICurrentUserScope userScope,
    IJournalImageStorage storage,
    ILogger<JournalAttachmentReconciliationJob> logger)
{
    public async Task<JournalAttachmentReconciliationReport> RunOnceAsync(
        CancellationToken cancellationToken)
    {
        var blobKeys = await ReadBlobKeysAsync(cancellationToken).ConfigureAwait(false);
        var ownerIds = await userScope.RunAsSystemAsync(
            "enumerate accounts for Journal attachment reconciliation",
            () => db.Users.AsNoTracking().Select(user => user.Id).ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        var sqlKeys = await ReadSqlKeysAsync(ownerIds, cancellationToken).ConfigureAwait(false);
        var result = JournalAttachmentReconciliationPlan.Compare(blobKeys, sqlKeys);
        LogResult(result);
        return result;
    }

    private async Task<HashSet<string>> ReadBlobKeysAsync(CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var key in storage.ListKeysAsync(string.Empty, cancellationToken)
            .ConfigureAwait(false))
        {
            keys.Add(key);
        }

        return keys;
    }

    private async Task<HashSet<string>> ReadSqlKeysAsync(
        IEnumerable<Guid> ownerIds,
        CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ownerId in ownerIds)
        {
            var ownerKeys = await userScope.RunAsUserAsync(
                ownerId,
                "read owner-scoped Journal attachments for reconciliation",
                () => db.JournalEntryAttachments
                    .AsNoTracking()
                    .Where(attachment => attachment.OwnerId == ownerId)
                    .Select(attachment => attachment.StorageKey)
                    .ToListAsync(cancellationToken)).ConfigureAwait(false);
            keys.UnionWith(ownerKeys);
        }

        return keys;
    }

    private void LogResult(JournalAttachmentReconciliationReport result)
    {
        if (result.OrphanBlobCount == 0 && result.DanglingSqlRowCount == 0)
        {
            ReconciliationClean(logger);
            return;
        }

        ReconciliationMismatch(logger, result.OrphanBlobCount, result.DanglingSqlRowCount);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Journal attachment reconciliation completed: no orphan Blob objects or dangling SQL rows.")]
    private static partial void ReconciliationClean(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Journal attachment reconciliation found {OrphanBlobCount} orphan Blob objects and {DanglingSqlRowCount} dangling SQL rows.")]
    private static partial void ReconciliationMismatch(
        ILogger logger,
        long orphanBlobCount,
        long danglingSqlRowCount);
}
