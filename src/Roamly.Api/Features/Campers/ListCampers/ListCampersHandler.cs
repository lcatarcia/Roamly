using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.ListCampers;

/// <summary>Legge i camper dell'utente corrente in ordine COMB, senza materializzare entita'.</summary>
[ProtocolOutcomeHandler("Un cursore non decodificabile produce il 400 previsto da R52.")]
public sealed class ListCampersHandler(RoamlyDbContext db)
{
    private const int DefaultLimit = 50;

    /// <summary>Restituisce una pagina owner-scoped oppure null per un cursore malformato.</summary>
    public async Task<Page<ListCampersResponse>?> HandleAsync(
        ListCampersRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var limit = request.Limit ?? DefaultLimit;
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

    private async Task<List<ListCampersResponse>> ReadRowsAsync(
        Guid? afterId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = db.Campers
            .AsNoTracking()
            .Where(camper => camper.OwnerId == db.CurrentUserId);
        if (afterId.HasValue)
        {
            query = query.Where(camper => camper.Id > afterId.Value);
        }

        var rows = await query
            .OrderBy(camper => camper.Id)
            .Select(camper => new ListCampersResponse(camper.Id, camper.Name, camper.VehicleKind))
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows;
    }

    private static Page<ListCampersResponse> CreatePage(List<ListCampersResponse> rows, int limit)
    {
        var hasNextPage = rows.Count > limit;
        if (hasNextPage)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var nextCursor = hasNextPage ? KeysetCursor.Encode(rows[^1].Id) : null;
        return new Page<ListCampersResponse>(rows, nextCursor);
    }
}
