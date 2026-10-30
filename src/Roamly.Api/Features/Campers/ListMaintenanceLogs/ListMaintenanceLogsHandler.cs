using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.ListMaintenanceLogs;

/// <summary>Legge lo storico di una manutenzione con paginazione keyset owner-scoped.</summary>
[ProtocolOutcomeHandler("Un camper o una manutenzione non posseduti hanno risposta uniforme 404.")]
public sealed class ListMaintenanceLogsHandler(RoamlyDbContext db)
{
    private const int DefaultPageSize = 50;

    /// <summary>Restituisce la pagina o un esito non trovato/cursore non valido.</summary>
    public async Task<ListMaintenanceLogsResult> HandleAsync(
        Guid camperId,
        Guid maintenanceItemId,
        ListMaintenanceLogsRequest request,
        CancellationToken cancellationToken)
    {
        var itemExists = await db.MaintenanceItems.AnyAsync(
            item => item.CamperId == camperId && item.Id == maintenanceItemId,
            cancellationToken).ConfigureAwait(false);
        if (!itemExists)
        {
            return new ListMaintenanceLogsResult(null, false);
        }

        if (!TryReadCursor(request.After, out var cursor))
        {
            return new ListMaintenanceLogsResult(null, true);
        }

        var pageSize = request.Limit ?? DefaultPageSize;
        var logs = await ReadLogsAsync(
            maintenanceItemId,
            cursor,
            pageSize,
            cancellationToken).ConfigureAwait(false);
        var hasMore = logs.Count > pageSize;
        if (hasMore)
        {
            logs.RemoveAt(pageSize);
        }

        var nextCursor = hasMore ? KeysetCursor.Encode(logs[^1].Id) : null;
        var page = new Page<ListMaintenanceLogsResponse>(
            logs.Select(ListMaintenanceLogsResponse.From).ToArray(),
            nextCursor);
        return new ListMaintenanceLogsResult(page, false);
    }

    private Task<List<MaintenanceLog>> ReadLogsAsync(
        Guid maintenanceItemId,
        Guid? after,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.MaintenanceLogs.AsNoTracking()
            .Where(log => log.MaintenanceItemId == maintenanceItemId);
        if (after.HasValue)
        {
            query = query.Where(log => log.Id.CompareTo(after.Value) > 0);
        }

        return query.OrderBy(log => log.Id)
            .Take(pageSize + 1)
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

/// <summary>Esito della lettura dello storico.</summary>
public sealed record ListMaintenanceLogsResult(
    Page<ListMaintenanceLogsResponse>? Page,
    bool InvalidCursor);
