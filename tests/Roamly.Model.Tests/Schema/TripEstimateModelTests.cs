using Microsoft.EntityFrameworkCore;
using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Model.Tests.Schema;

public sealed class TripEstimateModelTests(ModelFixture model)
{
    [Theory]
    [InlineData("ApplicationModel")]
    [InlineData("FullSchemaModel")]
    public void Trip_estimates_are_optional_and_use_the_declared_sql_types(string modelName)
    {
        var efModel = modelName switch
        {
            "ApplicationModel" => model.ApplicationModel,
            "FullSchemaModel" => model.FullSchemaModel,
            _ => throw new ArgumentOutOfRangeException(nameof(modelName)),
        };
        var trip = efModel.FindEntityType(typeof(Trip));

        Assert.NotNull(trip);

        var distance = trip.FindProperty(nameof(Trip.EstimatedDistanceKm));
        Assert.NotNull(distance);
        Assert.Equal(typeof(decimal?), distance.ClrType);
        Assert.True(distance.IsNullable);
        Assert.Equal("decimal(18,2)", distance.GetColumnType());

        var duration = trip.FindProperty(nameof(Trip.EstimatedDurationMinutes));
        Assert.NotNull(duration);
        Assert.Equal(typeof(int?), duration.ClrType);
        Assert.True(duration.IsNullable);
        Assert.Equal("int", duration.GetColumnType());
    }

    [Theory]
    [InlineData("ApplicationModel")]
    [InlineData("FullSchemaModel")]
    public void Trip_fuel_price_is_an_optional_money_complex_type(string modelName)
    {
        var efModel = modelName switch
        {
            "ApplicationModel" => model.ApplicationModel,
            "FullSchemaModel" => model.FullSchemaModel,
            _ => throw new ArgumentOutOfRangeException(nameof(modelName)),
        };
        var trip = efModel.FindEntityType(typeof(Trip));

        Assert.NotNull(trip);
        var fuelPrice = trip.FindComplexProperty("FuelPricePerLiter");

        Assert.NotNull(fuelPrice);
        Assert.Equal(typeof(Money?), fuelPrice.ClrType);
        Assert.True(fuelPrice.IsNullable);
        Assert.Equal("decimal(19,4)", fuelPrice.ComplexType.FindProperty(nameof(Money.Amount))?.GetColumnType());
        Assert.Equal("char(3)", fuelPrice.ComplexType.FindProperty(nameof(Money.Currency))?.GetColumnType());
    }

    [Theory]
    [InlineData("ApplicationModel")]
    [InlineData("FullSchemaModel")]
    public void Trip_fuel_price_amount_and_currency_must_be_both_null_or_both_present(string modelName)
    {
        var efModel = modelName switch
        {
            "ApplicationModel" => model.ApplicationModel,
            "FullSchemaModel" => model.FullSchemaModel,
            _ => throw new ArgumentOutOfRangeException(nameof(modelName)),
        };
        var trip = efModel.FindEntityType(typeof(Trip));

        Assert.NotNull(trip);
        var pairConstraint = Assert.Single(
            trip.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Trip_FuelPricePerLiter_Pair");
        Assert.Equal(
            "([FuelPricePerLiterAmount] IS NULL AND [FuelPricePerLiterCurrency] IS NULL) OR "
            + "([FuelPricePerLiterAmount] IS NOT NULL AND [FuelPricePerLiterCurrency] IS NOT NULL)",
            pairConstraint.Sql);
    }
}
