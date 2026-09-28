using Roamly.Domain.Entities;
using Roamly.Domain.Enums;

namespace Roamly.TestSupport.Builders;

/// <summary><see cref="Checklist"/>: figlia di <see cref="Trip"/> in cascade.</summary>
public sealed class ChecklistBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(Checklist);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new Checklist
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            TripId = context.Parent<Trip>().Id,
            Name = "Partenza",
            Kind = ChecklistKind.Departure,
            Origin = MaintenanceOrigin.User,
            SeedTemplateKey = null,
            SeedCatalogVersion = null,
            CreatedAtUtc = context.UtcNow,
        };
    }
}
