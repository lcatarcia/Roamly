using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Roamly.Infrastructure.Maps;
using Roamly.Infrastructure.Persistence;

namespace Roamly.IntegrationTests.Maps;

public sealed partial class MapsEconomicGateTests
{
    [Fact]
    public async Task Provider_still_observing_after_timeout_cannot_reopen_a_newer_suspension()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingObservation = new TaskCompletionSource<MapsCostEvidence?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = new FakeObserver((_, _, _, _) => { entered.SetResult(); return pendingObservation.Task; });
        await using var oldDb = CreateDb();
        var oldAdmission = Gate(oldDb, slow).CheckAsync(Ct);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
            _clock.Advance(Policy.RefreshLease!.Value.Add(TimeSpan.FromSeconds(1)));
            Assert.NotEqual(MapsAdmissionReason.Admitted, await oldAdmission);
            Assert.False(pendingObservation.Task.IsCompleted);
            await using var newDb = CreateDb();
            var high = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(Evidence(1)));
            Assert.Equal(MapsAdmissionReason.Suspended, await Gate(newDb, high).CheckAsync(Ct));
        }
        finally { pendingObservation.TrySetResult(Evidence(0)); }
        await using var restarted = CreateDb();
        Assert.Equal(MapsAdmissionReason.Suspended,
            await Gate(restarted, new UnavailableMapsCostObserver()).CheckAsync(Ct));
        Assert.True((await restarted.MapsEconomicStates.AsNoTracking().SingleAsync(Ct)).AlertPending);
    }

    [Fact]
    public async Task Concurrent_instances_share_one_refresh_and_observe_outside_the_sql_lock()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<MapsCostEvidence?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new FakeObserver((_, _, _, _) => { entered.SetResult(); return release.Task; });
        await using var firstDb = CreateDb();
        await using var secondDb = CreateDb();
        var first = Gate(firstDb, observer).CheckAsync(Ct);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
            Assert.Equal(MapsAdmissionReason.RefreshInProgress, await Gate(secondDb, observer).CheckAsync(Ct));
            Assert.Equal(1, observer.Calls);
        }
        finally { release.TrySetResult(Evidence()); }
        Assert.Equal(MapsAdmissionReason.Admitted, await first);
    }

    [Fact]
    public async Task Cancelled_refresh_keeps_a_lease_until_expiry_and_another_instance_can_recover()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var request = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var observer = new FakeObserver(async (_, _, _, cancellation) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
            return Evidence();
        });
        await using var firstDb = CreateDb();
        var first = Gate(firstDb, observer).CheckAsync(request.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
        request.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await using var secondDb = CreateDb();
        var recovered = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(Evidence()));
        Assert.Equal(MapsAdmissionReason.RefreshInProgress, await Gate(secondDb, recovered).CheckAsync(Ct));
        _clock.Advance(Policy.RefreshLease!.Value.Add(TimeSpan.FromTicks(1)));
        Assert.Equal(MapsAdmissionReason.Admitted, await Gate(secondDb, recovered).CheckAsync(Ct));
    }

    [Theory]
    [InlineData(1, MapsAdmissionReason.Suspended)]
    [InlineData(0.5, MapsAdmissionReason.Superseded)]
    public async Task Late_low_cost_result_cannot_overwrite_a_newer_revision(
        double newerAmount, MapsAdmissionReason lateReason)
    {
        var barrier = new CompletionBarrier();
        await using var firstDb = CreateInterceptedDb(barrier);
        var low = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(Evidence()));
        var first = Gate(firstDb, low).CheckAsync(Ct);
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
            _clock.Advance(Policy.RefreshLease!.Value.Add(TimeSpan.FromSeconds(1)));
            await using var secondDb = CreateDb();
            var newer = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(Evidence(newerAmount)));
            var expected = newerAmount >= 1 ? MapsAdmissionReason.Suspended : MapsAdmissionReason.Admitted;
            Assert.Equal(expected, await Gate(secondDb, newer).CheckAsync(Ct));
        }
        finally { barrier.Release.TrySetResult(); }
        Assert.Equal(lateReason, await first);
        await using var restarted = CreateDb();
        var state = await restarted.MapsEconomicStates.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(newerAmount >= 1, state.AlertPending);
        Assert.Equal(newerAmount, state.ReportedAmountEur);
    }

    [Fact]
    public async Task Late_result_without_a_replacement_is_denied_after_lease_expiry()
    {
        var barrier = new CompletionBarrier();
        await using var db = CreateInterceptedDb(barrier);
        var observer = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(Evidence()));
        var first = Gate(db, observer).CheckAsync(Ct);
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
            _clock.Advance(Policy.RefreshLease!.Value.Add(TimeSpan.FromSeconds(1)));
        }
        finally { barrier.Release.TrySetResult(); }
        Assert.Equal(MapsAdmissionReason.Superseded, await first);
    }

    [Fact]
    public async Task Restart_and_new_month_do_not_reopen_the_latched_suspension()
    {
        await using (var db = CreateDb())
        {
            var high = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(Evidence(1)));
            Assert.Equal(MapsAdmissionReason.Suspended, await Gate(db, high).CheckAsync(Ct));
        }
        _clock.Advance(TimeSpan.FromDays(32));
        await using var restarted = CreateDb();
        var low = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(Evidence(0)));
        Assert.Equal(MapsAdmissionReason.Suspended, await Gate(restarted, low).CheckAsync(Ct));
        Assert.Equal(0, low.Calls);
    }

    private RoamlyDbContext CreateInterceptedDb(DbCommandInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<RoamlyDbContext>().UseSqlServer(_database.ConnectionString)
            .AddInterceptors(interceptor).Options, ThrowingCurrentUser.Instance);

    private sealed class CompletionBarrier : DbCommandInterceptor
    {
        private int _locks;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("sp_getapplock", StringComparison.Ordinal)
                && Interlocked.Increment(ref _locks) == 2)
            {
                Entered.SetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }
}
