namespace Roamly.Api.Features.Trips.GetTrip;

/// <summary>Dettaglio di un viaggio e token usato per la concorrenza ottimistica.</summary>
public sealed record GetTripResult(TripDetailsResponse Trip, byte[]? RowVersion);
