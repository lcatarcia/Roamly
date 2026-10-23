using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Api.Features.Campers.ListMaintenanceLogs;

/// <summary>Voce dello storico immutabile degli interventi.</summary>
public sealed record ListMaintenanceLogsResponse(
    Guid Id,
    DateOnly PerformedOnUtc,
    int? OdometerKm,
    CostResponse? Cost,
    string? Workshop,
    string? Notes)
{
    /// <summary>Proietta il log senza esporre l'entita' EF.</summary>
    public static ListMaintenanceLogsResponse From(MaintenanceLog log)
    {
        var cost = log.Cost is { } money
            ? new CostResponse(money.Amount, money.Currency)
            : null;
        return new ListMaintenanceLogsResponse(
            log.Id,
            log.PerformedOnUtc,
            log.OdometerKm,
            cost,
            log.Workshop,
            log.Notes);
    }
}

/// <summary>Importo con valuta esplicita.</summary>
public sealed record CostResponse(decimal Amount, string Currency);
