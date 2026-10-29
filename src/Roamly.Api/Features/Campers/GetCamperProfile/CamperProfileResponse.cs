using Roamly.Domain.Entities;

namespace Roamly.Api.Features.Campers.GetCamperProfile;

/// <summary>Profilo tecnico completo del camper, senza esporre il token di persistenza.</summary>
public sealed record CamperProfileResponse(
    Guid Id,
    string Name,
    string? Brand,
    string? Model,
    short? Year,
    string? PlateNumber,
    int VehicleKind,
    int? LengthMm,
    int? WidthMm,
    int? HeightMm,
    int? KerbWeightKg,
    int? MaxWeightKg,
    int? FreshWaterL,
    int? GreyWaterL,
    int? FuelTankL,
    int? GasKg,
    int? FuelKind,
    decimal? AverageConsumptionLPer100Km,
    string? ManualSourceName = null,
    string? ManualSourceUrl = null,
    IReadOnlyList<CamperManualMeasurementDto>? ManualMeasurements = null,
    CamperManualViewImagesDto? ManualViewImages = null)
{
    /// <summary>Proietta il mezzo senza esporre dati di ownership o RowVersion nel body.</summary>
    public static CamperProfileResponse From(Camper camper)
    {
        ArgumentNullException.ThrowIfNull(camper);
        return new CamperProfileResponse(
            camper.Id,
            camper.Name,
            camper.Brand,
            camper.Model,
            camper.Year,
            camper.PlateNumber,
            (int)camper.VehicleKind,
            camper.Dimensions?.LengthMm,
            camper.Dimensions?.WidthMm,
            camper.Dimensions?.HeightMm,
            camper.Weights?.KerbWeightKg,
            camper.Weights?.MaxWeightKg,
            camper.Capacities?.FreshWaterL,
            camper.Capacities?.GreyWaterL,
            camper.Capacities?.FuelTankL,
            camper.Capacities?.GasKg,
            camper.FuelKind.HasValue ? (int)camper.FuelKind.Value : null,
            camper.AverageConsumptionLPer100Km,
            camper.ManualSourceName,
            camper.ManualSourceUrl,
            CamperManualMeasurementsSerializer.Deserialize(camper.ManualMeasurementsJson),
            CamperManualMeasurementsSerializer.DeserializeViewImages(camper.ManualMeasurementsJson));
    }
}
