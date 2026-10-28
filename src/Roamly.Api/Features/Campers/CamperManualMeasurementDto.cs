using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Roamly.Api.Features.Campers;

/// <summary>Singola quota o specifica tecnica collocata sull'atlante dimensionale del camper.</summary>
public sealed record CamperManualMeasurementDto(
    [property: Required, StringLength(64)] string Id,
    [property: Required, StringLength(120)] string Name,
    [property: Required, StringLength(120)] string Value,
    [property: Required, StringLength(80)] string Zone,
    [property: Required, StringLength(16)] string View,
    [property: Range(0, 100)] int PositionX,
    [property: Range(0, 100)] int PositionY,
    [property: StringLength(300)] string? Description = null,
    [property: Range(1, 2000)] int? Page = null);

/// <summary>Serializzazione JSON deterministica delle misure di dettaglio del camper.</summary>
public static class CamperManualMeasurementsSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Deserializza la collezione di misure salvata sul camper.</summary>
    public static IReadOnlyList<CamperManualMeasurementDto> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<CamperManualMeasurementDto>>(json, SerializerOptions)
                ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Serializza e normalizza le misure di dettaglio; restituisce <c>null</c> se la lista è vuota.</summary>
    public static string? Serialize(IReadOnlyList<CamperManualMeasurementDto>? measurements)
    {
        if (measurements is null || measurements.Count == 0)
        {
            return null;
        }

        var normalized = measurements
            .Select(item => new CamperManualMeasurementDto(
                item.Id.Trim(),
                item.Name.Trim(),
                item.Value.Trim(),
                item.Zone.Trim(),
                item.View.Trim().ToLowerInvariant(),
                Math.Clamp(item.PositionX, 0, 100),
                Math.Clamp(item.PositionY, 0, 100),
                string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                item.Page))
            .ToArray();

        return JsonSerializer.Serialize(normalized, SerializerOptions);
    }
}
