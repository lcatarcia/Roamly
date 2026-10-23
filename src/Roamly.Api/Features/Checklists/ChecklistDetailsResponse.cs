using Roamly.Domain.Entities;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Checklists;

/// <summary>Dettaglio pubblico di una checklist e delle sue voci ordinate.</summary>
public sealed record ChecklistDetailsResponse(
    Guid Id,
    Guid TripId,
    string Name,
    ChecklistKind Kind,
    IReadOnlyList<ChecklistItemDetailsResponse> Items)
{
    /// <summary>Proietta la checklist senza esporre entita' EF Core.</summary>
    public static ChecklistDetailsResponse From(Checklist checklist)
    {
        ArgumentNullException.ThrowIfNull(checklist);
        return new ChecklistDetailsResponse(
            checklist.Id,
            checklist.TripId,
            checklist.Name,
            checklist.Kind,
            checklist.Items.OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select(ChecklistItemDetailsResponse.From)
                .ToArray());
    }
}

/// <summary>Dettaglio pubblico di una voce di checklist.</summary>
public sealed record ChecklistItemDetailsResponse(
    Guid Id,
    string Text,
    bool IsDone,
    int SortOrder)
{
    /// <summary>Proietta una voce senza esporre l'entita' EF Core.</summary>
    public static ChecklistItemDetailsResponse From(ChecklistItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ChecklistItemDetailsResponse(item.Id, item.Text, item.IsDone, item.SortOrder);
    }
}
