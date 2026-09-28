using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Domain.ValueObjects;

namespace Roamly.TestSupport.Builders;

/// <summary>
/// <see cref="SavedPlace"/>: figlio diretto di <c>AspNetUsers</c>, senza padre di dominio.
/// La posizione usa <b>due decimal</b> (complex type <see cref="Coordinates"/>), non un
/// <c>Point</c> NTS: nessun SRID da impostare (TESTING.md §9, ADR-0001).
/// </summary>
public sealed class SavedPlaceBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(SavedPlace);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new SavedPlace
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            Name = "Aire de Carnac",
            Category = SavedPlaceCategory.Aire,
            Position = new Coordinates(47.584_000m, -3.079_000m),
            Address = "Route de Plouharnel, Carnac",
            Notes = "Scarico acque grigie disponibile.",
            ExternalProvider = "manual",
            ExternalPlaceId = "carnac-aire-1",
            ExternalFetchedAtUtc = context.UtcNow,
            CreatedAtUtc = context.UtcNow,
        };
    }
}
