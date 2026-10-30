using Roamly.Api.Features.Trips;

namespace Roamly.Api.Features.Trips.CreateTrip;

/// <summary>Esito del POST idempotente di creazione viaggio.</summary>
public sealed record CreateTripResult(CreateTripOutcome Outcome, TripDetailsResponse? Trip);

/// <summary>Esiti di protocollo della creazione viaggio.</summary>
public enum CreateTripOutcome
{
    /// <summary>Viaggio creato.</summary>
    Created,

    /// <summary>Replay equivalente della creazione.</summary>
    Replayed,

    /// <summary>ID riutilizzato con contenuto diverso.</summary>
    Conflict,

    /// <summary>Camper assente o non posseduto.</summary>
    NotFound,
}
