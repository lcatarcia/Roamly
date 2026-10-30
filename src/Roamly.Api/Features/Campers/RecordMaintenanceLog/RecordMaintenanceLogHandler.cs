using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Domain.ValueObjects;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.RecordMaintenanceLog;

/// <summary>Registra un fatto immutabile e aggiorna proiezione e odometro nella stessa transazione.</summary>
[ProtocolOutcomeHandler("R50 e la collisione odometrica hanno contratti HTTP 409 distinti.")]
public sealed class RecordMaintenanceLogHandler(
    RoamlyDbContext db,
    TimeProvider clock,
    IIdGenerator ids)
{
    /// <summary>Registra il log o restituisce replay, conflitto o lookup non posseduto.</summary>
    public Task<RecordMaintenanceLogResult> HandleAsync(
        Guid camperId,
        Guid maintenanceItemId,
        RecordMaintenanceLogRequest request,
        CancellationToken cancellationToken)
    {
        return RecordAsync(camperId, maintenanceItemId, request, true, cancellationToken);
    }

    private async Task<RecordMaintenanceLogResult> RecordAsync(
        Guid camperId,
        Guid maintenanceItemId,
        RecordMaintenanceLogRequest request,
        bool allowSingleRetry,
        CancellationToken cancellationToken)
    {
        var item = await ReadItemAsync(camperId, maintenanceItemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return new RecordMaintenanceLogResult(RecordMaintenanceLogOutcome.NotFound, null);
        }

        var existingLog = await FindLogAsync(request.Id!.Value, cancellationToken).ConfigureAwait(false);
        if (existingLog is not null)
        {
            return Resolve(existingLog, maintenanceItemId, request);
        }

        var createdAt = clock.GetUtcNow().UtcDateTime;
        var odometer = await FindOdometerAsync(
            camperId,
            request.PerformedOnUtc!.Value,
            cancellationToken).ConfigureAwait(false);
        if (IsOdometerConflict(odometer, request.OdometerKm))
        {
            return new RecordMaintenanceLogResult(RecordMaintenanceLogOutcome.OdometerConflict, null);
        }

        return await SaveAsync(
            item,
            camperId,
            maintenanceItemId,
            request,
            odometer,
            createdAt,
            allowSingleRetry,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<RecordMaintenanceLogResult> SaveAsync(
        MaintenanceItem item,
        Guid camperId,
        Guid maintenanceItemId,
        RecordMaintenanceLogRequest request,
        OdometerReading? existingOdometer,
        DateTime createdAt,
        bool allowSingleRetry,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await db.Database
                .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await AcquireMaintenanceItemLockAsync(
                item.OwnerId,
                maintenanceItemId,
                cancellationToken).ConfigureAwait(false);
            var log = BuildLog(maintenanceItemId, request, createdAt);
            db.MaintenanceLogs.Add(log);
            AddServiceOdometerIfNeeded(camperId, request, createdAt, existingOdometer);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (!await RecalculateProjectionAsync(item, cancellationToken).ConfigureAwait(false))
            {
                return new RecordMaintenanceLogResult(RecordMaintenanceLogOutcome.ProjectionOutOfRange, null);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new RecordMaintenanceLogResult(
                RecordMaintenanceLogOutcome.Created,
                RecordMaintenanceLogResponse.From(log));
        }
        catch (DbUpdateConcurrencyException) when (allowSingleRetry)
        {
            db.ChangeTracker.Clear();
            return await RecordAsync(
                camperId,
                maintenanceItemId,
                request,
                false,
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new RecordMaintenanceLogResult(RecordMaintenanceLogOutcome.ConcurrencyConflict, null);
        }
        catch (DbUpdateException exception) when (allowSingleRetry && IsUniqueConstraintViolation(exception))
        {
            db.ChangeTracker.Clear();
            return await RecoverUniqueRaceAsync(
                camperId,
                maintenanceItemId,
                request,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AcquireMaintenanceItemLockAsync(
        Guid ownerId,
        Guid maintenanceItemId,
        CancellationToken cancellationToken)
    {
        var resource = $"RoamlyMaintenance:{ownerId:N}:{maintenanceItemId:N}";
        List<int> lockResults;
        try
        {
            lockResults = await db.Database.SqlQuery<int>($"""
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock
                    @Resource = {resource},
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = -1;
                SELECT @lockResult AS [Value];
                """)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SqlException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "L'acquisizione del lock della manutenzione e' stata annullata.",
                exception,
                cancellationToken);
        }

        if (lockResults.Count != 1)
        {
            throw new InvalidOperationException("Il lock della manutenzione non e' stato acquisito.");
        }

        var lockResult = lockResults[0];
        if (lockResult == -2)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (lockResult < 0)
        {
            throw new InvalidOperationException("Il lock della manutenzione non e' stato acquisito.");
        }
    }

    private async Task<bool> RecalculateProjectionAsync(
        MaintenanceItem item,
        CancellationToken cancellationToken)
    {
        var latest = await db.MaintenanceLogs.AsNoTracking()
            .Where(log => log.MaintenanceItemId == item.Id)
            .OrderByDescending(log => log.PerformedOnUtc)
            .ThenByDescending(log => log.CreatedAtUtc)
            .ThenByDescending(log => log.Id)
            .Select(log => new LatestService(log.PerformedOnUtc, log.OdometerKm))
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        if (item.IntervalMonths is { } intervalMonths
            && latest.PerformedOnUtc > DateOnly.MaxValue.AddMonths(-intervalMonths))
        {
            return false;
        }

        if (item.IntervalKm is { } intervalKm
            && latest.OdometerKm is { } odometerKm
            && (long)odometerKm + intervalKm > int.MaxValue)
        {
            return false;
        }

        item.LastServiceOnUtc = latest.PerformedOnUtc;
        item.LastServiceOdometerKm = latest.OdometerKm;
        item.NextDueOnUtc = item.IntervalMonths.HasValue
            ? latest.PerformedOnUtc.AddMonths(item.IntervalMonths.Value)
            : null;
        item.NextDueOdometerKm = item.IntervalKm.HasValue && latest.OdometerKm.HasValue
            ? latest.OdometerKm.Value + item.IntervalKm.Value
            : null;
        return true;
    }

    private async Task<RecordMaintenanceLogResult> RecoverUniqueRaceAsync(
        Guid camperId,
        Guid maintenanceItemId,
        RecordMaintenanceLogRequest request,
        CancellationToken cancellationToken)
    {
        var racedLog = await FindLogAsync(request.Id!.Value, cancellationToken).ConfigureAwait(false);
        if (racedLog is not null)
        {
            return Resolve(racedLog, maintenanceItemId, request);
        }

        var racedReading = await FindOdometerAsync(
            camperId,
            request.PerformedOnUtc!.Value,
            cancellationToken).ConfigureAwait(false);
        if (racedReading is null)
        {
            throw new InvalidOperationException("La violazione univoca non corrisponde al log o all'odometro.");
        }

        if (IsOdometerConflict(racedReading, request.OdometerKm))
        {
            return new RecordMaintenanceLogResult(RecordMaintenanceLogOutcome.OdometerConflict, null);
        }

        return await RecordAsync(
            camperId,
            maintenanceItemId,
            request,
            false,
            cancellationToken).ConfigureAwait(false);
    }

    private Task<MaintenanceItem?> ReadItemAsync(
        Guid camperId,
        Guid maintenanceItemId,
        CancellationToken cancellationToken)
    {
        return db.MaintenanceItems.SingleOrDefaultAsync(
            item => item.CamperId == camperId && item.Id == maintenanceItemId,
            cancellationToken);
    }

    private Task<MaintenanceLog?> FindLogAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.MaintenanceLogs.AsNoTracking()
            .SingleOrDefaultAsync(log => log.Id == id, cancellationToken);
    }

    private Task<OdometerReading?> FindOdometerAsync(
        Guid camperId,
        DateOnly takenOn,
        CancellationToken cancellationToken)
    {
        return db.OdometerReadings.AsNoTracking()
            .SingleOrDefaultAsync(
                reading => reading.CamperId == camperId && reading.TakenOnUtc == takenOn,
                cancellationToken);
    }

    private void AddServiceOdometerIfNeeded(
        Guid camperId,
        RecordMaintenanceLogRequest request,
        DateTime createdAt,
        OdometerReading? existingOdometer)
    {
        if (request.OdometerKm is not { } odometerKm || existingOdometer is not null)
        {
            return;
        }

        db.OdometerReadings.Add(new OdometerReading
        {
            Id = ids.NewId(),
            CamperId = camperId,
            ReadingKm = odometerKm,
            TakenOnUtc = request.PerformedOnUtc!.Value,
            Source = OdometerSource.Service,
            CreatedAtUtc = createdAt,
        });
    }

    private static MaintenanceLog BuildLog(
        Guid maintenanceItemId,
        RecordMaintenanceLogRequest request,
        DateTime createdAt)
    {
        var cost = request.Cost is { } value
            ? new Money(value.Amount!.Value, value.Currency!)
            : (Money?)null;
        return new MaintenanceLog
        {
            Id = request.Id!.Value,
            MaintenanceItemId = maintenanceItemId,
            PerformedOnUtc = request.PerformedOnUtc!.Value,
            OdometerKm = request.OdometerKm,
            Cost = cost,
            Workshop = request.Workshop,
            Notes = request.Notes,
            CreatedAtUtc = createdAt,
        };
    }

    private static RecordMaintenanceLogResult Resolve(
        MaintenanceLog existing,
        Guid maintenanceItemId,
        RecordMaintenanceLogRequest request)
    {
        var requestCost = request.Cost is { } cost
            ? new Money(cost.Amount!.Value, cost.Currency!)
            : (Money?)null;
        var sameContent = existing.MaintenanceItemId == maintenanceItemId
            && existing.PerformedOnUtc == request.PerformedOnUtc
            && existing.OdometerKm == request.OdometerKm
            && existing.Cost == requestCost
            && string.Equals(existing.Workshop, request.Workshop, StringComparison.Ordinal)
            && string.Equals(existing.Notes, request.Notes, StringComparison.Ordinal);
        return sameContent
            ? new RecordMaintenanceLogResult(
                RecordMaintenanceLogOutcome.Replayed,
                RecordMaintenanceLogResponse.From(existing))
            : new RecordMaintenanceLogResult(RecordMaintenanceLogOutcome.Conflict, null);
    }

    private static bool IsOdometerConflict(OdometerReading? reading, int? requestedKm)
    {
        return reading is not null
            && requestedKm.HasValue
            && reading.ReadingKm != requestedKm.Value;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
    }

    private sealed record LatestService(DateOnly PerformedOnUtc, int? OdometerKm);
}
