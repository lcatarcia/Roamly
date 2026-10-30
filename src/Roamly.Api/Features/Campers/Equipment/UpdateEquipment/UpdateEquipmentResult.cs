using Roamly.Api.Features.Campers.Equipment;

namespace Roamly.Api.Features.Campers.Equipment.UpdateEquipment;

/// <summary>Esito HTTP dell'aggiornamento con If-Match.</summary>
public sealed record UpdateEquipmentResult(
    UpdateEquipmentOutcome Outcome,
    EquipmentResponse? Equipment = null,
    byte[]? RowVersion = null);

/// <summary>Esiti distinti richiesti da ownership e precondizioni HTTP.</summary>
public enum UpdateEquipmentOutcome
{
    /// <summary>La risorsa non appartiene all'utente o al camper specificato.</summary>
    NotFound,
    /// <summary>Header If-Match assente.</summary>
    MissingPrecondition,
    /// <summary>Token errato o superato da una modifica concorrente.</summary>
    PreconditionFailed,
    /// <summary>Modifica salvata.</summary>
    Updated,
}
