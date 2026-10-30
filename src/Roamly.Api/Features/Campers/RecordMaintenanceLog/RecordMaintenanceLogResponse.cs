using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Api.Features.Campers.RecordMaintenanceLog;

/// <summary>Rappresentazione API del fatto immutabile registrato.</summary>
public sealed record RecordMaintenanceLogResponse(
    Guid Id,
    Guid MaintenanceItemId,
    DateOnly PerformedOnUtc,
    int? OdometerKm,
    CostResponse? Cost,
    string? Workshop,
    string? Notes)
{
    /// <summary>Proietta il log senza esporre l'entita' EF.</summary>
    public static RecordMaintenanceLogResponse From(MaintenanceLog log)
    {
        var cost = log.Cost is { } money
            ? new CostResponse(money.Amount, money.Currency)
            : null;
        return new RecordMaintenanceLogResponse(
            log.Id,
            log.MaintenanceItemId,
            log.PerformedOnUtc,
            log.OdometerKm,
            cost,
            log.Workshop,
            log.Notes);
    }
}

/// <summary>Importo e valuta esplicita.</summary>
public sealed record CostResponse(decimal Amount, string Currency);
