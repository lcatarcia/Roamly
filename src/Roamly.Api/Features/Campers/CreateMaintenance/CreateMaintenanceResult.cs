namespace Roamly.Api.Features.Campers.CreateMaintenance;

/// <summary>Esito del POST idempotente di creazione manutenzione.</summary>
public sealed record CreateMaintenanceResult(CreateMaintenanceOutcome Outcome, CreateMaintenanceResponse? Item);

/// <summary>Esito HTTP tradotto dall'endpoint.</summary>
public enum CreateMaintenanceOutcome
{
    /// <summary>Regola creata.</summary>
    Created,

    /// <summary>Retry equivalente della creazione.</summary>
    Replayed,

    /// <summary>Identificativo riutilizzato con contenuto diverso.</summary>
    Conflict,

    /// <summary>Camper assente o non posseduto.</summary>
    NotFound,
}
