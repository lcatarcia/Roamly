using Roamly.Api.Features.Campers.Equipment;

namespace Roamly.Api.Features.Campers.Equipment.CreateEquipment;

/// <summary>Esito della creazione idempotente di un accessorio.</summary>
public sealed record CreateEquipmentResult(CreateEquipmentOutcome Outcome, EquipmentResponse? Equipment);

/// <summary>Esiti distinti previsti dal contratto POST dell'inventario.</summary>
public enum CreateEquipmentOutcome
{
    /// <summary>Elemento creato.</summary>
    Created,
    /// <summary>Replay del contenuto gia' salvato.</summary>
    Replayed,
    /// <summary>Il camper non appartiene all'utente.</summary>
    NotFound,
    /// <summary>Lo stesso id e' gia' associato a contenuto diverso.</summary>
    Conflict,
}
