using Roamly.Api.Common.Http;
using Roamly.Api.Features.Journal;
using Roamly.Common;
using Roamly.Common.Storage;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Journal.UploadJournalAttachment;

[ProtocolOutcomeHandler("404 owner-scoped, 409 limite allegati e 428/412 sul Trip sono esiti previsti.")]
public sealed partial class UploadJournalAttachmentHandler(
    RoamlyDbContext db,
    IJournalImageStorage storage,
    IIdGenerator ids,
    TimeProvider clock,
    ILogger<UploadJournalAttachmentHandler> logger)
{
    private const int MaximumAttachments = 10;

    public async Task<JournalAttachmentMutationResult> HandleAsync(
        Guid tripId,
        Guid entryId,
        JournalImageUploadStream content,
        string contentType,
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
            return new JournalAttachmentMutationResult(JournalOutcome.NotFound);
        }

        var precondition = JournalMutationSupport.CheckPrecondition(trip, ifMatch, out var token);
        if (precondition is not null)
        {
            return new JournalAttachmentMutationResult(precondition.Value);
        }

        if (entry.Attachments.Count >= MaximumAttachments)
        {
            return new JournalAttachmentMutationResult(JournalOutcome.AttachmentLimitReached);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var id = ids.NewId();
        var key = CreateStorageKey(trip.OwnerId, entry.Id, id);
        var attachment = new JournalEntryAttachment
        {
            Id = id,
            OwnerId = trip.OwnerId,
            JournalEntryId = entry.Id,
            StorageKey = key,
            ContentType = contentType,
            CreatedAtUtc = now,
        };

        try
        {
            await storage.UploadAsync(key, content, contentType, cancellationToken).ConfigureAwait(false);
            attachment.SizeBytes = content.BytesRead;
            db.JournalEntryAttachments.Add(attachment);
            entry.Attachments.Add(attachment);
            var saved = await JournalMutationSupport.SaveTripMutationAsync(
                db, trip, token, now, cancellationToken).ConfigureAwait(false);
            if (!saved)
            {
                await DeleteUncommittedBlobAsync(key, cancellationToken).ConfigureAwait(false);
                return new JournalAttachmentMutationResult(JournalOutcome.PreconditionFailed);
            }

            return new JournalAttachmentMutationResult(
                JournalOutcome.Created,
                JournalAttachmentResponse.From(attachment),
                trip.RowVersion);
        }
        catch
        {
            await DeleteUncommittedBlobAsync(key, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static string CreateStorageKey(Guid ownerId, Guid entryId, Guid attachmentId) =>
        $"{ownerId:D}/journal-entry-attachment/{entryId:D}/{attachmentId:D}";

    private async Task DeleteUncommittedBlobAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteIfExistsAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            UploadCleanupFailed(logger, exception);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "A failed Journal image upload left an uncommitted Blob object; F2 reconciliation will detect it.")]
    private static partial void UploadCleanupFailed(ILogger logger, Exception exception);
}
