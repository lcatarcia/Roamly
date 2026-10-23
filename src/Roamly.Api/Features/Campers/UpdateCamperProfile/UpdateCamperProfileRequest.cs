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
    decimal? AverageConsumptionLPer100Km)
    : IValidatableObject
{
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
