using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.GetCamperDashboard;

/// <summary>Compone la dashboard del camper con tre query owner-scoped e senza N+1.</summary>
[ProtocolOutcomeHandler("Un camper assente o non posseduto restituisce il 404 uniforme.")]
public sealed class GetCamperDashboardHandler(RoamlyDbContext db, TimeProvider clock)
{
    private const int OdometerStaleAfterDays = 90;
    private const int DueSoonDays = 30;
    private const int DueSoonKilometers = 1_000;

    /// <summary>Restituisce la proiezione del camper o null per un lookup non posseduto.</summary>
    public async Task<CamperDashboardResponse?> HandleAsync(Guid camperId, CancellationToken cancellationToken)
    {
        var ownerId = db.CurrentUserId;
        var camper = await ReadCamperAsync(ownerId, camperId, cancellationToken).ConfigureAwait(false);
        if (camper is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var reading = await ReadLatestOdometerAsync(ownerId, camperId, today, cancellationToken)
            .ConfigureAwait(false);
        var maintenance = await ReadMaintenanceAsync(ownerId, camperId, today, reading, cancellationToken)
            .ConfigureAwait(false);
        return new CamperDashboardResponse(camper, reading, maintenance);
    }

    private Task<CamperDashboardCamper?> ReadCamperAsync(
        Guid ownerId,
        Guid camperId,
        CancellationToken cancellationToken)
    {
        return db.Campers
            .AsNoTracking()
            .Where(item => item.OwnerId == ownerId && item.Id == camperId)
            .Select(item => new CamperDashboardCamper(
                item.Id, item.Name, item.Brand, item.Model, item.Year, (int)item.VehicleKind))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<CamperDashboardOdometer?> ReadLatestOdometerAsync(
        Guid ownerId,
        Guid camperId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var reading = await db.OdometerReadings
            .AsNoTracking()
            .Where(item => item.OwnerId == ownerId && item.CamperId == camperId)
            .OrderByDescending(item => item.TakenOnUtc)
            .Select(item => new { item.ReadingKm, item.TakenOnUtc })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return reading is null
            ? null
            : new CamperDashboardOdometer(
                reading.ReadingKm,
                reading.TakenOnUtc,
                reading.TakenOnUtc < today.AddDays(-OdometerStaleAfterDays));
    }

    private async Task<IReadOnlyList<CamperDashboardMaintenance>> ReadMaintenanceAsync(
        Guid ownerId,
        Guid camperId,
        DateOnly today,
        CamperDashboardOdometer? reading,
        CancellationToken cancellationToken)
    {
        var rows = await ReadMaintenanceSnapshotsAsync(ownerId, camperId, cancellationToken)
            .ConfigureAwait(false);
        var currentOdometer = reading is { IsStale: false } ? reading.ReadingKm : (int?)null;
        return rows.Select(row => ProjectMaintenance(row, today, currentOdometer)).ToArray();
    }

    private Task<List<MaintenanceSnapshot>> ReadMaintenanceSnapshotsAsync(
        Guid ownerId,
        Guid camperId,
        CancellationToken cancellationToken)
    {
        return db.MaintenanceItems
            .AsNoTracking()
            .Where(item => item.OwnerId == ownerId && item.CamperId == camperId && item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new MaintenanceSnapshot(
                item.Id,
                item.Name,
                item.IntervalMonths,
                item.IntervalKm,
                item.LastServiceOnUtc,
                item.NextDueOnUtc,
                item.NextDueOdometerKm))
            .ToListAsync(cancellationToken);
    }

    private static CamperDashboardMaintenance ProjectMaintenance(
        MaintenanceSnapshot item,
        DateOnly today,
        int? currentOdometer)
    {
        var hasHistory = item.LastServiceOnUtc.HasValue;
        var timeAxis = ProjectTimeAxis(item, today, hasHistory);
        var odometerAxis = ProjectOdometerAxis(item, currentOdometer, hasHistory);
        return new CamperDashboardMaintenance(
            item.Id, item.Name, hasHistory, item.LastServiceOnUtc, timeAxis, odometerAxis);
    }

    private static CamperDashboardTimeAxis ProjectTimeAxis(
        MaintenanceSnapshot item,
        DateOnly today,
        bool hasHistory)
    {
        if (!hasHistory)
        {
            return new(CamperDashboardAxisStatus.NotScheduled, null);
        }

        if (!item.IntervalMonths.HasValue)
        {
            return new(CamperDashboardAxisStatus.NotConfigured, null);
        }

        var dueOn = item.NextDueOnUtc;
        var status = dueOn.HasValue
            ? GetDateStatus(dueOn.Value, today)
            : CamperDashboardAxisStatus.Unknown;
        return new(status, dueOn);
    }

    private static CamperDashboardOdometerAxis ProjectOdometerAxis(
        MaintenanceSnapshot item,
        int? currentOdometer,
        bool hasHistory)
    {
        if (!hasHistory)
        {
            return new(CamperDashboardAxisStatus.NotScheduled, null, null);
        }

        if (!item.IntervalKm.HasValue)
        {
            return new(CamperDashboardAxisStatus.NotConfigured, null, null);
        }

        if (currentOdometer is null || item.NextDueOdometerKm is null)
        {
            return new(CamperDashboardAxisStatus.Unknown, item.NextDueOdometerKm, null);
        }

        var remaining = item.NextDueOdometerKm.Value - currentOdometer.Value;
        var status = GetOdometerStatus(remaining);
        return new(status, item.NextDueOdometerKm, remaining);
    }

    private static string GetOdometerStatus(int remaining)
    {
        if (remaining <= 0)
        {
            return CamperDashboardAxisStatus.Overdue;
        }

        return remaining <= DueSoonKilometers
            ? CamperDashboardAxisStatus.DueSoon
            : CamperDashboardAxisStatus.Ok;
    }

    private static string GetDateStatus(DateOnly dueOn, DateOnly today)
    {
        if (dueOn <= today)
        {
            return CamperDashboardAxisStatus.Overdue;
        }

        return dueOn <= today.AddDays(DueSoonDays)
            ? CamperDashboardAxisStatus.DueSoon
            : CamperDashboardAxisStatus.Ok;
    }

    private sealed record MaintenanceSnapshot(
        Guid Id,
        string Name,
        short? IntervalMonths,
        int? IntervalKm,
        DateOnly? LastServiceOnUtc,
        DateOnly? NextDueOnUtc,
        int? NextDueOdometerKm);
}
