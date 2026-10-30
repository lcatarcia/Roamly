using Roamly.Api.Common.Http;
using Roamly.Api.Features.Journal;
using Roamly.Common;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Journal.DeleteJournalAttachment;

[ProtocolOutcomeHandler("404 owner-scoped and 428/412 sul Trip sono esiti previsti.")]
public sealed class DeleteJournalAttachmentHandler(
    RoamlyDbContext db,
    IIdGenerator ids,
    TimeProvider clock)
{
    public async Task<JournalAttachmentMutationResult> HandleAsync(
        Guid tripId,
        Guid entryId,
        Guid attachmentId,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        var trip = await JournalMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        var entry = trip is null
            ? null
            : await JournalMutationSupport.FindOwnedEntryAsync(db, tripId, entryId, cancellationToken)
                .ConfigureAwait(false);
        var attachment = entry?.Attachments.SingleOrDefault(item =>
            item.OwnerId == db.CurrentUserId && item.Id == attachmentId);
        if (trip is null || attachment is null)
        {
            return new JournalAttachmentMutationResult(JournalOutcome.NotFound);
        }

        var precondition = JournalMutationSupport.CheckPrecondition(trip, ifMatch, out var token);
        if (precondition is not null)
        {
            return new JournalAttachmentMutationResult(precondition.Value);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        JournalMutationSupport.QueueBlobDeletion(db, ids, attachment.OwnerId, attachment.StorageKey, now);
        db.JournalEntryAttachments.Remove(attachment);
        var saved = await JournalMutationSupport.SaveTripMutationAsync(
            db, trip, token, now, cancellationToken).ConfigureAwait(false);
        return saved
            ? new JournalAttachmentMutationResult(JournalOutcome.Deleted, RowVersion: trip.RowVersion)
            : new JournalAttachmentMutationResult(JournalOutcome.PreconditionFailed);
    }
}
