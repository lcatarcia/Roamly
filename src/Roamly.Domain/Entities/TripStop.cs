using Roamly.Domain.Abstractions;
using Roamly.Domain.ValueObjects;

namespace Roamly.Domain.Entities;

/// <summary>
/// Tappa di un viaggio specifico (CONTEXT.md §2.3). Non e' un luogo riusabile: la posizione
/// e' uno snapshot, cosi' la tappa resta leggibile se il <see cref="SavedPlace"/> collegato cambia.
/// </summary>
public class TripStop : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Viaggio di appartenenza.</summary>
    public Guid TripId { get; set; }

    /// <summary>Ordine della tappa nell'itinerario.</summary>
    public int SequenceNo { get; set; }

    /// <summary>Nome della tappa.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Posizione, complex type opzionale: "da qualche parte in Bretagna" e' una tappa valida.</summary>
    public Coordinates? Position { get; set; }

    /// <summary>Arrivo.</summary>
    public DateOnly? ArrivalOnUtc { get; set; }

    /// <summary>Partenza.</summary>
    public DateOnly? DepartureOnUtc { get; set; }

    /// <summary>Luogo salvato da cui la tappa e' stata creata, se esiste.</summary>
    public Guid? SavedPlaceId { get; set; }

    /// <summary>Note libere.</summary>
    public string? Notes { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Navigazione verso il viaggio.</summary>
    public Trip? Trip { get; set; }

    /// <summary>Navigazione verso il luogo salvato.</summary>
    public SavedPlace? SavedPlace { get; set; }
}
