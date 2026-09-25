using Roamly.Domain.Abstractions;

namespace Roamly.Domain.Entities;

/// <summary>Voce di una <see cref="Checklist"/> (CONTEXT.md §2.3).</summary>
public class ChecklistItem : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Checklist di appartenenza.</summary>
    public Guid ChecklistId { get; set; }

    /// <summary>Testo della voce.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Voce completata.</summary>
    public bool IsDone { get; set; }

    /// <summary>Ordinamento manuale.</summary>
    public int SortOrder { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Navigazione verso la checklist.</summary>
    public Checklist? Checklist { get; set; }
}
