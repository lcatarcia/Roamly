using Roamly.Domain.Abstractions;

namespace Roamly.Domain.Entities;

/// <summary>Intento persistente, owner-scoped, di cancellazione di un blob Journal.</summary>
public class JournalAttachmentDeletionOutboxMessage : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Chiave dell'oggetto da rimuovere dopo il commit SQL.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }
}
