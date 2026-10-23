using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Checklists.ListChecklists;

/// <summary>Legge le checklist di un viaggio posseduto in ordine COMB.</summary>
[ProtocolOutcomeHandler("Il viaggio assente produce 404 e un cursore non valido produce il 400 previsto da R52.")]
public sealed class ListChecklistsHandler(RoamlyDbContext db)
{
    private const int DefaultLimit = 50;

    /// <summary>Restituisce la pagina, il token del viaggio o l'esito HTTP previsto.</summary>
    public async Task<ListChecklistsResult> HandleAsync(
        Guid tripId,
        ListChecklistsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trip = await ChecklistMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        if (trip is null)
        {
            return new ListChecklistsResult(ListChecklistsOutcome.NotFound);
        }

        if (!TryGetAfterId(request.After, out var afterId))
        {
            return new ListChecklistsResult(ListChecklistsOutcome.InvalidCursor);
        }

        var limit = request.Limit is null
            ? DefaultLimit
            : int.Parse(request.Limit, NumberStyles.None, CultureInfo.InvariantCulture);
        var rows = await ReadRowsAsync(tripId, afterId, limit, cancellationToken).ConfigureAwait(false);
        var hasNextPage = rows.Count > limit;
        if (hasNextPage)
        {
            rows.RemoveAt(limit);
        }

        var nextCursor = hasNextPage ? KeysetCursor.Encode(rows[^1].Id) : null;
        return new ListChecklistsResult(
            ListChecklistsOutcome.Found,
            new Page<ChecklistDetailsResponse>(rows, nextCursor),
            trip.RowVersion);
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

    private async Task<List<ChecklistDetailsResponse>> ReadRowsAsync(
        Guid tripId,
        Guid? afterId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = db.Checklists
            .AsNoTracking()
            .Where(checklist =>
                checklist.OwnerId == db.CurrentUserId
                && checklist.TripId == tripId);
        if (afterId.HasValue)
        {
            query = query.Where(checklist => checklist.Id > afterId.Value);
        }

        // Split loading reads the bounded checklist page and its items in two queries, not N+1.
        var checklists = await query
            .Include(checklist => checklist.Items)
            .AsSplitQuery()
            .OrderBy(checklist => checklist.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return checklists.Select(ChecklistDetailsResponse.From).ToList();
    }
}

/// <summary>Esito della lettura paginata delle checklist.</summary>
public enum ListChecklistsOutcome
{
    Found,
    NotFound,
    InvalidCursor,
}

/// <summary>Pagina e token ETag del viaggio che contiene le checklist.</summary>
public sealed record ListChecklistsResult(
    ListChecklistsOutcome Outcome,
    Page<ChecklistDetailsResponse>? Page = null,
    byte[]? RowVersion = null);
