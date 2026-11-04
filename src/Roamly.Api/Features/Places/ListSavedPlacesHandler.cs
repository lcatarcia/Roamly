using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Places;

/// <summary>Elenca i luoghi dell'utente in ordine dell'ID COMB.</summary>
[ProtocolOutcomeHandler("Un cursore non decodificabile produce il 400 previsto da R52.")]
public sealed class ListSavedPlacesHandler(RoamlyDbContext db)
{
    private const int DefaultLimit = 50;

    /// <summary>Restituisce la pagina owner-scoped oppure null per un cursore malformato.</summary>
    public async Task<Page<SavedPlaceResponse>?> HandleAsync(
        ListSavedPlacesRequest request,
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

    private async Task<List<SavedPlaceResponse>> ReadRowsAsync(
        Guid? afterId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = db.SavedPlaces.AsNoTracking()
            .Where(place => place.OwnerId == db.CurrentUserId);
        if (afterId.HasValue)
        {
            query = query.Where(place => place.Id.CompareTo(afterId.Value) > 0);
        }

        return await query
            .OrderBy(place => place.Id)
            .Select(place => new SavedPlaceResponse(
                place.Id,
                place.Name,
                (int)place.Category,
                place.Position == null
                    ? null
                    : new SavedPlacePositionResponse(
                        place.Position.Value.Latitude,
                        place.Position.Value.Longitude),
                place.Address,
                place.Notes,
                place.GoogleMapsUrl))
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static Page<SavedPlaceResponse> CreatePage(List<SavedPlaceResponse> rows, int limit)
    {
        var hasNextPage = rows.Count > limit;
        if (hasNextPage)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var nextCursor = hasNextPage ? KeysetCursor.Encode(rows[^1].Id) : null;
        return new Page<SavedPlaceResponse>(rows, nextCursor);
    }
}
