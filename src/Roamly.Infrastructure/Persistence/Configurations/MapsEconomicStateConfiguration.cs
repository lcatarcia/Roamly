using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Roamly.Infrastructure.Persistence.Configurations;

public sealed class MapsEconomicStateConfiguration : IEntityTypeConfiguration<MapsEconomicState>
{
    public void Configure(EntityTypeBuilder<MapsEconomicState> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("MapsEconomicStates", ConfigureConstraints);
        builder.HasKey(state => state.ResourceId);
        builder.Property(state => state.ResourceId).HasMaxLength(450).ValueGeneratedNever();
        builder.Property(state => state.SuspensionReason).HasMaxLength(32);
        builder.Property(state => state.Revision).IsConcurrencyToken();
        builder.Property(state => state.ReportedAmountEur).HasColumnType("float");
        ConfigureDates(builder);
    }

    private static void ConfigureConstraints(TableBuilder<MapsEconomicState> table)
    {
        table.HasCheckConstraint("CK_MapsEconomicState_Period",
            "[PeriodEndUtc] > [PeriodStartUtc] AND [Revision] >= 1 AND LEN([ResourceId]) > 0");
        table.HasCheckConstraint("CK_MapsEconomicState_Lease",
            "([LeaseId] IS NULL AND [LeaseExpiresAtUtc] IS NULL) OR ([LeaseId] IS NOT NULL AND [LeaseExpiresAtUtc] IS NOT NULL)");
        ConfigureSuspensionConstraints(table);
        table.HasCheckConstraint("CK_MapsEconomicState_Evidence",
            "([ReportedAmountEur] IS NULL AND [ObservedAtUtc] IS NULL AND [CoveredThroughUtc] IS NULL) OR " +
            "([ReportedAmountEur] IS NOT NULL AND [ReportedAmountEur] >= 0 AND [ObservedAtUtc] IS NOT NULL " +
            "AND [CoveredThroughUtc] IS NOT NULL AND [CoveredThroughUtc] >= [PeriodStartUtc] " +
            "AND [CoveredThroughUtc] <= [ObservedAtUtc] AND [ObservedAtUtc] < [PeriodEndUtc])");
    }

    private static void ConfigureSuspensionConstraints(TableBuilder<MapsEconomicState> table)
    {
        table.HasCheckConstraint("CK_MapsEconomicState_Suspension",
            "([SuspensionReason] IS NULL AND [SuspendedAtUtc] IS NULL AND [AlertPending] = 0) OR " +
            "([SuspensionReason] IS NOT NULL AND [SuspensionReason] = 'ThresholdReached' " +
            "AND [SuspendedAtUtc] IS NOT NULL AND [AlertPending] = 1 " +
            "AND [ReportedAmountEur] IS NOT NULL AND [ReportedAmountEur] >= 1)");
        table.HasCheckConstraint("CK_MapsEconomicState_Threshold",
            "[ReportedAmountEur] IS NULL OR [ReportedAmountEur] < 1 OR [SuspensionReason] IS NOT NULL");
    }

    private static void ConfigureDates(EntityTypeBuilder<MapsEconomicState> builder)
    {
        // Full tick precision prevents lease/evidence rounding from extending validity.
        builder.Property(state => state.PeriodStartUtc).HasColumnType("datetime2(7)");
        builder.Property(state => state.PeriodEndUtc).HasColumnType("datetime2(7)");
        builder.Property(state => state.SuspendedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(state => state.LeaseExpiresAtUtc).HasColumnType("datetime2(7)");
        builder.Property(state => state.ObservedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(state => state.CoveredThroughUtc).HasColumnType("datetime2(7)");
    }
}
