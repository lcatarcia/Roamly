using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Api.Features.Expenses;

/// <summary>Read model pubblico di una spesa.</summary>
public sealed record ExpenseDetailsResponse(
    Guid Id,
    Guid CamperId,
    Guid? TripId,
    Money Amount,
    int Category,
    DateOnly IncurredOnUtc,
    string? Description,
    int? OdometerKm)
{
    /// <summary>Crea il read model dall'entita' persistita.</summary>
    public static ExpenseDetailsResponse From(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);
        return new ExpenseDetailsResponse(
            expense.Id,
            expense.CamperId,
            expense.TripId,
            expense.Amount,
            (int)expense.Category,
            expense.IncurredOnUtc,
            expense.Description,
            expense.OdometerKm);
    }
}
