using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="Trip"/> (CONTEXT.md §2.3, FK #11).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
/// <param name="includeChecklistNavigations">Se <see langword="false"/>, esclude le checklist non attive.</param>
/// <param name="includeJournalEntryNavigations">Se <see langword="false"/>, esclude il diario non attivo.</param>
public sealed class TripConfiguration(
    RoamlyDbContextBase context,
    bool includeChecklistNavigations = true,
    bool includeJournalEntryNavigations = true) : IEntityTypeConfiguration<Trip>
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
        builder.Property(e => e.EstimatedDistanceKm).HasDecimal(18, 2);
        builder.Property(e => e.EstimatedDurationMinutes).HasColumnType("int");
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.ComplexProperty<Money>(e => e.FuelPricePerLiter, price =>
        {
            price.IsRequired(false);
            price.Property<decimal>(nameof(Money.Amount))
                .HasColumnName("FuelPricePerLiterAmount")
                .HasDecimal(19, 4);
            price.Property<string>(nameof(Money.Currency))
                .HasColumnName("FuelPricePerLiterCurrency")
                .HasIsoCurrency();
        });

        builder.HasOne(e => e.Camper)
            .WithMany()
            .HasForeignKey(e => new { e.OwnerId, e.CamperId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Trip_FuelPricePerLiter_Amount",
                "[FuelPricePerLiterAmount] IS NULL OR [FuelPricePerLiterAmount] > 0");
            table.HasCheckConstraint(
                "CK_Trip_FuelPricePerLiter_Currency",
                "[FuelPricePerLiterCurrency] IS NULL OR "
                + "([FuelPricePerLiterCurrency] = UPPER([FuelPricePerLiterCurrency]) "
                + "AND LEN([FuelPricePerLiterCurrency]) = 3)");
            table.HasCheckConstraint(
                "CK_Trip_FuelPricePerLiter_Pair",
                "([FuelPricePerLiterAmount] IS NULL AND [FuelPricePerLiterCurrency] IS NULL) OR "
                + "([FuelPricePerLiterAmount] IS NOT NULL AND [FuelPricePerLiterCurrency] IS NOT NULL)");
        });

        if (!includeChecklistNavigations)
        {
            builder.Ignore(e => e.Checklists);
        }

        if (!includeJournalEntryNavigations)
        {
            builder.Ignore(e => e.JournalEntries);
        }
    }
}
