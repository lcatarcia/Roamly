using Microsoft.EntityFrameworkCore;
using Roamly.Common;
using Roamly.Api.Common.Http;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Journal;

internal static class JournalMutationSupport
{
    public static Task<Trip?> FindOwnedTripAsync(
        RoamlyDbContext db,
        Guid tripId,
        CancellationToken cancellationToken) =>
        db.Trips.SingleOrDefaultAsync(
            trip => trip.OwnerId == db.CurrentUserId && trip.Id == tripId,
            cancellationToken);

    public static Task<JournalEntry?> FindOwnedEntryAsync(
        RoamlyDbContext db,
        Guid tripId,
        Guid entryId,
        CancellationToken cancellationToken) =>
        db.JournalEntries
            .Include(entry => entry.Attachments)
            .SingleOrDefaultAsync(
                entry => entry.OwnerId == db.CurrentUserId
                    && entry.TripId == tripId
                    && entry.Id == entryId,
                cancellationToken);

    public static Task<JournalEntry?> FindOwnedEntryByIdAsync(
        RoamlyDbContext db,
        Guid entryId,
        CancellationToken cancellationToken) =>
        db.JournalEntries.AsNoTracking()
            .Include(entry => entry.Attachments)
            .SingleOrDefaultAsync(
                entry => entry.OwnerId == db.CurrentUserId && entry.Id == entryId,
                cancellationToken);

    public static Task<bool> StopBelongsToTripAsync(
        RoamlyDbContext db,
        Guid ownerId,
        Guid tripId,
        Guid stopId,
        CancellationToken cancellationToken) =>
        db.TripStops.AnyAsync(
            stop => stop.OwnerId == ownerId && stop.TripId == tripId && stop.Id == stopId,
            cancellationToken);

    public static JournalOutcome? CheckPrecondition(Trip trip, string? ifMatch, out byte[] token)
    {
        token = [];
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return JournalOutcome.MissingPrecondition;
        }

        return !RowVersionEtag.TryParse(ifMatch, out token)
            || trip.RowVersion is not { Length: > 0 } current
            || !current.AsSpan().SequenceEqual(token)
                ? JournalOutcome.PreconditionFailed
                : null;
    }

    public static void TouchTrip(RoamlyDbContext db, Trip trip, byte[] token, DateTime nowUtc)
    {
        trip.UpdatedAtUtc = nowUtc;
        db.Entry(trip).Property(item => item.UpdatedAtUtc).IsModified = true;
        db.Entry(trip).Property(item => item.RowVersion).OriginalValue = token;
    }

    public static void QueueBlobDeletion(
        RoamlyDbContext db,
        IIdGenerator ids,
        Guid ownerId,
        string storageKey,
        DateTime createdAtUtc)
    {
        db.JournalAttachmentDeletionOutbox.Add(new JournalAttachmentDeletionOutboxMessage
        {
            Id = ids.NewId(),
            OwnerId = ownerId,
            StorageKey = storageKey,
            CreatedAtUtc = createdAtUtc,
        });
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

    public static JournalMutationResult NotFound() => new(JournalOutcome.NotFound);
}

public enum JournalOutcome
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
    AttachmentLimitReached,
    InvalidCursor,
}

public sealed record JournalMutationResult(
    JournalOutcome Outcome,
    JournalEntryResponse? Entry = null,
    byte[]? RowVersion = null);

public sealed record JournalEntryPageResult(
    JournalOutcome Outcome,
    Roamly.Api.Common.Http.Page<JournalEntryResponse>? Page = null);

public sealed record JournalEntryReadResult(
    JournalOutcome Outcome,
    JournalEntryResponse? Entry = null,
    byte[]? RowVersion = null);

public sealed record JournalAttachmentMutationResult(
    JournalOutcome Outcome,
    JournalAttachmentResponse? Attachment = null,
    byte[]? RowVersion = null);

public sealed record JournalAttachmentDownloadResponse(string Url, DateTimeOffset ExpiresAtUtc);

public sealed record JournalAttachmentDownloadResult(
    JournalOutcome Outcome,
    JournalAttachmentDownloadResponse? Download = null,
    byte[]? RowVersion = null);

public sealed record JournalAttachmentReadResult(
    JournalOutcome Outcome,
    JournalAttachmentResponse? Attachment = null,
    byte[]? RowVersion = null);
