using Roamly.Domain.Abstractions;
using Roamly.Domain.Enums;

namespace Roamly.Domain.Entities;

/// <summary>
/// Lettura datata del contachilometri (CONTEXT.md §2.2). Una lettura senza data mente:
/// per questo non esiste un campo <c>CurrentOdometerKm</c> su <see cref="Camper"/>.
/// </summary>
public class OdometerReading : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Camper di appartenenza.</summary>
    public Guid CamperId { get; set; }

    /// <summary>Valore letto, in km.</summary>
    public int ReadingKm { get; set; }

    /// <summary>Data della lettura: una sola per camper per giorno.</summary>
    public DateOnly TakenOnUtc { get; set; }

    /// <summary>Origine della lettura.</summary>
    public OdometerSource Source { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Navigazione verso il camper.</summary>
    public Camper? Camper { get; set; }
}
