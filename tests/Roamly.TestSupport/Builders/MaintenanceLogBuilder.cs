using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.TestSupport.Builders;

/// <summary>
/// <see cref="MaintenanceLog"/>: il fatto storico dell'intervento. Popola il complex type
/// opzionale <c>Cost</c> (<see cref="Money"/>), dove vive il costo della manutenzione.
/// </summary>
public sealed class MaintenanceLogBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(MaintenanceLog);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new MaintenanceLog
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            MaintenanceItemId = context.Parent<MaintenanceItem>().Id,
            PerformedOnUtc = context.Today.AddDays(-200),
            OdometerKm = 48000,
            Cost = new Money(189.9m, "EUR"),
            Workshop = "Officina Bianchi",
            Notes = "Sostituito anche il filtro.",
            CreatedAtUtc = context.UtcNow,
        };
    }
}
