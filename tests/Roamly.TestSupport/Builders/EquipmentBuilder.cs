using Roamly.Domain.Entities;
using Roamly.Domain.Enums;

namespace Roamly.TestSupport.Builders;

/// <summary><see cref="Equipment"/>: figlio di <see cref="Camper"/> in cascade.</summary>
public sealed class EquipmentBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(Equipment);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new Equipment
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            CamperId = context.Parent<Camper>().Id,
            Name = "Pannello solare",
            Category = EquipmentCategory.Solar,
            Notes = "160 W, installato sul tetto.",
            InstalledOnUtc = context.Today.AddDays(-400),
            CreatedAtUtc = context.UtcNow,
        };
    }
}
