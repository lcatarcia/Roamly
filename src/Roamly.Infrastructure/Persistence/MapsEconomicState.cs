namespace Roamly.Infrastructure.Persistence;

/// <summary>R10: global technical service-cost state, deliberately outside the personal ownership graph.</summary>
public sealed class MapsEconomicState
{
    public string ResourceId { get; set; } = string.Empty;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public long Revision { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }
    public bool AlertPending { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public double? ReportedAmountEur { get; set; }
    public DateTime? ObservedAtUtc { get; set; }
    public DateTime? CoveredThroughUtc { get; set; }
}
