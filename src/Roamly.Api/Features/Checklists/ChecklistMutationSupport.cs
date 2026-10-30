using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Checklists;

public enum ChecklistOutcome
{
    Found,
    Created,
    Replayed,
    Updated,
    Deleted,
    NotFound,
    MissingPrecondition,
    PreconditionFailed,
    Conflict,
    InvalidItemIds,
}

public sealed record ChecklistResult(
    ChecklistOutcome Outcome,
    ChecklistDetailsResponse? Checklist = null,
    byte[]? RowVersion = null);

internal static class ChecklistMutationSupport
{
    public static Task<Trip?> FindOwnedTripAsync(
        RoamlyDbContext db,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return db.Trips.SingleOrDefaultAsync(
            trip => trip.OwnerId == db.CurrentUserId && trip.Id == tripId,
            cancellationToken);
    }

    public static Task<Checklist?> FindOwnedChecklistAsync(
        RoamlyDbContext db,
        Guid tripId,
        Guid checklistId,
        CancellationToken cancellationToken)
    {
        return db.Checklists
            .Include(checklist => checklist.Items)
            .SingleOrDefaultAsync(
                checklist => checklist.OwnerId == db.CurrentUserId
                    && checklist.TripId == tripId
                    && checklist.Id == checklistId,
                cancellationToken);
    }

    public static Task<Checklist?> FindOwnedChecklistByIdAsync(
        RoamlyDbContext db,
        Guid checklistId,
        CancellationToken cancellationToken)
    {
        return db.Checklists.AsNoTracking()
            .Include(checklist => checklist.Items)
            .SingleOrDefaultAsync(
                checklist => checklist.OwnerId == db.CurrentUserId && checklist.Id == checklistId,
                cancellationToken);
    }

    public static Task<ChecklistItem?> FindOwnedItemByIdAsync(
        RoamlyDbContext db,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        return db.ChecklistItems.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OwnerId == db.CurrentUserId && item.Id == itemId,
                cancellationToken);
    }

    public static ChecklistOutcome? CheckPrecondition(Trip trip, string? ifMatch, out byte[] token)
    {
        token = [];
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return ChecklistOutcome.MissingPrecondition;
        }

        return !RowVersionEtag.TryParse(ifMatch, out token)
            || trip.RowVersion is not { Length: > 0 } current
            || !current.AsSpan().SequenceEqual(token)
                ? ChecklistOutcome.PreconditionFailed
                : null;
    }

    public static void TouchTrip(RoamlyDbContext db, Trip trip, byte[] token, DateTime nowUtc)
    {
        trip.UpdatedAtUtc = nowUtc;
        db.Entry(trip).Property(item => item.UpdatedAtUtc).IsModified = true;
        db.Entry(trip).Property(item => item.RowVersion).OriginalValue = token;
    }

    public static async Task<bool> SaveTripMutationAsync(
        RoamlyDbContext db,
        Trip trip,
        byte[] token,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        TouchTrip(db, trip, token, nowUtc);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public static ChecklistResult UpdatedResult(Checklist checklist, Trip trip) =>
        new(ChecklistOutcome.Updated, ChecklistDetailsResponse.From(checklist), trip.RowVersion);

    public static ChecklistResult NotFoundResult() => new(ChecklistOutcome.NotFound);
}
