using Roamly.Api.Common.Http;
using Roamly.Api.Features.Journal;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Journal.GetJournalEntry;

[ProtocolOutcomeHandler("Una voce assente o non posseduta restituisce il 404 uniforme.")]
public sealed class GetJournalEntryHandler(RoamlyDbContext db)
{
    public async Task<JournalEntryReadResult> HandleAsync(
        Guid tripId,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        var trip = await JournalMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        var entry = trip is null
            ? null
            : await JournalMutationSupport.FindOwnedEntryAsync(db, tripId, entryId, cancellationToken)
                .ConfigureAwait(false);
        return trip is null || entry is null
            ? new JournalEntryReadResult(JournalOutcome.NotFound)
            : new JournalEntryReadResult(
                JournalOutcome.Found,
                JournalEntryResponse.From(entry),
                trip.RowVersion);
    }
}
