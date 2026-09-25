using Roamly.Domain.Abstractions;
using Roamly.Domain.Enums;
using Roamly.Domain.ValueObjects;

namespace Roamly.Domain.Entities;

/// <summary>
/// Un esborso (CONTEXT.md §2.3). <c>CamperId</c> e' obbligatorio, <c>TripId</c> opzionale:
/// una spesa non attribuibile a un camper non entra in nessuna metrica del prodotto.
/// </summary>
public class Expense : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Camper di appartenenza, obbligatorio.</summary>
    public Guid CamperId { get; set; }

    /// <summary>Viaggio di appartenenza, opzionale.</summary>
    public Guid? TripId { get; set; }

    /// <summary>Importo con valuta esplicita.</summary>
    public Money Amount { get; set; }

    /// <summary>Categoria di spesa, senza <c>Maintenance</c>.</summary>
    public ExpenseCategory Category { get; set; }

    /// <summary>Data della spesa.</summary>
    public DateOnly IncurredOnUtc { get; set; }

    /// <summary>Descrizione libera.</summary>
    public string? Description { get; set; }

    /// <summary>Chilometraggio al momento della spesa: serve al costo per km.</summary>
    public int? OdometerKm { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Navigazione verso il camper.</summary>
    public Camper? Camper { get; set; }

    /// <summary>Navigazione verso il viaggio.</summary>
    public Trip? Trip { get; set; }
}
