using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Features.Campers.Equipment;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.Equipment.ListEquipment;

/// <summary>Legge l'inventario in pagine keyset owner-scoped.</summary>
[ProtocolOutcomeHandler("Un camper non posseduto e un cursore malformato hanno risposte HTTP distinte.")]
public sealed class ListEquipmentHandler(RoamlyDbContext db)
{
    private const int DefaultLimit = 50;

    /// <summary>Restituisce la pagina del camper posseduto o un esito di protocollo esplicito.</summary>
    public async Task<ListEquipmentResult> HandleAsync(
        Guid camperId,
        ListEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await CamperExistsAsync(camperId, cancellationToken).ConfigureAwait(false))
        {
            return new ListEquipmentResult(null, false, true);
        }

        if (!TryGetAfterId(request.After, out var afterId))
        {
            return new ListEquipmentResult(null, true, false);
        }

        var limit = request.Limit ?? DefaultLimit;
        var rows = await ReadRowsAsync(camperId, afterId, limit, cancellationToken).ConfigureAwait(false);
        return new ListEquipmentResult(CreatePage(rows, limit), false, false);
    }

    private Task<bool> CamperExistsAsync(Guid camperId, CancellationToken cancellationToken)
    {
        return db.Campers.AnyAsync(
            item => item.OwnerId == db.CurrentUserId && item.Id == camperId,
            cancellationToken);
    }

    private async Task<List<EquipmentResponse>> ReadRowsAsync(
        Guid camperId,
        Guid? afterId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = db.Equipment.AsNoTracking()
            .Where(item => item.OwnerId == db.CurrentUserId && item.CamperId == camperId);
        if (afterId.HasValue)
        {
            query = query.Where(item => item.Id > afterId.Value);
        }

        return await query.OrderBy(item => item.Id)
            .Select(item => new EquipmentResponse(
                item.Id,
                item.Name,
                (int)item.Category,
                item.Notes,
                item.InstalledOnUtc))
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryGetAfterId(string? cursor, out Guid? afterId)
    {
        afterId = null;
        if (cursor is null)
        {
            return true;
        }

        if (!KeysetCursor.TryDecode(cursor, out var decoded))
        {
            return false;
        }

        afterId = decoded;
        return true;
    }

    private static Page<EquipmentResponse> CreatePage(List<EquipmentResponse> rows, int limit)
    {
        var hasNext = rows.Count > limit;
        if (hasNext)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var cursor = hasNext ? KeysetCursor.Encode(rows[^1].Id) : null;
        return new Page<EquipmentResponse>(rows, cursor);
    }
}
