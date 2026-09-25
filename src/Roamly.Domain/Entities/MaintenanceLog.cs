using Roamly.Domain.Abstractions;
using Roamly.Domain.ValueObjects;

namespace Roamly.Domain.Entities;

/// <summary>
/// Il fatto che un intervento e' stato eseguito. Immutabile (CONTEXT.md §2.2).
/// Il costo vive qui, non in <c>Expense</c>.
/// </summary>
public class MaintenanceLog : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Manutenzione ricorrente di appartenenza.</summary>
    public Guid MaintenanceItemId { get; set; }

    /// <summary>Data dell'intervento.</summary>
    public DateOnly PerformedOnUtc { get; set; }

    /// <summary>Chilometraggio al momento dell'intervento, se noto.</summary>
    public int? OdometerKm { get; set; }

    /// <summary>Costo dell'intervento, complex type opzionale.</summary>
    public Money? Cost { get; set; }

    /// <summary>Officina.</summary>
    public string? Workshop { get; set; }

    /// <summary>Note libere.</summary>
    public string? Notes { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Navigazione verso la manutenzione ricorrente.</summary>
    public MaintenanceItem? MaintenanceItem { get; set; }
}
