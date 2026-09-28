using Roamly.Domain.Entities;
using Roamly.Domain.Enums;

namespace Roamly.TestSupport.Builders;

/// <summary><see cref="OdometerReading"/>: lettura datata, figlia di <see cref="Camper"/>.</summary>
public sealed class OdometerReadingBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(OdometerReading);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new OdometerReading
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            CamperId = context.Parent<Camper>().Id,
            ReadingKm = 52350,
            TakenOnUtc = context.Today,
            Source = OdometerSource.Manual,
            CreatedAtUtc = context.UtcNow,
        };
    }
}
