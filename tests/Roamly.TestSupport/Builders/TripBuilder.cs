using Roamly.Domain.Entities;

namespace Roamly.TestSupport.Builders;

/// <summary>
/// <see cref="Trip"/>: figlio obbligatorio di <see cref="Camper"/>. Lo stato resta
/// <c>Planned</c>: si cambia solo con <c>TransitionTo</c>, e un builder non e' il posto
/// dove esercitare la matrice di transizione.
/// </summary>
public sealed class TripBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(Trip);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new Trip
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            CamperId = context.Parent<Camper>().Id,
            Name = "Bretagna in agosto",
            PlannedStartOnUtc = context.Today.AddDays(30),
            PlannedEndOnUtc = context.Today.AddDays(44),
            ActualStartOnUtc = null,
            ActualEndOnUtc = null,
            CreatedAtUtc = context.UtcNow,
        };
    }
}
