using Roamly.Domain.Abstractions;
using Roamly.Domain.Enums;

namespace Roamly.Domain.Entities;

/// <summary>
/// Oggetto o accessorio presente sul camper (CONTEXT.md §2.2). Figlio di <see cref="Camper"/>, CASCADE.
/// </summary>
public class Equipment : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Camper di appartenenza.</summary>
    public Guid CamperId { get; set; }

    /// <summary>Nome dell'accessorio.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Categoria dell'accessorio.</summary>
    public EquipmentCategory Category { get; set; }

    /// <summary>Note libere.</summary>
    public string? Notes { get; set; }

    /// <summary>Data di installazione.</summary>
    public DateOnly? InstalledOnUtc { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Navigazione verso il camper.</summary>
    public Camper? Camper { get; set; }
}
