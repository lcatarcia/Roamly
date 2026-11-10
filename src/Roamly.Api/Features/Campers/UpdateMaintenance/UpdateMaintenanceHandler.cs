using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Features.Campers.GetMaintenance;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Domain.ValueObjects;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.UpdateMaintenance;

/// <summary>Aggiorna una manutenzione posseduta e l'intervento associato con concorrenza rowversion.</summary>
[ProtocolOutcomeHandler("404, 409, 412 e 428 sono esiti HTTP distinti previsti da ADR-0005 e dal contratto odometrico.")]
public sealed class UpdateMaintenanceHandler(
    RoamlyDbContext db,
    TimeProvider clock,
    IIdGenerator ids)
{
    /// <summary>Modifica la manutenzione solo se l'ETag inviato e' ancora corrente.</summary>
    public async Task<UpdateMaintenanceResult> HandleAsync(
        Guid camperId,
        Guid maintenanceItemId,
        UpdateMaintenanceRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await FindOwnedItemAsync(camperId, maintenanceItemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return new UpdateMaintenanceResult(UpdateMaintenanceOutcome.NotFound);
        }

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return new UpdateMaintenanceResult(UpdateMaintenanceOutcome.MissingPrecondition);
        }

        if (!RowVersionEtag.TryParse(ifMatch, out var token)
            || item.RowVersion is not { Length: > 0 } current
            || !current.AsSpan().SequenceEqual(token))
        {
            return new UpdateMaintenanceResult(UpdateMaintenanceOutcome.PreconditionFailed);
        }

        return await ApplyUpdateAsync(item, camperId, request, token, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UpdateMaintenanceResult> ApplyUpdateAsync(
        MaintenanceItem item,
        Guid camperId,
        UpdateMaintenanceRequest request,
        byte[] token,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var logs = await ReadLogsAsync(item.Id, cancellationToken).ConfigureAwait(false);
        var latestLog = logs.FirstOrDefault();

        if (request.PerformedOnUtc is { } performedOnUtc)
        {
            if (!await SyncOdometerAsync(camperId, performedOnUtc, request.OdometerKm, latestLog, now, cancellationToken).ConfigureAwait(false))
            {
                return new UpdateMaintenanceResult(UpdateMaintenanceOutcome.OdometerConflict);
            }

            latestLog = UpsertLog(item.Id, latestLog, request, performedOnUtc, now);
            ApplyProjectedService(item, request, performedOnUtc, now);
        }
        else
        {
            if (logs.Count > 0)
            {
                db.MaintenanceLogs.RemoveRange(logs);
            }

            latestLog = null;
            ClearProjectedService(item, request, now);
        }

        db.Entry(item).Property(maintenance => maintenance.RowVersion).OriginalValue = token;

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UpdateMaintenanceResult(UpdateMaintenanceOutcome.PreconditionFailed);
        }

        return new UpdateMaintenanceResult(
            UpdateMaintenanceOutcome.Updated,
            MaintenanceDetailResponse.From(item, latestLog),
            item.RowVersion);
    }

    private async Task<bool> SyncOdometerAsync(
        Guid camperId,
        DateOnly performedOnUtc,
        int? requestedKm,
        MaintenanceLog? latestLog,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (requestedKm is not { } odometerKm)
        {
            return true;
        }

        var existing = await db.OdometerReadings
            .Where(reading =>
                reading.OwnerId == db.CurrentUserId
                && reading.CamperId == camperId
                && reading.TakenOnUtc == performedOnUtc)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            db.OdometerReadings.Add(new OdometerReading
            {
                Id = ids.NewId(),
                CamperId = camperId,
                ReadingKm = odometerKm,
                TakenOnUtc = performedOnUtc,
                Source = OdometerSource.Service,
                CreatedAtUtc = now,
            });
            return true;
        }

        if (existing.ReadingKm == odometerKm)
        {
            return true;
        }

        if (existing.Source == OdometerSource.Service
            && latestLog is not null
            && latestLog.PerformedOnUtc == performedOnUtc
            && latestLog.OdometerKm == existing.ReadingKm)
        {
            existing.ReadingKm = odometerKm;
            existing.UpdatedAtUtc = now;
            return true;
        }

        return false;
    }

    private MaintenanceLog UpsertLog(
        Guid maintenanceItemId,
        MaintenanceLog? latestLog,
        UpdateMaintenanceRequest request,
        DateOnly performedOnUtc,
        DateTime now)
    {
        var cost = request.Cost is { } value
            ? new Money(value.Amount!.Value, value.Currency!)
            : (Money?)null;
        var workshop = Normalize(request.Workshop);
        var notes = Normalize(request.Notes);
        var warranty = request.IsUnderWarranty ?? false;

        if (latestLog is null)
        {
            var created = new MaintenanceLog
            {
                Id = ids.NewId(),
                MaintenanceItemId = maintenanceItemId,
                PerformedOnUtc = performedOnUtc,
                OdometerKm = request.OdometerKm,
                Cost = cost,
                Workshop = workshop,
                Notes = notes,
                IsUnderWarranty = warranty,
                CreatedAtUtc = now,
            };
            db.MaintenanceLogs.Add(created);
            return created;
        }

        latestLog.PerformedOnUtc = performedOnUtc;
        latestLog.OdometerKm = request.OdometerKm;
        latestLog.Cost = cost;
        latestLog.Workshop = workshop;
        latestLog.Notes = notes;
        latestLog.IsUnderWarranty = warranty;
        latestLog.UpdatedAtUtc = now;
        return latestLog;
    }

    private static void ApplyProjectedService(
        MaintenanceItem item,
        UpdateMaintenanceRequest request,
        DateOnly performedOnUtc,
        DateTime now)
    {
        item.Name = request.Name!.Trim();
        item.Category = request.Category!.Value;
        item.IntervalKm = request.IntervalKm;
        item.IntervalMonths = request.IntervalMonths;
        item.LastServiceOnUtc = performedOnUtc;
        item.LastServiceOdometerKm = request.OdometerKm;
        item.LastServiceIsUnderWarranty = request.IsUnderWarranty ?? false;
        item.NextDueOnUtc = request.IntervalMonths.HasValue
            ? performedOnUtc.AddMonths(request.IntervalMonths.Value)
            : null;
        item.NextDueOdometerKm = request.IntervalKm.HasValue && request.OdometerKm.HasValue
            ? request.OdometerKm.Value + request.IntervalKm.Value
            : null;
        item.UpdatedAtUtc = now;
    }

    private static void ClearProjectedService(
        MaintenanceItem item,
        UpdateMaintenanceRequest request,
        DateTime now)
    {
        item.Name = request.Name!.Trim();
        item.Category = request.Category!.Value;
        item.IntervalKm = request.IntervalKm;
        item.IntervalMonths = request.IntervalMonths;
        item.LastServiceOnUtc = null;
        item.LastServiceOdometerKm = null;
        item.LastServiceIsUnderWarranty = null;
        item.NextDueOnUtc = null;
        item.NextDueOdometerKm = null;
        item.UpdatedAtUtc = now;
    }

    private Task<MaintenanceItem?> FindOwnedItemAsync(
        Guid camperId,
        Guid maintenanceItemId,
        CancellationToken cancellationToken)
    {
        return db.MaintenanceItems
            .Where(item =>
                item.OwnerId == db.CurrentUserId
                && item.CamperId == camperId
                && item.Id == maintenanceItemId
                && item.IsActive)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private Task<List<MaintenanceLog>> ReadLogsAsync(
        Guid maintenanceItemId,
        CancellationToken cancellationToken)
    {
        return db.MaintenanceLogs
            .Where(log => log.OwnerId == db.CurrentUserId && log.MaintenanceItemId == maintenanceItemId)
            .OrderByDescending(log => log.PerformedOnUtc)
            .ThenByDescending(log => log.CreatedAtUtc)
            .ThenByDescending(log => log.Id)
            .ToListAsync(cancellationToken);
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
