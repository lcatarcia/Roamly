using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="MaintenanceItem"/> (CONTEXT.md §2.2, FK #5).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class MaintenanceItemConfiguration(RoamlyDbContextBase context)
    : IEntityTypeConfiguration<MaintenanceItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MaintenanceItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.Name).HasNvarchar(80).IsRequired();
        builder.Property(e => e.Category).HasTinyint().IsRequired();
        builder.Property(e => e.IntervalKm).HasColumnType("int");
        builder.Property(e => e.IntervalMonths).HasColumnType("smallint");
        builder.Property(e => e.LastServiceOnUtc).HasDate();
        builder.Property(e => e.LastServiceOdometerKm).HasColumnType("int");
        builder.Property(e => e.NextDueOnUtc).HasDate();
        builder.Property(e => e.NextDueOdometerKm).HasColumnType("int");
        builder.Property(e => e.Origin).HasTinyint().IsRequired();
        builder.Property(e => e.SeedTemplateKey).HasNvarchar(64);
        builder.Property(e => e.SeedCatalogVersion).HasNvarchar(32);
        builder.Property(e => e.IsActive).HasColumnType("bit").HasDefaultValue(true).IsRequired();
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Camper)
            .WithMany(c => c.MaintenanceItems)
            .HasForeignKey(e => new { e.OwnerId, e.CamperId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
        {
            // Una manutenzione senza ricorrenza non e' un MaintenanceItem, e' una voce di storico.
            table.HasCheckConstraint(
                "CK_MaintenanceItem_Recurrence",
                "[IntervalKm] IS NOT NULL OR [IntervalMonths] IS NOT NULL");

            table.HasCheckConstraint(
                "CK_MaintenanceItem_IntervalKm",
                "[IntervalKm] IS NULL OR [IntervalKm] >= 1");

            table.HasCheckConstraint(
                "CK_MaintenanceItem_IntervalMonths",
                "[IntervalMonths] IS NULL OR [IntervalMonths] >= 1");

            // Origin e SeedTemplateKey si implicano a vicenda: una riga da catalogo senza chiave
            // di catalogo non e' tracciabile, una riga utente con chiave di catalogo mente.
            table.HasCheckConstraint(
                "CK_MaintenanceItem_SeedProvenance",
                "([Origin] = 1 AND [SeedTemplateKey] IS NOT NULL) OR ([Origin] = 0 AND [SeedTemplateKey] IS NULL)");
        });

        builder.HasIndex(e => new { e.OwnerId, e.CamperId })
            .IncludeProperties(e => new { e.NextDueOnUtc, e.NextDueOdometerKm });

        builder.HasIndex(e => new { e.OwnerId, e.NextDueOnUtc })
            .HasFilter("[IsActive] = 1");
    }
}
