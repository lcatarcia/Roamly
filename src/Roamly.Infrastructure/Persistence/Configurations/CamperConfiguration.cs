using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="Camper"/> (CONTEXT.md §2.2).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class CamperConfiguration(RoamlyDbContextBase context) : IEntityTypeConfiguration<Camper>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Camper> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.Name).HasNvarchar(60).IsRequired();
        builder.Property(e => e.Brand).HasNvarchar(60);
        builder.Property(e => e.Model).HasNvarchar(60);
        builder.Property(e => e.Year).HasColumnType("smallint");
        builder.Property(e => e.PlateNumber).HasNvarchar(16);
        builder.Property(e => e.VehicleKind).HasTinyint().IsRequired();
        builder.Property(e => e.FuelKind).HasTinyint();
        builder.Property(e => e.AverageConsumptionLPer100Km).HasDecimal(4, 1);
        builder.Property(e => e.RowVersion).IsRowVersion();

        // Tre complex type sulla stessa tabella: stessi campi di una CamperSpecification 1:1,
        // senza join, senza FK e senza una riga che puo' mancare (CONTEXT.md §2.2).
        builder.ComplexProperty<Dimensions>(e => e.Dimensions, dimensions =>
        {
            dimensions.IsRequired(false);
            dimensions.Property<int>(nameof(Dimensions.LengthMm)).HasColumnName("LengthMm").HasColumnType("int");
            dimensions.Property<int>(nameof(Dimensions.WidthMm)).HasColumnName("WidthMm").HasColumnType("int");
            dimensions.Property<int>(nameof(Dimensions.HeightMm)).HasColumnName("HeightMm").HasColumnType("int");
        });

        builder.ComplexProperty<Weights>(e => e.Weights, weights =>
        {
            weights.IsRequired(false);
            weights.Property<int>(nameof(Weights.KerbWeightKg)).HasColumnName("KerbWeightKg").HasColumnType("int");
            weights.Property<int>(nameof(Weights.MaxWeightKg)).HasColumnName("MaxWeightKg").HasColumnType("int");
        });

        builder.ComplexProperty<Capacities>(e => e.Capacities, capacities =>
        {
            capacities.IsRequired(false);
            capacities.Property<int>(nameof(Capacities.FreshWaterL)).HasColumnName("FreshWaterL").HasColumnType("int");
            capacities.Property<int>(nameof(Capacities.GreyWaterL)).HasColumnName("GreyWaterL").HasColumnType("int");
            capacities.Property<int>(nameof(Capacities.FuelTankL)).HasColumnName("FuelTankL").HasColumnType("int");
            capacities.Property<int>(nameof(Capacities.GasKg)).HasColumnName("GasKg").HasColumnType("int");
        });

        // Nessun indice (OwnerId) separato: la clustering key lo contiene gia' come colonna guida.
    }
}
