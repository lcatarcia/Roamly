using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Roamly.Infrastructure.Maps;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Maps;

[Collection(DatabaseCollection.Name)]
public sealed partial class MapsEconomicGateTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private DatabaseLease _database = null!;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static MapsEconomicPolicy Policy => new()
    {
        Enabled = true, ResourceId = "synthetic-maps", UtcCalendarMonthVerified = true,
        EvidenceMaxAge = TimeSpan.FromMinutes(5), CoverageMaxLag = TimeSpan.FromMinutes(5),
        RefreshLease = TimeSpan.FromMinutes(1), ObserverTimeout = TimeSpan.FromSeconds(10),
        SqlLockTimeout = TimeSpan.FromSeconds(2),
    };

    public async ValueTask InitializeAsync()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        _database = migrated.Lease;
        await _database.ResetAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData(0.99, MapsAdmissionReason.Admitted)]
    [InlineData(1, MapsAdmissionReason.Suspended)]
    [InlineData(1.01, MapsAdmissionReason.Suspended)]
    public async Task Verified_cost_below_one_is_admitted_but_exactly_one_latches_suspension(
        double amount, MapsAdmissionReason expected)
    {
        await using var db = CreateDb();
        var observer = new FakeObserver((_, _, _, _) => Task.FromResult<MapsCostEvidence?>(Evidence(amount)));
        Assert.Equal(expected, await Gate(db, observer).CheckAsync(Ct));
        await using var restarted = CreateDb();
        var unavailable = new UnavailableMapsCostObserver();
        Assert.Equal(expected, await Gate(restarted, unavailable).CheckAsync(Ct));
        var state = await restarted.MapsEconomicStates.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(expected == MapsAdmissionReason.Suspended, state.AlertPending);
    }

    private RoamlyDbContext CreateDb() => new(new DbContextOptionsBuilder<RoamlyDbContext>()
        .UseSqlServer(_database.ConnectionString).Options, ThrowingCurrentUser.Instance);

    private MapsEconomicGate Gate(RoamlyDbContext db, IMapsCostObserver observer) =>
        new(db, Policy, observer, _clock, NullLogger<MapsEconomicGate>.Instance);

    private MapsCostEvidence Evidence(double amount = 0.99) => new(Policy.ResourceId!, "EUR",
        MapsEconomicPolicy.MonthStart(_clock.GetUtcNow().UtcDateTime),
        MapsEconomicPolicy.MonthStart(_clock.GetUtcNow().UtcDateTime).AddMonths(1),
        _clock.GetUtcNow().UtcDateTime, _clock.GetUtcNow().UtcDateTime, amount, true, true, true);

    private sealed class FakeObserver(
        Func<string, DateTime, DateTime, CancellationToken, Task<MapsCostEvidence?>> observe) : IMapsCostObserver
    {
        public int Calls { get; private set; }
        public Task<MapsCostEvidence?> ObserveAsync(string resourceId, DateTime start, DateTime end,
            CancellationToken cancellationToken)
        {
            Calls++;
            return observe(resourceId, start, end, cancellationToken);
        }
    }
}
