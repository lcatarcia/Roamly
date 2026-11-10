namespace Roamly.Api.Features.Campers.DeleteMaintenance;

/// <summary>Esito HTTP della rimozione fisica di una manutenzione.</summary>
public enum DeleteMaintenanceOutcome
{
    /// <summary>La risorsa non appartiene all'utente o al camper specificato.</summary>
    NotFound,

    /// <summary>Header If-Match assente.</summary>
    MissingPrecondition,

    /// <summary>Token errato o superato da una modifica concorrente.</summary>
    PreconditionFailed,

    /// <summary>Manutenzione cancellata fisicamente.</summary>
    Deleted,
}
