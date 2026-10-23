namespace Roamly.Api.Features.Campers.ListMaintenance;

/// <summary>Voce di una pagina della collection delle manutenzioni.</summary>
public sealed record ListMaintenanceResponse(
    Guid Id,
    string Name,
    int Category,
    int? IntervalKm,
    short? IntervalMonths,
    DateOnly? LastServiceOnUtc,
    int? LastServiceOdometerKm,
    DateOnly? NextDueOnUtc,
    int? NextDueOdometerKm);
