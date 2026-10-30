using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.ListMaintenance;

/// <summary>Legge la collection di manutenzione in ordine COMB e con cursore keyset (R52).</summary>
[ProtocolOutcomeHandler("Un camper assente o un cursore malformato ha un contratto HTTP 404 o 400.")]
public sealed class ListMaintenanceHandler(RoamlyDbContext db)
{
    private const int DefaultPageSize = 50;

    /// <summary>Restituisce la pagina, null per camper assente o cursore non valido.</summary>
    public async Task<ListMaintenanceResult> HandleAsync(
        Guid camperId,
        ListMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        var camperExists = await db.Campers.AnyAsync(
            camper => camper.Id == camperId,
            cancellationToken).ConfigureAwait(false);
        if (!camperExists)
        {
            return new ListMaintenanceResult(null, false);
        }

        if (!TryReadCursor(request.After, out var cursor))
        {
            return new ListMaintenanceResult(null, true);
        }

        var pageSize = request.Limit ?? DefaultPageSize;
        var items = await ReadItemsAsync(
            camperId,
            cursor,
            pageSize,
            cancellationToken).ConfigureAwait(false);
        var hasMore = items.Count > pageSize;
        if (hasMore)
        {
            items.RemoveAt(pageSize);
        }

        var nextCursor = hasMore ? KeysetCursor.Encode(items[^1].Id) : null;
        return new ListMaintenanceResult(new Page<ListMaintenanceResponse>(items, nextCursor), false);
    }

    private Task<List<ListMaintenanceResponse>> ReadItemsAsync(
        Guid camperId,
        Guid? after,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.MaintenanceItems.AsNoTracking()
            .Where(item => item.CamperId == camperId);
        if (after.HasValue)
        {
            query = query.Where(item => item.Id.CompareTo(after.Value) > 0);
        }

        return query.OrderBy(item => item.Id)
            .Take(pageSize + 1)
            .Select(item => new ListMaintenanceResponse(
                item.Id,
                item.Name,
                (int)item.Category,
                item.IntervalKm,
                item.IntervalMonths,
                item.LastServiceOnUtc,
                item.LastServiceOdometerKm,
                item.NextDueOnUtc,
                item.NextDueOdometerKm))
            .ToListAsync(cancellationToken);
    }

    private static bool TryReadCursor(string? after, out Guid? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(after))
        {
            return true;
        }

        if (!KeysetCursor.TryDecode(after, out var id))
        {
            return false;
        }

        cursor = id;
        return true;
    }
}

/// <summary>Esito della lettura della collection.</summary>
public sealed record ListMaintenanceResult(Page<ListMaintenanceResponse>? Page, bool InvalidCursor);
