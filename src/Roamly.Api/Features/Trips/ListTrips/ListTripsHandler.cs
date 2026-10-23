using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Trips.ListTrips;

/// <summary>Legge i viaggi dell'utente corrente in ordine COMB.</summary>
[ProtocolOutcomeHandler("Un cursore non decodificabile produce il 400 previsto da R52.")]
public sealed class ListTripsHandler(RoamlyDbContext db)
{
    private const int DefaultLimit = 50;

    /// <summary>Restituisce una pagina owner-scoped o null per un cursore malformato.</summary>
    public async Task<Page<ListTripsResponse>?> HandleAsync(
        ListTripsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var limit = request.Limit is null
            ? DefaultLimit
            : int.Parse(request.Limit, NumberStyles.None, CultureInfo.InvariantCulture);
        if (!TryGetAfterId(request.After, out var afterId))
        {
            return null;
        }

        var rows = await ReadRowsAsync(afterId, limit, cancellationToken).ConfigureAwait(false);
        return CreatePage(rows, limit);
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

    private async Task<List<ListTripsResponse>> ReadRowsAsync(
        Guid? afterId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = db.Trips
            .AsNoTracking()
            .Where(trip => trip.OwnerId == db.CurrentUserId);
        if (afterId.HasValue)
        {
            query = query.Where(trip => trip.Id > afterId.Value);
        }

        return await query
            .OrderBy(trip => trip.Id)
            .Select(trip => new ListTripsResponse(
                trip.Id,
                trip.CamperId,
                trip.Name,
                (int)trip.Status))
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static Page<ListTripsResponse> CreatePage(List<ListTripsResponse> rows, int limit)
    {
        var hasNextPage = rows.Count > limit;
        if (hasNextPage)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var nextCursor = hasNextPage ? KeysetCursor.Encode(rows[^1].Id) : null;
        return new Page<ListTripsResponse>(rows, nextCursor);
    }
}
