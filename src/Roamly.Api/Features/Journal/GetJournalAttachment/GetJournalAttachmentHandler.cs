using Roamly.Api.Common.Http;
using Roamly.Api.Features.Journal;
using Roamly.Api.Features.Journal.GetJournalEntry;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Journal.GetJournalAttachment;

[ProtocolOutcomeHandler("Un'immagine assente o non posseduta restituisce il 404 uniforme.")]
public sealed class GetJournalAttachmentHandler(RoamlyDbContext db)
{
    public async Task<JournalAttachmentReadResult> HandleAsync(
        Guid tripId,
        Guid entryId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var result = await new GetJournalEntryHandler(db)
            .HandleAsync(tripId, entryId, cancellationToken).ConfigureAwait(false);
        var attachment = result.Entry?.Attachments.SingleOrDefault(item => item.Id == attachmentId);
        return attachment is null
            ? new JournalAttachmentReadResult(JournalOutcome.NotFound)
            : new JournalAttachmentReadResult(JournalOutcome.Found, attachment, result.RowVersion);
    }
}
