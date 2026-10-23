using System.Data.Common;
using Azure;
using Microsoft.EntityFrameworkCore;
using Roamly.Common;
using Roamly.Common.Storage;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Common.Ownership;

/// <summary>Drena gli intenti SQL di cancellazione Blob; i messaggi falliti restano ritentabili.</summary>
public interface IJournalAttachmentDeletionDrainer
{
    Task RunOnceAsync(CancellationToken cancellationToken);
}

public sealed partial class JournalAttachmentDeletionOutboxJob(
    RoamlyDbContext db,
    ICurrentUserScope userScope,
    IJournalImageStorage storage,
    ILogger<JournalAttachmentDeletionOutboxJob> logger) : IJournalAttachmentDeletionDrainer
{
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var owners = await userScope.RunAsSystemAsync(
            "enumerate Journal Blob deletion outbox owners",
            () => db.JournalAttachmentDeletionOutbox
                .IgnoreQueryFilters()
                .Select(message => message.OwnerId)
                .Distinct()
                .OrderBy(ownerId => ownerId)
                .ToListAsync(cancellationToken)).ConfigureAwait(false);
        var failures = new List<Exception>();
        var deletedMessages = 0;

        foreach (var ownerId in owners)
        {
            var messages = await ReadMessagesAsync(ownerId, cancellationToken).ConfigureAwait(false);
            foreach (var message in messages)
            {
                try
                {
                    await storage.DeleteIfExistsAsync(message.StorageKey, cancellationToken).ConfigureAwait(false);
                    deletedMessages += await RemoveMessageAsync(
                        ownerId, message.Id, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception) when (
                    exception is DbUpdateException or DbException or InvalidOperationException
                        or RequestFailedException or HttpRequestException)
                {
                    failures.Add(exception);
                    DeletionFailed(logger, exception, message.Id);
                }
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"{failures.Count} Journal Blob deletion(s) failed and remain in the SQL outbox.",
                failures);
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            DeletionOutboxDrained(logger, deletedMessages);
        }
    }

    private Task<List<JournalAttachmentDeletionOutboxMessage>> ReadMessagesAsync(
        Guid ownerId,
        CancellationToken cancellationToken) =>
        userScope.RunAsUserAsync(
            ownerId,
            "read owner-scoped Journal Blob deletion intents",
            () => db.JournalAttachmentDeletionOutbox
                .AsNoTracking()
                .Where(message => message.OwnerId == ownerId)
                .OrderBy(message => message.CreatedAtUtc)
                .ThenBy(message => message.Id)
                .ToListAsync(cancellationToken));

    private Task<int> RemoveMessageAsync(
        Guid ownerId,
        Guid messageId,
        CancellationToken cancellationToken) =>
        userScope.RunAsUserAsync(
            ownerId,
            "remove completed Journal Blob deletion intent",
            () => db.JournalAttachmentDeletionOutbox
                .Where(message => message.OwnerId == ownerId && message.Id == messageId)
                .ExecuteDeleteAsync(cancellationToken));

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Journal Blob deletion outbox message {MessageId} failed and will be retried.")]
    private static partial void DeletionFailed(
        ILogger logger,
        Exception exception,
        Guid messageId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Journal Blob deletion outbox drained {DeletedMessageCount} message(s).")]
    private static partial void DeletionOutboxDrained(ILogger logger, int deletedMessageCount);
}
