using Roamly.Domain.Abstractions;
using Roamly.Domain.Enums;

namespace Roamly.Domain.Entities;

/// <summary>Lista di controllo legata a un viaggio e a un momento (CONTEXT.md §2.3).</summary>
public class Checklist : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Viaggio di appartenenza.</summary>
    public Guid TripId { get; set; }

    /// <summary>Nome della checklist.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Momento a cui la checklist si riferisce.</summary>
    public ChecklistKind Kind { get; set; }

    /// <summary>Provenienza, stessa semantica di <see cref="MaintenanceItem.Origin"/>.</summary>
    public MaintenanceOrigin Origin { get; set; }

    /// <summary>Voce di catalogo che ha generato la riga.</summary>
    public string? SeedTemplateKey { get; set; }

    /// <summary>Versione del catalogo che ha generato la riga.</summary>
    public string? SeedCatalogVersion { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Navigazione verso il viaggio.</summary>
    public Trip? Trip { get; set; }

    /// <summary>Voci della checklist.</summary>
    public ICollection<ChecklistItem> Items { get; } = [];
}
