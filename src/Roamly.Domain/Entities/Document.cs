using Roamly.Domain.Abstractions;

namespace Roamly.Domain.Entities;

/// <summary>
/// Documento relativo al camper (CONTEXT.md §2.3, Phase 4). Fuori dall'MVP: introduce lo storage
/// di file, che e' un sottosistema a se'. Qui esiste solo la topologia, non la feature.
/// CONTEXT.md non elenca i campi di questa entita': l'insieme minimo sotto e' dichiarato nel
/// rapporto del Blocco 2 e va confermato prima della Phase 4.
/// </summary>
public class Document : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Camper di appartenenza.</summary>
    public Guid CamperId { get; set; }

    /// <summary>Titolo del documento.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Chiave dell'oggetto binario nello storage: nessun byte vive sul database.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Content type dichiarato.</summary>
    public string? ContentType { get; set; }

    /// <summary>Dimensione in byte.</summary>
    public int? SizeBytes { get; set; }

    /// <summary>Data di emissione.</summary>
    public DateOnly? IssuedOnUtc { get; set; }

    /// <summary>Data di scadenza.</summary>
    public DateOnly? ExpiresOnUtc { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Navigazione verso il camper.</summary>
    public Camper? Camper { get; set; }
}
