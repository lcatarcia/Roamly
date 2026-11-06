using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Roamly.Infrastructure.Maps;
using Roamly.Infrastructure.Persistence;

namespace Roamly.IntegrationTests.Maps;

public sealed partial class MapsEconomicGateTests
{
    [Fact]
    public async Task Observer_timeout_is_fail_closed_even_if_provider_ignores_cancellation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource<MapsCostEvidence?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new FakeObserver((_, _, _, _) => { entered.SetResult(); return late.Task; });
        await using var db = CreateDb();
        var check = Gate(db, observer).CheckAsync(Ct);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
            _clock.Advance(Policy.ObserverTimeout!.Value);
            Assert.Equal(MapsAdmissionReason.ObserverUnavailable, await check);
        }
        finally { late.TrySetResult(Evidence()); }
        Assert.Null((await db.MapsEconomicStates.AsNoTracking().SingleAsync(Ct)).ReportedAmountEur);
    }

    [Theory]
    [InlineData("missing", MapsAdmissionReason.EvidenceMissing)]
    [InlineData("no-samples", MapsAdmissionReason.EvidenceInvalid)]
    [InlineData("old-coverage", MapsAdmissionReason.EvidenceStale)]
    [InlineData("old-month", MapsAdmissionReason.EvidenceInvalid)]
    [InlineData("currency", MapsAdmissionReason.EvidenceInvalid)]
    [InlineData("future", MapsAdmissionReason.EvidenceInvalid)]
    [InlineData("incomplete", MapsAdmissionReason.EvidenceInvalid)]
    [InlineData("error", MapsAdmissionReason.ObserverUnavailable)]
    public async Task Unreliable_observations_deny_without_caching_an_apparent_zero(
        string scenario, MapsAdmissionReason reason)
    {
        await using var db = CreateDb();
        var observer = new FakeObserver((_, _, _, _) => scenario == "error"
            ? throw new InvalidOperationException("private-provider-detail")
            : Task.FromResult<MapsCostEvidence?>(scenario switch
            {
                "missing" => null,
                "no-samples" => Evidence(0) with { HasSamples = false },
                "old-coverage" => Evidence(0) with { CoveredThroughUtc = _clock.GetUtcNow().UtcDateTime.AddHours(-1) },
                "old-month" => Evidence(0) with { PeriodStartUtc = Evidence().PeriodStartUtc.AddMonths(-1) },
                "currency" => Evidence(0) with { Currency = "USD" },
                "future" => Evidence(0) with { ObservedAtUtc = _clock.GetUtcNow().UtcDateTime.AddDays(1) },
                _ => Evidence(0) with { CoverageComplete = false },
            }));
        var log = new AdmissionLog<MapsEconomicGate>();
        var gate = new MapsEconomicGate(db, Policy, observer, _clock, log);
        Assert.Equal(reason, await gate.CheckAsync(Ct));
        Assert.Contains(log.Messages, message => message.Contains(reason.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(log.Messages, message => message.Contains("private-provider-detail", StringComparison.Ordinal));
        var state = await db.MapsEconomicStates.AsNoTracking().SingleAsync(Ct);
        Assert.Null(state.ReportedAmountEur);
        Assert.False(state.AlertPending);
        var valid = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(Evidence()));
        Assert.Equal(MapsAdmissionReason.Admitted, await Gate(db, valid).CheckAsync(Ct));
    }

    [Fact]
    public async Task Stale_evidence_is_refreshed_on_open_instead_of_reused()
    {
        await using var db = CreateDb();
        var low = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(Evidence()));
        Assert.Equal(MapsAdmissionReason.Admitted, await Gate(db, low).CheckAsync(Ct));
        _clock.Advance(TimeSpan.FromMinutes(6));
        var high = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(Evidence(1)));
        Assert.Equal(MapsAdmissionReason.Suspended, await Gate(db, high).CheckAsync(Ct));
        Assert.Equal(1, high.Calls);
    }

    [Fact]
    public async Task New_month_requires_new_evidence_even_when_previous_month_was_below_threshold()
    {
        await using var db = CreateDb();
        var old = Evidence();
        var observer = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(old));
        Assert.Equal(MapsAdmissionReason.Admitted, await Gate(db, observer).CheckAsync(Ct));
        _clock.Advance(TimeSpan.FromDays(32));
        Assert.Equal(MapsAdmissionReason.EvidenceInvalid, await Gate(db, observer).CheckAsync(Ct));
        var current = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(Evidence()));
        Assert.Equal(MapsAdmissionReason.Admitted, await Gate(db, current).CheckAsync(Ct));
    }

    [Fact]
    public async Task Database_failure_denies_and_logs_without_observing_or_leaking_exception_details()
    {
        await using var db = CreateInterceptedDb(new BrokenStorage());
        var observer = new FakeObserver((_, _, _, _) => throw new InvalidOperationException("Must not observe"));
        var log = new AdmissionLog<MapsEconomicGate>();
        Assert.Equal(MapsAdmissionReason.StorageUnavailable,
            await new MapsEconomicGate(db, Policy, observer, _clock, log).CheckAsync(Ct));
        Assert.Equal(0, observer.Calls);
        Assert.Contains(log.Messages, message => message.Contains("StorageUnavailable", StringComparison.Ordinal));
        Assert.DoesNotContain(log.Messages, message => message.Contains("private-db-detail", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("negative")]
    [InlineData("lease")]
    [InlineData("suspension")]
    [InlineData("unlatched-threshold")]
    public async Task Sql_constraints_reject_inconsistent_global_states(string scenario)
    {
        await using var db = CreateDb();
        var state = new MapsEconomicState
        {
            ResourceId = Policy.ResourceId!, Revision = 1,
            PeriodStartUtc = Evidence().PeriodStartUtc, PeriodEndUtc = Evidence().PeriodEndUtc,
        };
        if (scenario is "negative" or "unlatched-threshold")
        {
            state.ReportedAmountEur = scenario == "negative" ? -1 : 1;
            state.ObservedAtUtc = Evidence().ObservedAtUtc;
            state.CoveredThroughUtc = Evidence().CoveredThroughUtc;
        }
        if (scenario == "lease") state.LeaseId = Guid.NewGuid();
        if (scenario == "suspension") state.SuspendedAtUtc = Evidence().ObservedAtUtc;
        db.MapsEconomicStates.Add(state);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
    }

    private sealed class BrokenStorage : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("private-db-detail");
    }

    private sealed class AdmissionLog<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
