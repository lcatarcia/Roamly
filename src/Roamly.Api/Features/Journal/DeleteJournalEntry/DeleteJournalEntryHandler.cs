using Roamly.Api.Common.Http;
using Roamly.Api.Features.Journal;
using Roamly.Common;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Journal.DeleteJournalEntry;

[ProtocolOutcomeHandler("404 owner-scoped e 428/412 sul Trip sono esiti previsti; la mutation SQL accoda atomicamente la cancellazione Blob.")]
public sealed class DeleteJournalEntryHandler(
    RoamlyDbContext db,
    IIdGenerator ids,
    TimeProvider clock)
{
    public async Task<JournalMutationResult> HandleAsync(
        Guid tripId,
        Guid entryId,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        var trip = await JournalMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        var entry = trip is null
            ? null
            : await JournalMutationSupport.FindOwnedEntryAsync(db, tripId, entryId, cancellationToken)
                .ConfigureAwait(false);
        if (trip is null || entry is null)
        {
            return JournalMutationSupport.NotFound();
        }

        var precondition = JournalMutationSupport.CheckPrecondition(trip, ifMatch, out var token);
        if (precondition is not null)
        {
            return new JournalMutationResult(precondition.Value);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var attachment in entry.Attachments)
        {
            JournalMutationSupport.QueueBlobDeletion(
                db, ids, attachment.OwnerId, attachment.StorageKey, now);
        }

        db.JournalEntries.Remove(entry);
        var saved = await JournalMutationSupport.SaveTripMutationAsync(
            db, trip, token, now, cancellationToken).ConfigureAwait(false);
        return saved
            ? new JournalMutationResult(JournalOutcome.Deleted, RowVersion: trip.RowVersion)
            : new JournalMutationResult(JournalOutcome.PreconditionFailed);
    }
}
