using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="JournalEntryAttachment"/> (ADR-0010).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class JournalEntryAttachmentConfiguration(RoamlyDbContextBase context)
    : IEntityTypeConfiguration<JournalEntryAttachment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<JournalEntryAttachment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(attachment => attachment.JournalEntryId).IsRequired();
        builder.Property(attachment => attachment.StorageKey).HasNvarchar(512).IsRequired();
        builder.Property(attachment => attachment.ContentType)
            .HasMaxLength(64)
            .IsUnicode(false)
            .HasColumnType("varchar(64)")
            .IsRequired();
        builder.Property(attachment => attachment.SizeBytes).HasColumnType("bigint").IsRequired();

        builder.HasOne(attachment => attachment.JournalEntry)
            .WithMany(entry => entry.Attachments)
            .HasForeignKey(attachment => new { attachment.OwnerId, attachment.JournalEntryId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(attachment => new { attachment.OwnerId, attachment.JournalEntryId });
    }
}
