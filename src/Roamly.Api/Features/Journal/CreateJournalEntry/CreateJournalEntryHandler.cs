using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Journal.CreateJournalEntry;

[ProtocolOutcomeHandler("404 owner-scoped, 428/412 sul Trip e replay/conflict IdentityBased sono esiti previsti.")]
public sealed class CreateJournalEntryHandler(RoamlyDbContext db, TimeProvider clock)
{
    public async Task<JournalMutationResult> HandleAsync(
        Guid tripId,
        CreateJournalEntryRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var trip = await JournalMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        if (trip is null)
        {
            return JournalMutationSupport.NotFound();
        }

        var existing = await JournalMutationSupport.FindOwnedEntryByIdAsync(
            db, request.Id!.Value, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return ResolveReplay(existing, tripId, request, trip.RowVersion);
        }

        var precondition = JournalMutationSupport.CheckPrecondition(trip, ifMatch, out var token);
        if (precondition is not null)
        {
            return new JournalMutationResult(precondition.Value);
        }

        if (request.TripStopId is { } stopId
            && !await JournalMutationSupport.StopBelongsToTripAsync(
                db, trip.OwnerId, tripId, stopId, cancellationToken).ConfigureAwait(false))
        {
            return JournalMutationSupport.NotFound();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var entry = new JournalEntry
        {
            Id = request.Id.Value,
            OwnerId = trip.OwnerId,
            TripId = trip.Id,
            TripStopId = request.TripStopId,
            EntryOnUtc = request.EntryOnUtc!.Value,
            Title = NormalizeTitle(request.Title),
            Body = request.Body!,
            CreatedAtUtc = now,
        };
        db.JournalEntries.Add(entry);
        var saved = await JournalMutationSupport.SaveTripMutationAsync(
            db, trip, token, now, cancellationToken).ConfigureAwait(false);
        return saved
            ? new JournalMutationResult(JournalOutcome.Created, JournalEntryResponse.From(entry), trip.RowVersion)
            : new JournalMutationResult(JournalOutcome.PreconditionFailed);
    }

    private static JournalMutationResult ResolveReplay(
        JournalEntry existing,
        Guid tripId,
        CreateJournalEntryRequest request,
        byte[]? rowVersion)
    {
        var sameContent = existing.TripId == tripId
            && existing.EntryOnUtc == request.EntryOnUtc
            && existing.TripStopId == request.TripStopId
            && string.Equals(existing.Title, NormalizeTitle(request.Title), StringComparison.Ordinal)
            && string.Equals(existing.Body, request.Body, StringComparison.Ordinal);
        return sameContent
            ? new JournalMutationResult(JournalOutcome.Replayed, JournalEntryResponse.From(existing), rowVersion)
            : new JournalMutationResult(JournalOutcome.Conflict);
    }

    private static string? NormalizeTitle(string? title) =>
        string.IsNullOrWhiteSpace(title) ? null : title.Trim();
}
