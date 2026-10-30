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

/// <summary>Immagini personalizzate opzionali (data URL o URL HTTP/HTTPS) per le tre viste del camper.</summary>
public sealed record CamperManualViewImagesDto(
    [property: StringLength(600_000)] string? Side = null,
    [property: StringLength(600_000)] string? Front = null,
    [property: StringLength(600_000)] string? Top = null);

/// <summary>Serializzazione JSON deterministica delle misure di dettaglio e delle viste personalizzate del camper.</summary>
public static class CamperManualMeasurementsSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private sealed record StoredManualEnvelope(
        IReadOnlyList<CamperManualMeasurementDto>? Measurements,
        CamperManualViewImagesDto? ViewImages);

    /// <summary>Deserializza la collezione di misure salvata sul camper.</summary>
    public static IReadOnlyList<CamperManualMeasurementDto> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var trimmed = json.AsSpan().TrimStart();
        try
        {
            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                var envelope = JsonSerializer.Deserialize<StoredManualEnvelope>(json, SerializerOptions);
                return envelope?.Measurements ?? [];
            }

            return JsonSerializer.Deserialize<List<CamperManualMeasurementDto>>(json, SerializerOptions)
                ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Deserializza le immagini personalizzate delle viste salvate sul camper.</summary>
    public static CamperManualViewImagesDto? DeserializeViewImages(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var trimmed = json.AsSpan().TrimStart();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<StoredManualEnvelope>(json, SerializerOptions);
            return NormalizeViewImages(envelope?.ViewImages);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Serializza e normalizza le misure di dettaglio e le immagini personalizzate delle viste.</summary>
    public static string? Serialize(
        IReadOnlyList<CamperManualMeasurementDto>? measurements,
        CamperManualViewImagesDto? viewImages = null)
    {
        var normalizedMeasurements = (measurements ?? [])
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

        var normalizedImages = NormalizeViewImages(viewImages);

        if (normalizedMeasurements.Length == 0 && normalizedImages is null)
        {
            return null;
        }

        if (normalizedImages is null)
        {
            return JsonSerializer.Serialize(normalizedMeasurements, SerializerOptions);
        }

        var envelope = new StoredManualEnvelope(normalizedMeasurements, normalizedImages);
        return JsonSerializer.Serialize(envelope, SerializerOptions);
    }

    private static CamperManualViewImagesDto? NormalizeViewImages(CamperManualViewImagesDto? viewImages)
    {
        if (viewImages is null)
        {
            return null;
        }

        var side = string.IsNullOrWhiteSpace(viewImages.Side) ? null : viewImages.Side.Trim();
        var front = string.IsNullOrWhiteSpace(viewImages.Front) ? null : viewImages.Front.Trim();
        var top = string.IsNullOrWhiteSpace(viewImages.Top) ? null : viewImages.Top.Trim();

        if (side is null && front is null && top is null)
        {
            return null;
        }

        return new CamperManualViewImagesDto(side, front, top);
    }
}
