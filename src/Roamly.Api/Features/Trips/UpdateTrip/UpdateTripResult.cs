using Roamly.Api.Features.Trips;

namespace Roamly.Api.Features.Trips.UpdateTrip;

/// <summary>Esito del PUT di un viaggio e del suo aggregato di tappe.</summary>
public sealed record UpdateTripResult(
    UpdateTripOutcome Outcome,
    TripDetailsResponse? Trip = null,
    byte[]? RowVersion = null);

/// <summary>Esiti HTTP distinti per ownership, precondizioni e riferimenti delle tappe.</summary>
public enum UpdateTripOutcome
{
    /// <summary>Il viaggio non esiste per l'utente corrente.</summary>
    NotFound,
    /// <summary>La richiesta non contiene If-Match.</summary>
    MissingPrecondition,
    /// <summary>Il token non e' valido, stantio o concorrente.</summary>
    PreconditionFailed,
    /// <summary>Il viaggio e' stato sostituito.</summary>
    Updated,
    /// <summary>Un ID di tappa non appartiene al viaggio o e' duplicato.</summary>
    InvalidStopIds,
    /// <summary>Una tappa rimossa e' ancora referenziata dal diario.</summary>
    ReferencedStopConflict,
}
