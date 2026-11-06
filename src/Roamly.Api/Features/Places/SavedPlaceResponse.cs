using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Api.Features.Places;

/// <summary>Dettagli manuali esposti per un luogo salvato.</summary>
public sealed record SavedPlaceResponse(
    Guid Id,
    string Name,
    int Category,
    SavedPlacePositionResponse? Position,
    string? Address,
    string? Notes,
    string? GoogleMapsUrl)
{
    /// <summary>Converte la risorsa persistita nel contratto API.</summary>
    public static SavedPlaceResponse From(SavedPlace place)
    {
        ArgumentNullException.ThrowIfNull(place);
        return new SavedPlaceResponse(
            place.Id,
            place.Name,
            (int)place.Category,
            SavedPlacePositionResponse.From(place.Position),
            place.Address,
            place.Notes,
            place.GoogleMapsUrl);
    }
}

/// <summary>Coordinate salvate manualmente, se presenti.</summary>
public sealed record SavedPlacePositionResponse(decimal Latitude, decimal Longitude)
{
    /// <summary>Converte le coordinate del dominio nel contratto API.</summary>
    public static SavedPlacePositionResponse? From(Coordinates? position)
    {
        return position is null
            ? null
            : new SavedPlacePositionResponse(position.Value.Latitude, position.Value.Longitude);
    }
}
