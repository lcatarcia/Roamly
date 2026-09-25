using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="MaintenanceLog"/> (CONTEXT.md §2.2, FK #7).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class MaintenanceLogConfiguration(RoamlyDbContextBase context)
    : IEntityTypeConfiguration<MaintenanceLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MaintenanceLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.PerformedOnUtc).HasDate().IsRequired();
        builder.Property(e => e.OdometerKm).HasColumnType("int");
        builder.Property(e => e.Workshop).HasNvarchar(80);
        builder.Property(e => e.Notes).HasNvarchar(1000);

        // Il costo dell'intervento vive qui, non in Expense: per questo ExpenseCategory
        // non ha il valore Maintenance (CONTEXT.md §2.2).
        builder.ComplexProperty<Money>(e => e.Cost, cost =>
        {
            cost.IsRequired(false);
            cost.Property<decimal>(nameof(Money.Amount)).HasColumnName("CostAmount").HasDecimal(19, 4);
            cost.Property<string>(nameof(Money.Currency)).HasColumnName("CostCurrency").HasIsoCurrency();
        });

        builder.HasOne(e => e.MaintenanceItem)
            .WithMany(i => i.Logs)
            .HasForeignKey(e => new { e.OwnerId, e.MaintenanceItemId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_MaintenanceLog_CostCurrency",
            "[CostCurrency] IS NULL OR ([CostCurrency] = UPPER([CostCurrency]) AND LEN([CostCurrency]) = 3)"));

        builder.HasIndex(e => new { e.OwnerId, e.MaintenanceItemId, e.PerformedOnUtc })
            .IsDescending(false, false, true);
    }
}
