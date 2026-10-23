using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Features.Expenses;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Expenses.GetExpense;

/// <summary>Legge il dettaglio di una spesa dell'utente corrente.</summary>
[ProtocolOutcomeHandler("Una spesa assente o non posseduta produce il 404 uniforme.")]
public sealed class GetExpenseHandler(RoamlyDbContext db)
{
    /// <summary>Restituisce read model e token, oppure null per una risorsa assente/non posseduta.</summary>
    public async Task<GetExpenseResult?> HandleAsync(Guid expenseId, CancellationToken cancellationToken)
    {
        var expense = await db.Expenses.AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OwnerId == db.CurrentUserId && candidate.Id == expenseId,
                cancellationToken).ConfigureAwait(false);
        return expense is null
            ? null
            : new GetExpenseResult(ExpenseDetailsResponse.From(expense), expense.RowVersion);
    }
}

/// <summary>Dettaglio HTTP e token per l'ETag.</summary>
public sealed record GetExpenseResult(ExpenseDetailsResponse Expense, byte[]? RowVersion);
