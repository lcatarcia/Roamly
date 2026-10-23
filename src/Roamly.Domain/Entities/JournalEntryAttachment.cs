using Roamly.Domain.Abstractions;

namespace Roamly.Domain.Entities;

/// <summary>Metadati owner-scoped di un'immagine originale conservata in Blob Storage.</summary>
public class JournalEntryAttachment : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Voce del Journal a cui appartiene l'immagine.</summary>
    public Guid JournalEntryId { get; set; }

    /// <summary>Chiave blob server-generated owner-prefixed; non contiene il nome client.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Tipo immagine derivato dalla firma dei byte ricevuti.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Dimensione originale in byte.</summary>
    public long SizeBytes { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Navigazione verso la voce proprietaria.</summary>
    public JournalEntry? JournalEntry { get; set; }
}
