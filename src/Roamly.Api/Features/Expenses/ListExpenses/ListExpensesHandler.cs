using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Expenses.ListExpenses;

/// <summary>Legge le spese del camper corrente in ordine keyset per ID.</summary>
[ProtocolOutcomeHandler("Un cursore non decodificabile produce il 400 previsto da R52.")]
public sealed class ListExpensesHandler(RoamlyDbContext db)
{
    private const int DefaultLimit = 50;

    /// <summary>Restituisce la pagina, null per camper non posseduto, false per cursore invalido.</summary>
    public async Task<ListExpensesResult> HandleAsync(
        Guid camperId,
        ListExpensesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ownerId = db.CurrentUserId;
        var ownsCamper = await db.Campers.AnyAsync(
            camper => camper.OwnerId == ownerId && camper.Id == camperId,
            cancellationToken).ConfigureAwait(false);
        if (!ownsCamper)
        {
            return new ListExpensesResult(null, false);
        }

        var limit = request.Limit is null
            ? DefaultLimit
            : int.Parse(request.Limit, NumberStyles.None, CultureInfo.InvariantCulture);
        if (!TryGetAfterId(request.After, out var afterId))
        {
            return new ListExpensesResult(null, true);
        }

        var rows = await ReadRowsAsync(ownerId, camperId, afterId, request.TripId, limit, cancellationToken)
            .ConfigureAwait(false);
        return new ListExpensesResult(CreatePage(rows, limit), true);
    }

    private static bool TryGetAfterId(string? cursor, out Guid? afterId)
    {
        afterId = null;
        if (string.IsNullOrEmpty(cursor))
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

    private async Task<List<ExpenseDetailsResponse>> ReadRowsAsync(
        Guid ownerId,
        Guid camperId,
        Guid? afterId,
        Guid? tripId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = db.Expenses.AsNoTracking()
            .Where(expense => expense.OwnerId == ownerId && expense.CamperId == camperId);
        if (afterId.HasValue)
        {
            query = query.Where(expense => expense.Id > afterId.Value);
        }

        if (tripId.HasValue)
        {
            query = query.Where(expense => expense.TripId == tripId.Value);
        }

        return await query.OrderBy(expense => expense.Id)
            .Select(expense => ExpenseDetailsResponse.From(expense))
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static Page<ExpenseDetailsResponse> CreatePage(List<ExpenseDetailsResponse> rows, int limit)
    {
        var hasNextPage = rows.Count > limit;
        if (hasNextPage)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var nextCursor = hasNextPage ? KeysetCursor.Encode(rows[^1].Id) : null;
        return new Page<ExpenseDetailsResponse>(rows, nextCursor);
    }
}

/// <summary>Pagina o esito lookup per una collection spese.</summary>
public sealed record ListExpensesResult(Page<ExpenseDetailsResponse>? Page, bool CamperFound);
