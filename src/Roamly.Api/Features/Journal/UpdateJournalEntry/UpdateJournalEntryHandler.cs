using Roamly.Api.Common.Http;
using Roamly.Api.Features.Journal;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Journal.UpdateJournalEntry;

[ProtocolOutcomeHandler("404 owner-scoped e 428/412 sul Trip sono esiti previsti.")]
public sealed class UpdateJournalEntryHandler(RoamlyDbContext db, TimeProvider clock)
{
    public async Task<JournalMutationResult> HandleAsync(
        Guid tripId,
        Guid entryId,
        UpdateJournalEntryRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var trip = await JournalMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        var entry = trip is null
            ? null
            : await JournalMutationSupport.FindOwnedEntryAsync(db, tripId, entryId, cancellationToken)
                .ConfigureAwait(false);
        if (trip is null || entry is null)
        {
            return JournalMutationSupport.NotFound();
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
        entry.EntryOnUtc = request.EntryOnUtc!.Value;
        entry.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        entry.Body = request.Body!;
        entry.TripStopId = request.TripStopId;
        entry.UpdatedAtUtc = now;

        var saved = await JournalMutationSupport.SaveTripMutationAsync(
            db, trip, token, now, cancellationToken).ConfigureAwait(false);
        return saved
            ? new JournalMutationResult(JournalOutcome.Updated, JournalEntryResponse.From(entry), trip.RowVersion)
            : new JournalMutationResult(JournalOutcome.PreconditionFailed);
    }
}
