using Roamly.Api.Features.Campers.GetCamperProfile;

namespace Roamly.Api.Features.Campers.UpdateCamperProfile;

/// <summary>Esito HTTP di protocollo dell'aggiornamento ottimistico del profilo.</summary>
public sealed record UpdateCamperProfileResult(
    UpdateCamperProfileOutcome Outcome,
    CamperProfileResponse? Profile = null,
    byte[]? RowVersion = null);

/// <summary>Esiti distinti per ownership e precondizioni HTTP.</summary>
public enum UpdateCamperProfileOutcome
{
    /// <summary>La risorsa non esiste per l'utente corrente.</summary>
    NotFound,
    /// <summary>La richiesta non conteneva If-Match.</summary>
    MissingPrecondition,
    /// <summary>Il token non e' valido o non e' piu' corrente.</summary>
    PreconditionFailed,
    /// <summary>Il profilo e' stato aggiornato.</summary>
    Updated,
}
