using Roamly.Domain.Entities;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Campers.CreateMaintenance;

/// <summary>Rappresentazione API di una regola di manutenzione ricorrente.</summary>
public sealed record CreateMaintenanceResponse(
    Guid Id,
    string Name,
    int Category,
    int? IntervalKm,
    short? IntervalMonths,
    DateOnly? LastServiceOnUtc,
    int? LastServiceOdometerKm,
    DateOnly? NextDueOnUtc,
    int? NextDueOdometerKm)
{
    /// <summary>Proietta i dati della manutenzione senza esporre l'entita' EF.</summary>
    public static CreateMaintenanceResponse From(MaintenanceItem item)
    {
        return new CreateMaintenanceResponse(
            item.Id,
            item.Name,
            (int)item.Category,
            item.IntervalKm,
            item.IntervalMonths,
            item.LastServiceOnUtc,
            item.LastServiceOdometerKm,
            item.NextDueOnUtc,
            item.NextDueOdometerKm);
    }
}
