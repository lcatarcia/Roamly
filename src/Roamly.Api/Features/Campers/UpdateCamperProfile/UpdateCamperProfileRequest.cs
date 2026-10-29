using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Campers.UpdateCamperProfile;

/// <summary>Corpo completo di <c>PUT /api/v1/campers/{camperId}</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record UpdateCamperProfileRequest(
    [property: Required, StringLength(60)] string? Name,
    [property: StringLength(60)] string? Brand,
    [property: StringLength(60)] string? Model,
    [property: Range(1, short.MaxValue)] short? Year,
    [property: StringLength(16)] string? PlateNumber,
    [property: Required, EnumDataType(typeof(VehicleKind))] VehicleKind? VehicleKind,
    [property: Range(1, int.MaxValue)] int? LengthMm,
    [property: Range(1, int.MaxValue)] int? WidthMm,
    [property: Range(1, int.MaxValue)] int? HeightMm,
    [property: Range(1, int.MaxValue)] int? KerbWeightKg,
    [property: Range(1, int.MaxValue)] int? MaxWeightKg,
    [property: Range(1, int.MaxValue)] int? FreshWaterL,
    [property: Range(1, int.MaxValue)] int? GreyWaterL,
    [property: Range(1, int.MaxValue)] int? FuelTankL,
    [property: Range(1, int.MaxValue)] int? GasKg,
    [property: EnumDataType(typeof(FuelKind))] FuelKind? FuelKind,
    decimal? AverageConsumptionLPer100Km,
    [property: StringLength(200)] string? ManualSourceName = null,
    [property: StringLength(500)] string? ManualSourceUrl = null,
    IReadOnlyList<CamperManualMeasurementDto>? ManualMeasurements = null,
    CamperManualViewImagesDto? ManualViewImages = null)
    : IValidatableObject
{
    private static readonly HashSet<string> AllowedViews = new(StringComparer.OrdinalIgnoreCase)
    {
        "side",
        "front",
        "top",
    };

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name is not null && string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult("Il nome e' obbligatorio.", [nameof(Name)]);
        }

        foreach (var validation in ValidateGroup(
                     [LengthMm, WidthMm, HeightMm],
                     ["lengthMm", "widthMm", "heightMm"]))
        {
            yield return validation;
        }

        foreach (var validation in ValidateGroup(
                     [KerbWeightKg, MaxWeightKg],
                     ["kerbWeightKg", "maxWeightKg"]))
        {
            yield return validation;
        }

        foreach (var validation in ValidateGroup(
                     [FreshWaterL, GreyWaterL, FuelTankL, GasKg],
                     ["freshWaterL", "greyWaterL", "fuelTankL", "gasKg"]))
        {
            yield return validation;
        }

        if (KerbWeightKg.HasValue && MaxWeightKg.HasValue && MaxWeightKg < KerbWeightKg)
        {
            yield return new ValidationResult(
                "La massa massima non puo' essere inferiore alla massa in ordine di marcia.",
                [nameof(MaxWeightKg)]);
        }

        if (AverageConsumptionLPer100Km is { } consumption && (consumption < 0.1m || consumption > 999.9m))
        {
            yield return new ValidationResult(
                "Il consumo medio deve rientrare tra 0,1 e 999,9 l/100 km.",
                [nameof(AverageConsumptionLPer100Km)]);
        }

        if (AverageConsumptionLPer100Km is { } precision && decimal.Round(precision, 1) != precision)
        {
            yield return new ValidationResult(
                "Il consumo medio puo' avere al massimo una cifra decimale.",
                [nameof(AverageConsumptionLPer100Km)]);
        }

        if (!string.IsNullOrWhiteSpace(ManualSourceUrl)
            && (!Uri.TryCreate(ManualSourceUrl.Trim(), UriKind.Absolute, out var sourceUri)
                || (sourceUri.Scheme != Uri.UriSchemeHttps && sourceUri.Scheme != Uri.UriSchemeHttp)))
        {
            yield return new ValidationResult(
                "Il link della scheda tecnica deve essere un URL HTTP o HTTPS valido.",
                [nameof(ManualSourceUrl)]);
        }

        if (ManualMeasurements is not null)
        {
            if (ManualMeasurements.Count > 60)
            {
                yield return new ValidationResult(
                    "Non e' possibile salvare piu' di 60 misure di dettaglio.",
                    [nameof(ManualMeasurements)]);
            }

            foreach (var item in ManualMeasurements)
            {
                if (item is null
                    || string.IsNullOrWhiteSpace(item.Id)
                    || string.IsNullOrWhiteSpace(item.Name)
                    || string.IsNullOrWhiteSpace(item.Value)
                    || string.IsNullOrWhiteSpace(item.Zone)
                    || string.IsNullOrWhiteSpace(item.View)
                    || !AllowedViews.Contains(item.View.Trim()))
                {
                    yield return new ValidationResult(
                        "Ogni misura deve specificare identificativo, nome, valore, zona e vista valida (side, front, top).",
                        [nameof(ManualMeasurements)]);
                    break;
                }

                if (item.PositionX is < 0 or > 100 || item.PositionY is < 0 or > 100)
                {
                    yield return new ValidationResult(
                        "Le coordinate sulla mappa devono essere comprese tra 0 e 100.",
                        [nameof(ManualMeasurements)]);
                    break;
                }
            }
        }

        if (ManualViewImages is not null
            && (!IsValidViewImage(ManualViewImages.Side)
                || !IsValidViewImage(ManualViewImages.Front)
                || !IsValidViewImage(ManualViewImages.Top)))
        {
            yield return new ValidationResult(
                "Ogni immagine delle viste deve essere un'immagine PNG, JPEG o WebP valida oppure un URL HTTP/HTTPS.",
                [nameof(ManualViewImages)]);
        }
    }

    private static bool IsValidViewImage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 600_000)
        {
            return false;
        }

        if (trimmed.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("data:image/jpg;base64,", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    private static IEnumerable<ValidationResult> ValidateGroup(int?[] values, string[] fields)
    {
        var presentCount = values.Count(value => value.HasValue);
        if (presentCount != 0 && presentCount != values.Length)
        {
            yield return new ValidationResult(
                "Compila tutti i valori del gruppo tecnico oppure lascialo vuoto.",
                fields);
        }
    }
}
