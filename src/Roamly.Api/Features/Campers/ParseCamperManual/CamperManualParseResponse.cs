namespace Roamly.Api.Features.Campers.ParseCamperManual;

/// <summary>Bozza strutturata estratta da un manuale PDF o da una scheda tecnica esterna.</summary>
public sealed record CamperManualParseResponse(
    string SourceName,
    string? SourceUrl,
    string? Brand,
    string? Model,
    short? Year,
    int? VehicleKind,
    int? FuelKind,
    int? LengthMm,
    int? WidthMm,
    int? HeightMm,
    int? KerbWeightKg,
    int? MaxWeightKg,
    int? FreshWaterL,
    int? GreyWaterL,
    int? FuelTankL,
    int? GasKg,
    decimal? AverageConsumptionLPer100Km,
    IReadOnlyList<CamperManualMeasurementDto> Measurements,
    IReadOnlyList<string> Warnings);
