using Roamly.Domain.Entities;

namespace Roamly.TestSupport.Builders;

/// <summary>Prova sintetica, non un consenso Maps.</summary>
public sealed class ConsentTransitionBuilder : IEntityBuilder
{
    public Type EntityType => typeof(ConsentTransition);

    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ConsentTransition
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            Purpose = "synthetic-render",
            Version = "test-v1",
            TextReference = "synthetic-text-1",
            Action = "grant",
            Revision = 1,
            CreatedAtUtc = context.UtcNow,
        };
    }
}
