namespace Roamly.Api.Features.Trips.ListTrips;

/// <summary>Riepilogo API di un viaggio, senza caricare le tappe.</summary>
public sealed record ListTripsResponse(Guid Id, Guid CamperId, string Name, int Status);
