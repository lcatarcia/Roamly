using Roamly.Domain.Entities;

namespace Roamly.TestSupport.Builders;

/// <summary><see cref="ChecklistItem"/>: voce di una <see cref="Checklist"/>.</summary>
public sealed class ChecklistItemBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(ChecklistItem);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new ChecklistItem
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            ChecklistId = context.Parent<Checklist>().Id,
            Text = "Chiudere le bombole del gas",
            IsDone = false,
            SortOrder = 1,
            CreatedAtUtc = context.UtcNow,
        };
    }
}
