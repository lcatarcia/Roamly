using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Infrastructure.Maps;

/// <summary>On-demand global admission. All observation happens outside the SQL transaction.</summary>
public sealed partial class MapsEconomicGate(
    RoamlyDbContext db, MapsEconomicPolicy policy, IMapsCostObserver observer,
    TimeProvider clock, ILogger<MapsEconomicGate> logger)
{
    public async Task<MapsAdmissionReason> CheckAsync(CancellationToken cancellationToken)
    {
        if (!policy.IsConfigured) return Deny(MapsAdmissionReason.NotConfigured);
        try
        {
            var reservation = await ReserveAsync(cancellationToken).ConfigureAwait(false);
            if (reservation.LeaseId is null) return Report(reservation.Reason);
            var observation = await ObserveAsync(cancellationToken).ConfigureAwait(false);
            return Report(await CompleteAsync(reservation, observation, cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return Deny(MapsAdmissionReason.StorageUnavailable);
        }
    }

    private async Task<Reservation> ReserveAsync(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await LockAsync(ct).ConfigureAwait(false);
        var state = await LoadAsync(ct).ConfigureAwait(false);
        var now = clock.GetUtcNow().UtcDateTime;
        if (state.SuspensionReason is not null) return new(MapsAdmissionReason.Suspended);
        if (policy.Validate(ReadEvidence(state), now) == MapsAdmissionReason.Admitted)
            return new(MapsAdmissionReason.Admitted);
        if (state.LeaseExpiresAtUtc > now) return new(MapsAdmissionReason.RefreshInProgress);
        PrepareLease(state, now);
        await SaveAsync(state, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return new(MapsAdmissionReason.RefreshInProgress, state.LeaseId, state.Revision);
    }

    private void PrepareLease(MapsEconomicState state, DateTime now)
    {
        if (state.PeriodStartUtc != MapsEconomicPolicy.MonthStart(now))
        {
            state.PeriodStartUtc = MapsEconomicPolicy.MonthStart(now);
            state.PeriodEndUtc = state.PeriodStartUtc.AddMonths(1);
            state.ReportedAmountEur = null;
            state.ObservedAtUtc = null;
            state.CoveredThroughUtc = null;
        }
        state.LeaseId = Guid.NewGuid();
        state.LeaseExpiresAtUtc = now.Add(policy.RefreshLease!.Value);
        state.Revision = checked(state.Revision + 1);
    }

    private async Task<Observation> ObserveAsync(CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(policy.ObserverTimeout!.Value, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            var start = MapsEconomicPolicy.MonthStart(clock.GetUtcNow().UtcDateTime);
            var evidence = await observer.ObserveAsync(policy.ResourceId!, start, start.AddMonths(1), linked.Token)
                .WaitAsync(policy.ObserverTimeout.Value, clock, ct).ConfigureAwait(false);
            return new(evidence, policy.Validate(evidence, clock.GetUtcNow().UtcDateTime));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return new(null, MapsAdmissionReason.ObserverUnavailable); }
    }

    private async Task<MapsAdmissionReason> CompleteAsync(Reservation lease, Observation observation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await LockAsync(ct).ConfigureAwait(false);
        var state = await LoadAsync(ct).ConfigureAwait(false);
        var now = clock.GetUtcNow().UtcDateTime;
        if (state.SuspensionReason is not null) return MapsAdmissionReason.Suspended;
        if (state.LeaseId != lease.LeaseId || state.Revision != lease.Revision
            || state.LeaseExpiresAtUtc <= now || state.PeriodStartUtc != MapsEconomicPolicy.MonthStart(now))
            return MapsAdmissionReason.Superseded;
        var reason = ApplyObservation(state, observation, now);
        await SaveAsync(state, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return reason;
    }

    private MapsAdmissionReason ApplyObservation(MapsEconomicState state, Observation observation, DateTime now)
    {
        state.LeaseId = null;
        state.LeaseExpiresAtUtc = null;
        state.Revision = checked(state.Revision + 1);
        if (observation.Reason != MapsAdmissionReason.Admitted) return observation.Reason;
        var reason = policy.Validate(observation.Evidence, now);
        if (reason != MapsAdmissionReason.Admitted) return reason;
        var evidence = observation.Evidence!;
        state.ReportedAmountEur = evidence.Amount;
        state.ObservedAtUtc = evidence.ObservedAtUtc;
        state.CoveredThroughUtc = evidence.CoveredThroughUtc;
        if (evidence.Amount < MapsEconomicPolicy.SuspensionThresholdEur) return MapsAdmissionReason.Admitted;
        state.SuspensionReason = "ThresholdReached";
        state.SuspendedAtUtc = now;
        state.AlertPending = true;
        return MapsAdmissionReason.Suspended;
    }

    private async Task<MapsEconomicState> LoadAsync(CancellationToken ct) =>
        await db.MapsEconomicStates.AsNoTracking().SingleOrDefaultAsync(
            state => state.ResourceId == policy.ResourceId, ct).ConfigureAwait(false)
        ?? new MapsEconomicState
        {
            ResourceId = policy.ResourceId!,
            PeriodStartUtc = MapsEconomicPolicy.MonthStart(clock.GetUtcNow().UtcDateTime),
            PeriodEndUtc = MapsEconomicPolicy.MonthStart(clock.GetUtcNow().UtcDateTime).AddMonths(1),
        };

    private async Task SaveAsync(MapsEconomicState state, CancellationToken ct)
    {
        // Same parameterized SQL mechanism as LoginAttemptRateLimiter, on a separate global table.
        // No owned entities or change-tracker bypass APIs; the app lock encloses both paths.
        var written = state.Revision == 1
            ? await InsertLeaseAsync(state, ct).ConfigureAwait(false)
            : await UpdateStateAsync(state, ct).ConfigureAwait(false);
        if (written != 1) throw new DbUpdateConcurrencyException("Maps economic revision changed.");
    }

    private Task<int> InsertLeaseAsync(MapsEconomicState state, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [MapsEconomicStates]
                ([ResourceId], [PeriodStartUtc], [PeriodEndUtc], [Revision], [AlertPending], [LeaseId], [LeaseExpiresAtUtc])
            VALUES ({state.ResourceId}, {state.PeriodStartUtc}, {state.PeriodEndUtc}, {state.Revision}, 0,
                {state.LeaseId}, {state.LeaseExpiresAtUtc});
            """, ct);

    private Task<int> UpdateStateAsync(MapsEconomicState state, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [MapsEconomicStates] SET [PeriodStartUtc] = {state.PeriodStartUtc},
                [PeriodEndUtc] = {state.PeriodEndUtc}, [Revision] = {state.Revision},
                [AlertPending] = {state.AlertPending}, [LeaseId] = {state.LeaseId},
                [LeaseExpiresAtUtc] = {state.LeaseExpiresAtUtc}, [SuspensionReason] = {state.SuspensionReason},
                [SuspendedAtUtc] = {state.SuspendedAtUtc}, [ReportedAmountEur] = {state.ReportedAmountEur},
                [ObservedAtUtc] = {state.ObservedAtUtc}, [CoveredThroughUtc] = {state.CoveredThroughUtc}
            WHERE [ResourceId] = {state.ResourceId} AND [Revision] = {state.Revision - 1};
            """, ct);

    private static MapsCostEvidence? ReadEvidence(MapsEconomicState state) =>
        state.ReportedAmountEur is { } amount && state.ObservedAtUtc is { } observed
        && state.CoveredThroughUtc is { } covered
            ? new(state.ResourceId, "EUR", state.PeriodStartUtc, state.PeriodEndUtc,
                observed, covered, amount, true, true, true)
            : null;

    private Task<int> LockAsync(CancellationToken ct)
    {
        var resource = "Roamly:MapsEconomic:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(policy.ResourceId!)));
        var timeoutMs = (int)policy.SqlLockTimeout!.Value.TotalMilliseconds;
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = {resource},
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {timeoutMs};
            IF @result < 0 THROW 51000, 'Maps economic lock unavailable.', 1;
            """, ct);
    }

    private MapsAdmissionReason Report(MapsAdmissionReason reason) =>
        reason == MapsAdmissionReason.Admitted ? reason : Deny(reason);

    private MapsAdmissionReason Deny(MapsAdmissionReason reason)
    {
        LogDenied(logger, reason);
        return reason;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Maps local economic admission denied: {Reason}.")]
    private static partial void LogDenied(ILogger logger, MapsAdmissionReason reason);

    private sealed record Reservation(MapsAdmissionReason Reason, Guid? LeaseId = null, long Revision = 0);
    private sealed record Observation(MapsCostEvidence? Evidence, MapsAdmissionReason Reason);
}
