using Roamly.Api.Common.Http;
using Roamly.Api.Features.Journal;
using Roamly.Common.Storage;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Journal.GetJournalAttachmentDownload;

[ProtocolOutcomeHandler("Download autorizzato owner-scoped, blob mancante o non posseduto restituisce 404.")]
public sealed class GetJournalAttachmentDownloadHandler(
    RoamlyDbContext db,
    IJournalImageStorage storage)
{
    public async Task<JournalAttachmentDownloadResult> HandleAsync(
        Guid tripId,
        Guid entryId,
        Guid attachmentId,
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
            return new JournalAttachmentDownloadResult(JournalOutcome.NotFound);
        }

        var link = await storage.CreateReadOnlyDownloadLinkAsync(
            attachment.StorageKey, cancellationToken).ConfigureAwait(false);
        return link is null
            ? new JournalAttachmentDownloadResult(JournalOutcome.NotFound)
            : new JournalAttachmentDownloadResult(
                JournalOutcome.Found,
                new JournalAttachmentDownloadResponse(link.Uri.AbsoluteUri, link.ExpiresAtUtc),
                trip.RowVersion);
    }
}
