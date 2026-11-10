using Roamly.Domain.Entities;

namespace Roamly.Api.Features.Campers.GetMaintenance;

/// <summary>Dettaglio completo di una manutenzione e dell'ultimo intervento registrato.</summary>
public sealed record MaintenanceDetailResponse(
    Guid Id,
    string Name,
    int Category,
    int? IntervalKm,
    short? IntervalMonths,
    DateOnly? PerformedOnUtc,
    int? OdometerKm,
    MaintenanceDetailCostResponse? Cost,
    string? Workshop,
    string? Notes,
    bool IsUnderWarranty,
    DateOnly? NextDueOnUtc,
    int? NextDueOdometerKm)
{
    /// <summary>Proietta il dettaglio combinando la regola e l'ultimo log disponibile.</summary>
    public static MaintenanceDetailResponse From(MaintenanceItem item, MaintenanceLog? latestLog)
    {
        ArgumentNullException.ThrowIfNull(item);
        var cost = latestLog?.Cost is { } money
            ? new MaintenanceDetailCostResponse(money.Amount, money.Currency)
            : null;
        return new MaintenanceDetailResponse(
            item.Id,
            item.Name,
            (int)item.Category,
            item.IntervalKm,
            item.IntervalMonths,
            latestLog?.PerformedOnUtc ?? item.LastServiceOnUtc,
            latestLog?.OdometerKm ?? item.LastServiceOdometerKm,
            cost,
            latestLog?.Workshop,
            latestLog?.Notes,
            latestLog?.IsUnderWarranty ?? item.LastServiceIsUnderWarranty ?? false,
            item.NextDueOnUtc,
            item.NextDueOdometerKm);
    }
}

/// <summary>Importo e codice valuta ISO 4217 dell'intervento.</summary>
public sealed record MaintenanceDetailCostResponse(decimal Amount, string Currency);
