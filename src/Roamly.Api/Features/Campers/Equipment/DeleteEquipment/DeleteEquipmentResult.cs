namespace Roamly.Api.Features.Campers.Equipment.DeleteEquipment;

/// <summary>Esito HTTP della rimozione fisica di un accessorio.</summary>
public enum DeleteEquipmentOutcome
{
    /// <summary>La risorsa non appartiene all'utente o al camper specificato.</summary>
    NotFound,
    /// <summary>Header If-Match assente.</summary>
    MissingPrecondition,
    /// <summary>Token errato o superato da una modifica concorrente.</summary>
    PreconditionFailed,
    /// <summary>Elemento cancellato fisicamente.</summary>
    Deleted,
}
