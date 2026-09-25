using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="Trip"/> (CONTEXT.md §2.3, FK #11).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class TripConfiguration(RoamlyDbContextBase context) : IEntityTypeConfiguration<Trip>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.Name).HasNvarchar(80).IsRequired();
        builder.Property(e => e.Status).HasTinyint().IsRequired();
        builder.Property(e => e.PlannedStartOnUtc).HasDate();
        builder.Property(e => e.PlannedEndOnUtc).HasDate();
        builder.Property(e => e.ActualStartOnUtc).HasDate();
        builder.Property(e => e.ActualEndOnUtc).HasDate();
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Camper)
            .WithMany()
            .HasForeignKey(e => new { e.OwnerId, e.CamperId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
