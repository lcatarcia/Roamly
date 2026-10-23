namespace Roamly.Api.Features.Trips.TransitionTrip;

/// <summary>Esito HTTP della transizione del viaggio.</summary>
public enum TransitionTripOutcome
{
    /// <summary>Transizione persistita.</summary>
    Transitioned,

    /// <summary>Viaggio assente o non posseduto.</summary>
    NotFound,

    /// <summary>Header If-Match assente.</summary>
    MissingPrecondition,

    /// <summary>ETag malformato, obsoleto o concorrenza rilevata.</summary>
    PreconditionFailed,

    /// <summary>Arco non ammesso dalla matrice di dominio.</summary>
    InvalidTransition,
}

/// <summary>Risultato applicativo della transizione.</summary>
public sealed record TransitionTripResult(
    TransitionTripOutcome Outcome,
    Roamly.Api.Features.Trips.TripDetailsResponse? Trip = null,
    byte[]? RowVersion = null);
