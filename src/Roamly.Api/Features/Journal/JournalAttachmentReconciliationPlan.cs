namespace Roamly.Api.Features.Journal;

/// <summary>Confronto puro usato da F2 per esporre entrambe le direzioni della divergenza.</summary>
public static class JournalAttachmentReconciliationPlan
{
    public static JournalAttachmentReconciliationReport Compare(
        IEnumerable<string> blobKeys,
        IEnumerable<string> sqlStorageKeys)
    {
        ArgumentNullException.ThrowIfNull(blobKeys);
        ArgumentNullException.ThrowIfNull(sqlStorageKeys);
        var blobs = blobKeys.ToHashSet(StringComparer.Ordinal);
        var rows = sqlStorageKeys.ToHashSet(StringComparer.Ordinal);
        return new JournalAttachmentReconciliationReport(
            blobs.LongCount(key => !rows.Contains(key)),
            rows.LongCount(key => !blobs.Contains(key)));
    }
}

/// <summary>Conteggi di riconciliazione; non contiene owner o chiavi Blob nei log.</summary>
public sealed record JournalAttachmentReconciliationReport(
    long OrphanBlobCount,
    long DanglingSqlRowCount);
