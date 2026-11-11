using System.ComponentModel.DataAnnotations;
using Roamly.Api.Features.Campers;
using Roamly.Api.Features.Campers.UpdateCamperProfile;
using Roamly.Domain.Enums;

namespace Roamly.Model.Tests;

public sealed class CamperManualViewImagesTests
{
    private const string ValidWebpDataUrl =
        "data:image/webp;base64,UklGRiQAAABXRUJQVlA4IBgAAAAwAQCdASoBAAEAAwA0JaQAA3AA/vuUAAA=";

    [Fact]
    public void serializer_round_trips_hero_image_and_preserves_measurements()
    {
        var measurements = new[]
        {
            new CamperManualMeasurementDto(
                "external-length",
                "Lunghezza esterna",
                "5.413 mm",
                "chassis",
                "side",
                50,
                84),
        };
        var viewImages = new CamperManualViewImagesDto(
            Side: ValidWebpDataUrl,
            Front: null,
            Top: "  https://example.test/top.webp  ",
            Hero: ValidWebpDataUrl);

        var json = CamperManualMeasurementsSerializer.Serialize(measurements, viewImages);
        Assert.NotNull(json);

        var deserializedMeasurements = CamperManualMeasurementsSerializer.Deserialize(json);
        var deserializedImages = CamperManualMeasurementsSerializer.DeserializeViewImages(json);
        Assert.Single(deserializedMeasurements);
        Assert.NotNull(deserializedImages);
        Assert.Equal(ValidWebpDataUrl, deserializedImages!.Side);
        Assert.Null(deserializedImages.Front);
        Assert.Equal("https://example.test/top.webp", deserializedImages.Top);
        Assert.Equal(ValidWebpDataUrl, deserializedImages.Hero);
    }

    [Fact]
    public void update_camper_profile_request_validates_hero_image_format()
    {
        var validRequest = new UpdateCamperProfileRequest(
            Name: "Adria",
            Brand: "Adria",
            Model: "Twin 540 SP Axess",
            Year: 2025,
            PlateNumber: "GY301FG",
            VehicleKind: VehicleKind.Van,
            LengthMm: 5413,
            WidthMm: 2050,
            HeightMm: 2580,
            KerbWeightKg: null,
            MaxWeightKg: null,
            FreshWaterL: 100,
            GreyWaterL: 70,
            FuelTankL: 90,
            GasKg: 20,
            FuelKind: FuelKind.Diesel,
            AverageConsumptionLPer100Km: null,
            ManualViewImages: new CamperManualViewImagesDto(Hero: ValidWebpDataUrl));

        Assert.Empty(validRequest.Validate(new ValidationContext(validRequest)));

        var invalidRequest = validRequest with
        {
            ManualViewImages = new CamperManualViewImagesDto(Hero: "javascript:alert(1)"),
        };

        var errors = invalidRequest.Validate(new ValidationContext(invalidRequest)).ToList();
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(UpdateCamperProfileRequest.ManualViewImages)));
    }
}
