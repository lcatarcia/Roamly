using System.Globalization;
using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Api.Features.Trips;

/// <summary>Dettaglio API esplicito di un viaggio e delle sue tappe ordinate.</summary>
public sealed record TripDetailsResponse(
    Guid Id,
    Guid CamperId,
    string Name,
    int Status,
    DateOnly? PlannedStartOnUtc,
    DateOnly? PlannedEndOnUtc,
    DateOnly? ActualStartOnUtc,
    DateOnly? ActualEndOnUtc,
    string? EstimatedDistanceKm,
    int? EstimatedDurationMinutes,
    Money? FuelPricePerLiter,
    IReadOnlyList<TripStopDetailsResponse> Stops)
{
    /// <summary>Proietta il viaggio senza esporre entita' EF Core.</summary>
    public static TripDetailsResponse From(Trip trip)
    {
        ArgumentNullException.ThrowIfNull(trip);
        return new TripDetailsResponse(
            trip.Id,
            trip.CamperId,
            trip.Name,
            (int)trip.Status,
            trip.PlannedStartOnUtc,
            trip.PlannedEndOnUtc,
            trip.ActualStartOnUtc,
            trip.ActualEndOnUtc,
            trip.EstimatedDistanceKm?.ToString(CultureInfo.InvariantCulture),
            trip.EstimatedDurationMinutes,
            trip.FuelPricePerLiter,
            trip.Stops.OrderBy(stop => stop.SequenceNo)
                .Select(stop => TripStopDetailsResponse.From(stop))
                .ToArray());
    }
}

/// <summary>Dettaglio API esplicito di una tappa.</summary>
public sealed record TripStopDetailsResponse(
    Guid Id,
    int SequenceNo,
    string Name,
    DateOnly? ArrivalOnUtc,
    DateOnly? DepartureOnUtc,
    TripStopPositionResponse? Position,
    string? Notes)
{
    /// <summary>Proietta i campi modificabili senza esporre l'entita' EF.</summary>
    public static TripStopDetailsResponse From(Roamly.Domain.Entities.TripStop stop)
    {
        return new TripStopDetailsResponse(
            stop.Id,
            stop.SequenceNo,
            stop.Name,
            stop.ArrivalOnUtc,
            stop.DepartureOnUtc,
            stop.Position is { } position
                ? new TripStopPositionResponse(position.Latitude, position.Longitude)
                : null,
            stop.Notes);
    }
}

/// <summary>Coordinate WGS84 pubbliche della tappa.</summary>
public sealed record TripStopPositionResponse(decimal Latitude, decimal Longitude);
