using Roamly.Domain.Entities;

namespace Roamly.TestSupport.Builders;

/// <summary><see cref="Document"/>: documento del camper, Phase 4. Qui esiste solo la topologia.</summary>
public sealed class DocumentBuilder : IEntityBuilder
{
    /// <inheritdoc />
    public Type EntityType => typeof(Document);

    /// <inheritdoc />
    public object Build(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new Document
        {
            Id = context.Ids.NewId(),
            OwnerId = context.OwnerId,
            CamperId = context.Parent<Camper>().Id,
            Title = "Carta di circolazione",
            StorageKey = "documents/carta-circolazione.pdf",
            ContentType = "application/pdf",
            SizeBytes = 240_000,
            IssuedOnUtc = context.Today.AddDays(-2000),
            ExpiresOnUtc = context.Today.AddDays(1000),
            CreatedAtUtc = context.UtcNow,
        };
    }
}
