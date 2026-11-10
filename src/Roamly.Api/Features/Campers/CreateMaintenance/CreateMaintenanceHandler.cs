using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Domain.ValueObjects;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.CreateMaintenance;

/// <summary>Crea regole ricorrenti o interventi non periodici owner-scoped.</summary>
[ProtocolOutcomeHandler("R50: replay, conflitto d'identificativo e conflitto odometrico hanno esiti HTTP distinti.")]
public sealed class CreateMaintenanceHandler(
    RoamlyDbContext db,
    TimeProvider clock,
    IIdGenerator ids)
{
    /// <summary>Crea la regola oppure restituisce il replay o il conflitto sul suo identificativo.</summary>
    public async Task<CreateMaintenanceResult> HandleAsync(
        Guid camperId,
        CreateMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var camperExists = await db.Campers
            .AnyAsync(camper => camper.Id == camperId, cancellationToken)
            .ConfigureAwait(false);
        if (!camperExists)
        {
            return new CreateMaintenanceResult(CreateMaintenanceOutcome.NotFound, null);
        }

        var name = request.Name!.Trim();
        var category = request.Category!.Value;
        var existing = await FindAsync(request.Id!.Value, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            var existingLog = await FindLogAsync(existing.Id, cancellationToken).ConfigureAwait(false);
            return Resolve(existing, existingLog, camperId, name, category, request);
        }

        return await InsertAsync(camperId, request, name, category, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CreateMaintenanceResult> InsertAsync(
        Guid camperId,
        CreateMaintenanceRequest request,
        string name,
        MaintenanceCategory category,
        CancellationToken cancellationToken)
    {
        var createdAtUtc = clock.GetUtcNow().UtcDateTime;
        var performedOnUtc = request.PerformedOnUtc;
        if (performedOnUtc.HasValue)
        {
            var odometer = await FindOdometerAsync(camperId, performedOnUtc.Value, cancellationToken)
                .ConfigureAwait(false);
            if (odometer is not null && request.OdometerKm.HasValue && odometer.ReadingKm != request.OdometerKm.Value)
            {
                return new CreateMaintenanceResult(CreateMaintenanceOutcome.OdometerConflict, null);
            }

            if (odometer is null && request.OdometerKm is { } odometerKm)
            {
                db.OdometerReadings.Add(new OdometerReading
                {
                    Id = ids.NewId(),
                    CamperId = camperId,
                    ReadingKm = odometerKm,
                    TakenOnUtc = performedOnUtc.Value,
                    Source = OdometerSource.Service,
                    CreatedAtUtc = createdAtUtc,
                });
            }
        }

        var item = BuildItem(camperId, request, name, category, createdAtUtc);
        db.MaintenanceItems.Add(item);
        if (performedOnUtc.HasValue)
        {
            db.MaintenanceLogs.Add(BuildInitialLog(item.Id, request, performedOnUtc.Value, createdAtUtc));
        }

        return await SaveOrRecoverRaceAsync(item, camperId, name, category, request, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<CreateMaintenanceResult> SaveOrRecoverRaceAsync(
        MaintenanceItem item,
        Guid camperId,
        string name,
        MaintenanceCategory category,
        CreateMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new CreateMaintenanceResult(CreateMaintenanceOutcome.Created, CreateMaintenanceResponse.From(item));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            db.ChangeTracker.Clear();
            var existing = await FindAsync(item.Id, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                var existingLog = await FindLogAsync(existing.Id, cancellationToken).ConfigureAwait(false);
                return Resolve(existing, existingLog, camperId, name, category, request);
            }

            if (request.PerformedOnUtc.HasValue)
            {
                var racedOdometer = await FindOdometerAsync(
                    camperId,
                    request.PerformedOnUtc.Value,
                    cancellationToken).ConfigureAwait(false);
                if (racedOdometer is not null
                    && request.OdometerKm.HasValue
                    && racedOdometer.ReadingKm != request.OdometerKm.Value)
                {
                    return new CreateMaintenanceResult(CreateMaintenanceOutcome.OdometerConflict, null);
                }
            }

            throw;
        }
    }

    private static MaintenanceItem BuildItem(
        Guid camperId,
        CreateMaintenanceRequest request,
        string name,
        MaintenanceCategory category,
        DateTime createdAtUtc)
    {
        var performedOnUtc = request.PerformedOnUtc;
        var isUnderWarranty = performedOnUtc.HasValue ? (request.IsUnderWarranty ?? false) : (bool?)null;
        var nextDueOnUtc = performedOnUtc.HasValue && request.IntervalMonths.HasValue
            ? performedOnUtc.Value.AddMonths(request.IntervalMonths.Value)
            : (DateOnly?)null;
        var nextDueOdometerKm = performedOnUtc.HasValue && request.IntervalKm.HasValue && request.OdometerKm.HasValue
            ? request.OdometerKm.Value + request.IntervalKm.Value
            : (int?)null;

        return new MaintenanceItem
        {
            Id = request.Id!.Value,
            CamperId = camperId,
            Name = name,
            Category = category,
            IntervalKm = request.IntervalKm,
            IntervalMonths = request.IntervalMonths,
            LastServiceOnUtc = performedOnUtc,
            LastServiceOdometerKm = performedOnUtc.HasValue ? request.OdometerKm : null,
            LastServiceIsUnderWarranty = isUnderWarranty,
            NextDueOnUtc = nextDueOnUtc,
            NextDueOdometerKm = nextDueOdometerKm,
            Origin = MaintenanceOrigin.User,
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
        };
    }

    private static MaintenanceLog BuildInitialLog(
        Guid maintenanceItemId,
        CreateMaintenanceRequest request,
        DateOnly performedOnUtc,
        DateTime createdAtUtc)
    {
        var cost = request.Cost is { } value
            ? new Money(value.Amount!.Value, value.Currency!)
            : (Money?)null;
        return new MaintenanceLog
        {
            Id = maintenanceItemId,
            MaintenanceItemId = maintenanceItemId,
            PerformedOnUtc = performedOnUtc,
            OdometerKm = request.OdometerKm,
            Cost = cost,
            Workshop = Normalize(request.Workshop),
            Notes = Normalize(request.Notes),
            IsUnderWarranty = request.IsUnderWarranty ?? false,
            CreatedAtUtc = createdAtUtc,
        };
    }

    private Task<MaintenanceItem?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.MaintenanceItems.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
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

    private static CreateMaintenanceResult Resolve(
        MaintenanceItem existing,
        MaintenanceLog? existingLog,
        Guid camperId,
        string name,
        MaintenanceCategory category,
        CreateMaintenanceRequest request)
    {
        var expectedWarranty = request.PerformedOnUtc.HasValue
            ? (request.IsUnderWarranty ?? false)
            : (bool?)null;
        var expectedCost = request.Cost is { } cost
            ? new Money(cost.Amount!.Value, cost.Currency!)
            : (Money?)null;
        var sameItem = existing.CamperId == camperId
            && string.Equals(existing.Name.Trim(), name, StringComparison.Ordinal)
            && existing.Category == category
            && existing.IntervalKm == request.IntervalKm
            && existing.IntervalMonths == request.IntervalMonths
            && existing.LastServiceOnUtc == request.PerformedOnUtc
            && existing.LastServiceOdometerKm == (request.PerformedOnUtc.HasValue ? request.OdometerKm : null)
            && existing.LastServiceIsUnderWarranty == expectedWarranty;
        var sameLog = !request.PerformedOnUtc.HasValue
            ? existingLog is null
            : existingLog is not null
                && existingLog.PerformedOnUtc == request.PerformedOnUtc.Value
                && existingLog.OdometerKm == request.OdometerKm
                && existingLog.Cost == expectedCost
                && string.Equals(existingLog.Workshop, Normalize(request.Workshop), StringComparison.Ordinal)
                && string.Equals(existingLog.Notes, Normalize(request.Notes), StringComparison.Ordinal)
                && existingLog.IsUnderWarranty == (request.IsUnderWarranty ?? false);
        return sameItem && sameLog
            ? new CreateMaintenanceResult(CreateMaintenanceOutcome.Replayed, CreateMaintenanceResponse.From(existing))
            : new CreateMaintenanceResult(CreateMaintenanceOutcome.Conflict, null);
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
    }
}
