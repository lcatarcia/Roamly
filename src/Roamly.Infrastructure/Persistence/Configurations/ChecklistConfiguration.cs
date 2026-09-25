using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="Checklist"/> (CONTEXT.md §2.3, FK #14).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class ChecklistConfiguration(RoamlyDbContextBase context) : IEntityTypeConfiguration<Checklist>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Checklist> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.Name).HasNvarchar(80).IsRequired();
        builder.Property(e => e.Kind).HasTinyint().IsRequired();
        builder.Property(e => e.Origin).HasTinyint().IsRequired();
        builder.Property(e => e.SeedTemplateKey).HasNvarchar(64);
        builder.Property(e => e.SeedCatalogVersion).HasNvarchar(32);

        builder.HasOne(e => e.Trip)
            .WithMany(t => t.Checklists)
            .HasForeignKey(e => new { e.OwnerId, e.TripId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Checklist_SeedProvenance",
            "([Origin] = 1 AND [SeedTemplateKey] IS NOT NULL) OR ([Origin] = 0 AND [SeedTemplateKey] IS NULL)"));
    }
}
