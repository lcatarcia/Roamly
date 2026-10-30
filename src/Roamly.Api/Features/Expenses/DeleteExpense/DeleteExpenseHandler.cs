using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Expenses.DeleteExpense;

/// <summary>Elimina una spesa solo con il token di concorrenza corrente.</summary>
[ProtocolOutcomeHandler("If-Match mancante o non corrente produce gli status R49 428/412.")]
public sealed class DeleteExpenseHandler(RoamlyDbContext db)
{
    /// <summary>Elimina definitivamente la spesa con un singolo SaveChanges.</summary>
    public async Task<DeleteExpenseOutcome> HandleAsync(
        Guid expenseId,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        var expense = await db.Expenses.SingleOrDefaultAsync(
            candidate => candidate.OwnerId == db.CurrentUserId && candidate.Id == expenseId,
            cancellationToken).ConfigureAwait(false);
        if (expense is null)
        {
            return DeleteExpenseOutcome.NotFound;
        }

        if (ifMatch is null)
        {
            return DeleteExpenseOutcome.MissingPrecondition;
        }

        if (!RowVersionEtag.TryParse(ifMatch, out var originalRowVersion))
        {
            return DeleteExpenseOutcome.PreconditionFailed;
        }

        db.Entry(expense).Property(candidate => candidate.RowVersion).OriginalValue = originalRowVersion;
        db.Expenses.Remove(expense);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return DeleteExpenseOutcome.Deleted;
        }
        catch (DbUpdateConcurrencyException)
        {
            return DeleteExpenseOutcome.PreconditionFailed;
        }
    }
}

/// <summary>Esiti HTTP della cancellazione definitiva.</summary>
public enum DeleteExpenseOutcome
{
    /// <summary>Eliminata.</summary>
    Deleted,

    /// <summary>Non trovata o non posseduta.</summary>
    NotFound,

    /// <summary>If-Match assente.</summary>
    MissingPrecondition,

    /// <summary>ETag invalido o conflitto EF.</summary>
    PreconditionFailed,
}
