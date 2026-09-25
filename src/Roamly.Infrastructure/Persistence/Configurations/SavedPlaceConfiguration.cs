using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="SavedPlace"/> (CONTEXT.md §2.3, FK #20).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class SavedPlaceConfiguration(RoamlyDbContextBase context) : IEntityTypeConfiguration<SavedPlace>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SavedPlace> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.Name).HasNvarchar(120).IsRequired();
        builder.Property(e => e.Category).HasTinyint().IsRequired();
        builder.Property(e => e.Address).HasNvarchar(300);
        builder.Property(e => e.Notes).HasNvarchar(1000);
        builder.Property(e => e.ExternalProvider).HasNvarchar(32);
        builder.Property(e => e.ExternalPlaceId).HasNvarchar(128);
        builder.Property(e => e.ExternalFetchedAtUtc).HasUtcTimestamp();

        builder.ComplexProperty<Coordinates>(e => e.Position, position =>
        {
            position.IsRequired(false);
            position.Property<decimal>(nameof(Coordinates.Latitude)).HasColumnName("Latitude").HasDecimal(8, 6);
            position.Property<decimal>(nameof(Coordinates.Longitude)).HasColumnName("Longitude").HasDecimal(9, 6);
        });

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_SavedPlace_Position",
            "([Latitude] IS NULL AND [Longitude] IS NULL) OR "
            + "([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)"));
    }
}
