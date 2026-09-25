using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="OdometerReading"/> (CONTEXT.md §2.2, FK #9).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class OdometerReadingConfiguration(RoamlyDbContextBase context)
    : IEntityTypeConfiguration<OdometerReading>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OdometerReading> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.ReadingKm).HasColumnType("int").IsRequired();
        builder.Property(e => e.TakenOnUtc).HasDate().IsRequired();
        builder.Property(e => e.Source).HasTinyint().IsRequired();

        builder.HasOne(e => e.Camper)
            .WithMany(c => c.OdometerReadings)
            .HasForeignKey(e => new { e.OwnerId, e.CamperId })
            .OnDelete(DeleteBehavior.Cascade);

        // Un solo indice per due esigenze: il vincolo "una lettura al giorno per camper" e la
        // query "ultima lettura" (TOP 1 discendente). Le due chiavi sono identiche, e un secondo
        // indice sulle stesse colonne sarebbe un duplicato pagato a ogni scrittura.
        builder.HasIndex(e => new { e.OwnerId, e.CamperId, e.TakenOnUtc })
            .IsDescending(false, false, true)
            .IsUnique();
    }
}
