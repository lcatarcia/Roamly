namespace Roamly.Infrastructure.Maps;

/// <summary>Local admission only: never an authorization to issue a Maps credential.</summary>
public enum MapsAdmissionReason
{
    Admitted, NotConfigured, AccountRestricted, ConsentRequired, Suspended, EvidenceMissing,
    EvidenceInvalid, EvidenceStale, ObserverUnavailable, StorageUnavailable, RefreshInProgress, Superseded,
}

/// <summary>One service-cost total, not a sum of estimates and reported costs. No personal data.</summary>
public sealed record MapsCostEvidence(
    string ResourceId, string Currency, DateTime PeriodStartUtc, DateTime PeriodEndUtc,
    DateTime ObservedAtUtc, DateTime CoveredThroughUtc, double Amount, bool OfferVerified,
    bool CoverageComplete, bool HasSamples);

/// <summary>
/// No operational defaults. UTC calendar months are a local convention, to be verified against
/// the real offer before enabling. The EUR 1 threshold is fixed by the approved product decision.
/// </summary>
public sealed record MapsEconomicPolicy
{
    public bool Enabled { get; init; }
    public string? ResourceId { get; init; }
    public bool UtcCalendarMonthVerified { get; init; }
    public TimeSpan? EvidenceMaxAge { get; init; }
    public TimeSpan? CoverageMaxLag { get; init; }
    public TimeSpan? RefreshLease { get; init; }
    public TimeSpan? ObserverTimeout { get; init; }
    public TimeSpan? SqlLockTimeout { get; init; }
    public const double SuspensionThresholdEur = 1;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ResourceId) && ResourceId.Length <= 450
        && UtcCalendarMonthVerified && Positive(EvidenceMaxAge) && Positive(CoverageMaxLag)
        && Positive(RefreshLease) && Positive(ObserverTimeout) && ObserverTimeout < RefreshLease
        && Positive(SqlLockTimeout) && SqlLockTimeout!.Value.TotalMilliseconds <= int.MaxValue;

    public MapsAdmissionReason Validate(MapsCostEvidence? evidence, DateTime nowUtc)
    {
        if (!IsConfigured) return MapsAdmissionReason.NotConfigured;
        if (evidence is null) return MapsAdmissionReason.EvidenceMissing;
        if (!IsValid(evidence, nowUtc)) return MapsAdmissionReason.EvidenceInvalid;
        return nowUtc - evidence.ObservedAtUtc > EvidenceMaxAge
            || nowUtc - evidence.CoveredThroughUtc > CoverageMaxLag
            ? MapsAdmissionReason.EvidenceStale : MapsAdmissionReason.Admitted;
    }

    private bool IsValid(MapsCostEvidence evidence, DateTime nowUtc) =>
        string.Equals(evidence.ResourceId, ResourceId, StringComparison.Ordinal)
        && evidence.Currency == "EUR" && double.IsFinite(evidence.Amount) && evidence.Amount >= 0
        && evidence.OfferVerified && evidence.CoverageComplete && evidence.HasSamples
        && evidence.PeriodStartUtc == MonthStart(nowUtc) && evidence.PeriodEndUtc == MonthStart(nowUtc).AddMonths(1)
        && evidence.ObservedAtUtc <= nowUtc && evidence.CoveredThroughUtc <= evidence.ObservedAtUtc
        && evidence.ObservedAtUtc >= evidence.PeriodStartUtc
        && evidence.CoveredThroughUtc >= evidence.PeriodStartUtc;

    public static DateTime MonthStart(DateTime utc) => new(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    private static bool Positive(TimeSpan? value) => value.HasValue && value.Value > TimeSpan.Zero;
}

/// <summary>Replaceable observation boundary; no Azure adapter or network implementation is registered.</summary>
public interface IMapsCostObserver
{
    Task<MapsCostEvidence?> ObserveAsync(string resourceId, DateTime periodStartUtc,
        DateTime periodEndUtc, CancellationToken cancellationToken);
}

public sealed class UnavailableMapsCostObserver : IMapsCostObserver
{
    public Task<MapsCostEvidence?> ObserveAsync(string resourceId, DateTime periodStartUtc,
        DateTime periodEndUtc, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Maps economic observer is not configured.");
}
