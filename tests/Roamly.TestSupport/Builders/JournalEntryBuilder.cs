using Roamly.Domain.Entities;

namespace Roamly.TestSupport.Builders;

/// <summary>
/// <see cref="JournalEntry"/>: nota di viaggio. E' l'entita' del <b>secondo caso 1785</b>
/// (<c>Trip → JournalEntry</c> diretto e <c>Trip → TripStop → JournalEntry</c>): la FK verso
/// la tappa e' opzionale e in <c>NO ACTION</c>, e il builder la popola proprio per esercitarla.
/// </summary>
public sealed class JournalEntryBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(JournalEntry);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new JournalEntry
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            TripId = context.Parent<Trip>().Id,
            TripStopId = context.Parent<TripStop>().Id,
            EntryOnUtc = context.Today.AddDays(31),
            Title = "Primo giorno",
            Body = "Arrivati con la pioggia, ripartiti con il sole.",
            CreatedAtUtc = context.UtcNow,
        };
    }
}
