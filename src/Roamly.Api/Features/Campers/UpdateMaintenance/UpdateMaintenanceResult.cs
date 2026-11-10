using Roamly.Api.Features.Campers.GetMaintenance;

namespace Roamly.Api.Features.Campers.UpdateMaintenance;

/// <summary>Esito HTTP dell'aggiornamento di una manutenzione con If-Match.</summary>
public sealed record UpdateMaintenanceResult(
    UpdateMaintenanceOutcome Outcome,
    MaintenanceDetailResponse? Maintenance = null,
    byte[]? RowVersion = null);

/// <summary>Esiti distinti richiesti da ownership, odometro e precondizioni HTTP.</summary>
public enum UpdateMaintenanceOutcome
{
    /// <summary>La risorsa non appartiene all'utente o al camper specificato.</summary>
    NotFound,

    /// <summary>Header If-Match assente.</summary>
    MissingPrecondition,

    /// <summary>Token errato o superato da una modifica concorrente.</summary>
    PreconditionFailed,

    /// <summary>La lettura del contachilometri del giorno indicato ha un valore diverso.</summary>
    OdometerConflict,

    /// <summary>Modifica salvata.</summary>
    Updated,
}
