using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura dell'outbox owner-scoped delle cancellazioni Blob Journal.</summary>
/// <param name="context">Contesto sorgente del query filter.</param>
public sealed class JournalAttachmentDeletionOutboxMessageConfiguration(RoamlyDbContextBase context)
    : IEntityTypeConfiguration<JournalAttachmentDeletionOutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<JournalAttachmentDeletionOutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);
        builder.Property(message => message.StorageKey).HasNvarchar(512).IsRequired();
        builder.HasIndex(message => new { message.OwnerId, message.CreatedAtUtc });
    }
}
