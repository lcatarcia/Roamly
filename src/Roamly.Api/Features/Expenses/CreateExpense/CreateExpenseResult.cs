namespace Roamly.Api.Features.Expenses.CreateExpense;

/// <summary>Esito di protocollo della creazione idempotente di una spesa.</summary>
public sealed record CreateExpenseResult(
    CreateExpenseOutcome Outcome,
    ExpenseDetailsResponse? Expense,
    byte[]? RowVersion);

/// <summary>Esiti distinti della richiesta HTTP di creazione.</summary>
public enum CreateExpenseOutcome
{
    /// <summary>Nuova spesa salvata.</summary>
    Created,

    /// <summary>Contenuto identico alla risorsa esistente.</summary>
    Replayed,

    /// <summary>Stesso ID associato a contenuto diverso.</summary>
    Conflict,

    /// <summary>Camper assente o non posseduto.</summary>
    NotFound,

    /// <summary>Viaggio cancellato richiesto per una nuova spesa.</summary>
    CancelledTrip,
}
