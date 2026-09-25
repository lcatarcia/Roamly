namespace Roamly.Domain.ValueObjects;

/// <summary>
/// Posizione WGS84 in gradi decimali. Non e' un tipo spaziale (ADR-0001).
/// </summary>
public readonly record struct Coordinates
{
    /// <summary>Costruisce una posizione validando i due intervalli WGS84.</summary>
    /// <param name="latitude">Latitudine in [-90, 90].</param>
    /// <param name="longitude">Longitudine in [-180, 180].</param>
    public Coordinates(decimal latitude, decimal longitude)
    {
        if (latitude is < -90m or > 90m)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "La latitudine deve essere in [-90, 90].");
        }

        if (longitude is < -180m or > 180m)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "La longitudine deve essere in [-180, 180].");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>Latitudine, persistita come <c>decimal(8,6)</c>.</summary>
    public decimal Latitude { get; }

    /// <summary>Longitudine, persistita come <c>decimal(9,6)</c>.</summary>
    public decimal Longitude { get; }
}
