using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="Document"/> (CONTEXT.md §2.3, FK #21, Phase 4).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class DocumentConfiguration(RoamlyDbContextBase context) : IEntityTypeConfiguration<Document>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.Title).HasNvarchar(120).IsRequired();
        builder.Property(e => e.StorageKey).HasNvarchar(400).IsRequired();
        builder.Property(e => e.ContentType).HasNvarchar(100);
        builder.Property(e => e.SizeBytes).HasColumnType("int");
        builder.Property(e => e.IssuedOnUtc).HasDate();
        builder.Property(e => e.ExpiresOnUtc).HasDate();

        builder.HasOne(e => e.Camper)
            .WithMany()
            .HasForeignKey(e => new { e.OwnerId, e.CamperId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
