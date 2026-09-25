using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="TripStop"/> (CONTEXT.md §2.3, FK #12 e #13).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class TripStopConfiguration(RoamlyDbContextBase context) : IEntityTypeConfiguration<TripStop>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TripStop> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.SequenceNo).HasColumnType("int").IsRequired();
        builder.Property(e => e.Name).HasNvarchar(80).IsRequired();
        builder.Property(e => e.ArrivalOnUtc).HasDate();
        builder.Property(e => e.DepartureOnUtc).HasDate();
        builder.Property(e => e.Notes).HasNvarchar(1000);

        builder.ComplexProperty<Coordinates>(e => e.Position, position =>
        {
            position.IsRequired(false);
            position.Property<decimal>(nameof(Coordinates.Latitude)).HasColumnName("Latitude").HasDecimal(8, 6);
            position.Property<decimal>(nameof(Coordinates.Longitude)).HasColumnName("Longitude").HasDecimal(9, 6);
        });

        builder.HasOne(e => e.Trip)
            .WithMany(t => t.Stops)
            .HasForeignKey(e => new { e.OwnerId, e.TripId })
            .OnDelete(DeleteBehavior.Cascade);

        // FK opzionale verso un'entita' raggiungibile per un'altra strada: NO ACTION, mai SetNull.
        builder.HasOne(e => e.SavedPlace)
            .WithMany(p => p.TripStops)
            .HasForeignKey(e => new { e.OwnerId, e.SavedPlaceId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TripStop_Position",
            "([Latitude] IS NULL AND [Longitude] IS NULL) OR "
            + "([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)"));

        builder.HasIndex(e => new { e.OwnerId, e.TripId, e.SequenceNo });
    }
}
