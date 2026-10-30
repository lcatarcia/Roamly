namespace Roamly.Api.Features.Campers.CreateCamper;

/// <summary>Esito dell'operazione di creazione idempotente.</summary>
public enum CreateCamperOutcome
{
    /// <summary>Nuovo camper salvato.</summary>
    Created,

    /// <summary>Richiesta identica a una creazione esistente.</summary>
    Replayed,

    /// <summary>L'id appartiene gia' a contenuto diverso dello stesso proprietario.</summary>
    Conflict,
}

/// <summary>Risultato applicativo tradotto in HTTP dall'endpoint.</summary>
/// <param name="Outcome">Esito della richiesta.</param>
/// <param name="Camper">Risorsa disponibile per successo o replay.</param>
public sealed record CreateCamperResult(CreateCamperOutcome Outcome, CreateCamperResponse? Camper);
