using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Domain.ValueObjects;

namespace Roamly.TestSupport.Builders;

/// <summary>
/// <see cref="Expense"/>: <c>CamperId</c> obbligatorio e in cascade, <c>TripId</c> opzionale e in
/// <c>NO ACTION</c> — il primo caso 1785 di CONTEXT.md §5.2. Il builder popola entrambe.
/// </summary>
public sealed class ExpenseBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(Expense);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new Expense
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            CamperId = context.Parent<Camper>().Id,
            TripId = context.Parent<Trip>().Id,
            Amount = new Money(72.4m, "EUR"),
            Category = ExpenseCategory.Fuel,
            IncurredOnUtc = context.Today.AddDays(31),
            Description = "Pieno di gasolio a Vannes.",
            OdometerKm = 52800,
            CreatedAtUtc = context.UtcNow,
        };
    }
}
