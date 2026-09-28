using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Domain.ValueObjects;

namespace Roamly.TestSupport.Builders;

/// <summary>
/// <see cref="Camper"/>: radice pratica del dominio, figlia diretta di <c>AspNetUsers</c>.
/// Popola i tre complex type opzionali, secondo la convenzione di TESTING.md §9
/// (tutti i campi non-nullable, almeno un campo nullable per complex type).
/// </summary>
public sealed class CamperBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(Camper);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new Camper
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            Name = "Camper di prova",
            Brand = "Hymer",
            Model = "B-Klasse",
            Year = 2019,
            PlateNumber = "AB123CD",
            VehicleKind = VehicleKind.Coachbuilt,
            FuelKind = FuelKind.Diesel,
            AverageConsumptionLPer100Km = 11.5m,
            Dimensions = new Dimensions(7400, 2300, 2900),
            Weights = new Weights(3100, 3500),
            Capacities = new Capacities(120, 100, 90, 22),
            CreatedAtUtc = context.UtcNow,
        };
    }
}
