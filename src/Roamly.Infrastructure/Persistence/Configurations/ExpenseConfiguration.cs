using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="Expense"/> (CONTEXT.md §2.3, FK #18 e #19).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class ExpenseConfiguration(RoamlyDbContextBase context) : IEntityTypeConfiguration<Expense>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.Category).HasTinyint().IsRequired();
        builder.Property(e => e.IncurredOnUtc).HasDate().IsRequired();
        builder.Property(e => e.Description).HasNvarchar(200);
        builder.Property(e => e.OdometerKm).HasColumnType("int");

        builder.ComplexProperty(e => e.Amount, amount =>
        {
            amount.IsRequired();
            amount.Property<decimal>(nameof(Money.Amount)).HasColumnName("Amount").HasDecimal(19, 4);
            amount.Property<string>(nameof(Money.Currency)).HasColumnName("Currency").HasIsoCurrency();
        });

        builder.HasOne(e => e.Camper)
            .WithMany()
            .HasForeignKey(e => new { e.OwnerId, e.CamperId })
            .OnDelete(DeleteBehavior.Cascade);        // obbligatorio

        // Opzionale — MAI SetNull: lo sganciamento delle spese e' codice applicativo
        // nell'handler DeleteTrip (CONTEXT.md §5.2 caso b).
        builder.HasOne(e => e.Trip)
            .WithMany()
            .HasForeignKey(e => new { e.OwnerId, e.TripId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Expense_Currency",
            "[Currency] = UPPER([Currency]) AND LEN([Currency]) = 3"));

        builder.HasIndex(e => new { e.OwnerId, e.CamperId, e.IncurredOnUtc })
            .IncludeProperties(e => new { e.Category });

        builder.HasIndex(e => new { e.OwnerId, e.TripId })
            .HasFilter("[TripId] IS NOT NULL");
    }
}
