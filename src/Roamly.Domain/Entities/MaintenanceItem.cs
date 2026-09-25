using Roamly.Domain.Abstractions;
using Roamly.Domain.Enums;

namespace Roamly.Domain.Entities;

/// <summary>
/// Regola di una manutenzione ricorrente piu' lo stato di scadenza denormalizzato (CONTEXT.md §2.2).
/// </summary>
public class MaintenanceItem : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Camper di appartenenza.</summary>
    public Guid CamperId { get; set; }

    /// <summary>Nome della manutenzione.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Categoria della manutenzione.</summary>
    public MaintenanceCategory Category { get; set; }

    /// <summary>Ricorrenza chilometrica.</summary>
    public int? IntervalKm { get; set; }

    /// <summary>Ricorrenza temporale in mesi.</summary>
    public short? IntervalMonths { get; set; }

    /// <summary>Denormalizzato dall'ultimo <see cref="MaintenanceLog"/>.</summary>
    public DateOnly? LastServiceOnUtc { get; set; }

    /// <summary>Denormalizzato dall'ultimo <see cref="MaintenanceLog"/>.</summary>
    public int? LastServiceOdometerKm { get; set; }

    /// <summary>Scadenza temporale persistita, calcolata dal dominio.</summary>
    public DateOnly? NextDueOnUtc { get; set; }

    /// <summary>Scadenza chilometrica persistita, calcolata dal dominio.</summary>
    public int? NextDueOdometerKm { get; set; }

    /// <summary>Provenienza, immutabile (R24).</summary>
    public MaintenanceOrigin Origin { get; set; }

    /// <summary>Voce di catalogo che ha generato la riga.</summary>
    public string? SeedTemplateKey { get; set; }

    /// <summary>Versione del catalogo che ha generato la riga.</summary>
    public string? SeedCatalogVersion { get; set; }

    /// <summary>Disattivare non e' cancellare.</summary>
    public bool IsActive { get; set; } = true;

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Token di concorrenza ottimistica.</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>Navigazione verso il camper.</summary>
    public Camper? Camper { get; set; }

    /// <summary>Storico degli interventi.</summary>
    public ICollection<MaintenanceLog> Logs { get; } = [];
}
