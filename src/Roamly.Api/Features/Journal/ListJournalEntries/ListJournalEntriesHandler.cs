using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Features.Journal;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Journal.ListJournalEntries;

[ProtocolOutcomeHandler("Un cursore non decodificabile produce il 400 previsto da R52.")]
public sealed class ListJournalEntriesHandler(RoamlyDbContext db)
{
    public async Task<JournalEntryPageResult> HandleAsync(
        Guid tripId,
        ListJournalEntriesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var limit = request.Limit is null
            ? 50
            : int.Parse(request.Limit, NumberStyles.None, CultureInfo.InvariantCulture);
        if (!TryGetAfterId(request.After, out var afterId))
        {
            return new JournalEntryPageResult(JournalOutcome.InvalidCursor);
        }

        var exists = await db.Trips.AnyAsync(
            trip => trip.OwnerId == db.CurrentUserId && trip.Id == tripId,
            cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return new JournalEntryPageResult(JournalOutcome.NotFound);
        }

        var query = db.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == db.CurrentUserId && entry.TripId == tripId);
        if (afterId.HasValue)
        {
            query = query.Where(entry => entry.Id > afterId.Value);
        }

        var rows = await query
            .Include(entry => entry.Attachments)
            .OrderBy(entry => entry.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var hasNextPage = rows.Count > limit;
        if (hasNextPage)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return new JournalEntryPageResult(
            JournalOutcome.Found,
            new Page<JournalEntryResponse>(
                rows.Select(JournalEntryResponse.From).ToArray(),
                hasNextPage ? KeysetCursor.Encode(rows[^1].Id) : null));
    }

    private static bool TryGetAfterId(string? cursor, out Guid? afterId)
    {
        afterId = null;
        if (cursor is null)
        {
            return true;
        }

        if (!KeysetCursor.TryDecode(cursor, out var decodedId))
        {
            return false;
        }

        afterId = decodedId;
        return true;
    }
}
