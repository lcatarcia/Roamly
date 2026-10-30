using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Api.Features.Expenses.CreateExpense;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Expenses.UpdateExpense;

/// <summary>Corpo di sostituzione delle proprieta' modificabili di una spesa.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record UpdateExpenseRequest(
    [property: Required] ExpenseMoneyRequest? Amount,
    [property: Required, EnumDataType(typeof(ExpenseCategory))] ExpenseCategory? Category,
    [property: Required] DateOnly? IncurredOnUtc,
    [property: StringLength(200)] string? Description,
    int? OdometerKm,
    Guid? TripId) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TripId == Guid.Empty)
        {
            yield return new ValidationResult("L'identificativo del viaggio non puo' essere vuoto.", [nameof(TripId)]);
        }

        if (OdometerKm < 0)
        {
            yield return new ValidationResult("Il chilometraggio non puo' essere negativo.", [nameof(OdometerKm)]);
        }
    }
}
