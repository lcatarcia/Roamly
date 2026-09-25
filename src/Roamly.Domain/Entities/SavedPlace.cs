using Roamly.Domain.Abstractions;
using Roamly.Domain.Enums;
using Roamly.Domain.ValueObjects;

namespace Roamly.Domain.Entities;

/// <summary>
/// Luogo salvato dall'utente, riusabile tra piu' viaggi (CONTEXT.md §2.3). Figlio diretto di
/// <c>AspNetUsers</c>: il fatto geografico non e' personale, il fatto che io l'abbia salvato si'.
/// </summary>
public class SavedPlace : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Nome del luogo.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Categoria del luogo.</summary>
    public SavedPlaceCategory Category { get; set; }

    /// <summary>Posizione, complex type opzionale.</summary>
    public Coordinates? Position { get; set; }

    /// <summary>Indirizzo in chiaro.</summary>
    public string? Address { get; set; }

    /// <summary>Note libere.</summary>
    public string? Notes { get; set; }

    /// <summary>Provider esterno di provenienza. Colonna senza FK: e' uno snapshot.</summary>
    public string? ExternalProvider { get; set; }

    /// <summary>Identificativo presso il provider esterno.</summary>
    public string? ExternalPlaceId { get; set; }

    /// <summary>Quando lo snapshot esterno e' stato acquisito.</summary>
    public DateTime? ExternalFetchedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Tappe che citano questo luogo.</summary>
    public ICollection<TripStop> TripStops { get; } = [];
}
