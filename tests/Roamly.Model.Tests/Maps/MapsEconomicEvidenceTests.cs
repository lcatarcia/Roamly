using Roamly.Infrastructure.Maps;

namespace Roamly.Model.Tests.Maps;

public sealed class MapsEconomicEvidenceTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);
    private static readonly MapsEconomicPolicy Policy = new()
    {
        Enabled = true, ResourceId = "synthetic-maps", UtcCalendarMonthVerified = true,
        EvidenceMaxAge = TimeSpan.FromMinutes(5), CoverageMaxLag = TimeSpan.FromMinutes(10),
        RefreshLease = TimeSpan.FromMinutes(1), ObserverTimeout = TimeSpan.FromSeconds(10),
        SqlLockTimeout = TimeSpan.FromSeconds(2),
    };
    private static MapsCostEvidence Evidence => new("synthetic-maps", "EUR",
        MapsEconomicPolicy.MonthStart(Now), MapsEconomicPolicy.MonthStart(Now).AddMonths(1),
        Now, Now, 0.99, true, true, true);

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.01)]
    public void Non_finite_or_negative_totals_cannot_be_trusted(double amount) =>
        Assert.Equal(MapsAdmissionReason.EvidenceInvalid, Policy.Validate(Evidence with { Amount = amount }, Now));

    [Fact]
    public void Missing_samples_never_mean_zero_cost() =>
        Assert.Equal(MapsAdmissionReason.EvidenceInvalid,
            Policy.Validate(Evidence with { HasSamples = false, Amount = 0 }, Now));

    [Fact]
    public void Only_the_verified_resource_currency_month_offer_and_complete_coverage_are_accepted()
    {
        MapsCostEvidence[] invalid =
        [
            Evidence with { ResourceId = "other" }, Evidence with { Currency = "USD" },
            Evidence with { PeriodStartUtc = Evidence.PeriodStartUtc.AddMonths(-1) },
            Evidence with { PeriodEndUtc = Evidence.PeriodEndUtc.AddMonths(-1) },
            Evidence with { ObservedAtUtc = Now.AddTicks(1) },
            Evidence with { CoveredThroughUtc = Now.AddTicks(1) },
            Evidence with { CoveredThroughUtc = Evidence.PeriodStartUtc.AddTicks(-1) },
            Evidence with { OfferVerified = false }, Evidence with { CoverageComplete = false },
        ];
        Assert.All(invalid, evidence => Assert.Equal(MapsAdmissionReason.EvidenceInvalid, Policy.Validate(evidence, Now)));
        Assert.Equal(MapsAdmissionReason.EvidenceMissing, Policy.Validate(null, Now));
        Assert.Equal(MapsAdmissionReason.Admitted, Policy.Validate(Evidence, Now));
    }

    [Fact]
    public void Freshly_collected_old_coverage_and_stale_observations_are_denied()
    {
        Assert.Equal(MapsAdmissionReason.EvidenceStale,
            Policy.Validate(Evidence with { CoveredThroughUtc = Now.AddMinutes(-11) }, Now));
        Assert.Equal(MapsAdmissionReason.EvidenceStale,
            Policy.Validate(Evidence with { ObservedAtUtc = Now.AddMinutes(-6), CoveredThroughUtc = Now.AddMinutes(-6) }, Now));
    }
}
