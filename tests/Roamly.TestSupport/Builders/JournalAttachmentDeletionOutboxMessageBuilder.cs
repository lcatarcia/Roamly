using Roamly.Domain.Entities;

namespace Roamly.TestSupport.Builders;

/// <summary>Costruisce un intento Blob owner-scoped per i test model-driven di export ed erasure.</summary>
public sealed class JournalAttachmentDeletionOutboxMessageBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(JournalAttachmentDeletionOutboxMessage);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new JournalAttachmentDeletionOutboxMessage
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            StorageKey =
                $"{context.OwnerId:D}/journal-entry-attachment/{context.Ids.NewId():D}/{context.Ids.NewId():D}",
            CreatedAtUtc = context.UtcNow,
        };
    }
}
