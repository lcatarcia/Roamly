using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.TestSupport.Builders;

/// <summary>
/// <see cref="TripStop"/>: tappa di un viaggio, con FK opzionale verso <see cref="SavedPlace"/>
/// in <c>NO ACTION</c>. La posizione e' uno snapshot in due <c>decimal</c>, non un tipo spaziale.
/// </summary>
public sealed class TripStopBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(TripStop);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new TripStop
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            TripId = context.Parent<Trip>().Id,
            SavedPlaceId = context.Parent<SavedPlace>().Id,
            SequenceNo = 1,
            Name = "Carnac",
            Position = new Coordinates(47.584_000m, -3.079_000m),
            ArrivalOnUtc = context.Today.AddDays(31),
            DepartureOnUtc = context.Today.AddDays(33),
            Notes = "Due notti, visita ai menhir.",
            CreatedAtUtc = context.UtcNow,
        };
    }
}
