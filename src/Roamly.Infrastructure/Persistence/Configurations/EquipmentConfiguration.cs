using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="Equipment"/> (CONTEXT.md §2.2, FK #3).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class EquipmentConfiguration(RoamlyDbContextBase context) : IEntityTypeConfiguration<Equipment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.Name).HasNvarchar(60).IsRequired();
        builder.Property(e => e.Category).HasTinyint().IsRequired();
        builder.Property(e => e.Notes).HasNvarchar(500);
        builder.Property(e => e.InstalledOnUtc).HasDate();

        builder.HasOne(e => e.Camper)
            .WithMany(c => c.Equipment)
            .HasForeignKey(e => new { e.OwnerId, e.CamperId })
            // Referenzia la PK. NESSUN HasAlternateKey, NESSUN HasPrincipalKey.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.OwnerId, e.CamperId });
    }
}
