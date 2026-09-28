using Roamly.Domain.Entities;
using Roamly.Domain.Enums;

namespace Roamly.TestSupport.Builders;

/// <summary><see cref="MaintenanceItem"/>: manutenzione ricorrente, figlia di <see cref="Camper"/>.</summary>
public sealed class MaintenanceItemBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(MaintenanceItem);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new MaintenanceItem
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            CamperId = context.Parent<Camper>().Id,
            Name = "Cambio olio motore",
            Category = MaintenanceCategory.Engine,
            IntervalKm = 20000,
            IntervalMonths = 24,
            LastServiceOnUtc = context.Today.AddDays(-200),
            LastServiceOdometerKm = 48000,
            NextDueOnUtc = context.Today.AddDays(530),
            NextDueOdometerKm = 68000,
            Origin = MaintenanceOrigin.User,
            SeedTemplateKey = null,
            SeedCatalogVersion = null,
            IsActive = true,
            CreatedAtUtc = context.UtcNow,
        };
    }
}
