using Roamly.Domain.Entities;

namespace Roamly.TestSupport.Builders;

/// <summary>Costruisce un'immagine owned per i verificatori di copertura e di erasure.</summary>
public sealed class JournalEntryAttachmentBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(JournalEntryAttachment);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var entry = context.Parent<JournalEntry>();
        return new JournalEntryAttachment
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            JournalEntryId = entry.Id,
            StorageKey = $"{context.OwnerId:D}/journal-entry-attachment/{entry.Id:D}/attachment",
            ContentType = "image/jpeg",
            SizeBytes = 3,
            CreatedAtUtc = context.UtcNow,
        };
    }
}
