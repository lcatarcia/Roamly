using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Expenses.CreateExpense;

/// <summary>Corpo di <c>POST /api/v1/campers/{camperId}/expenses</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record CreateExpenseRequest(
    [property: Required] Guid? Id,
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
        if (Id == Guid.Empty)
        {
            yield return new ValidationResult("L'identificativo non puo' essere vuoto.", [nameof(Id)]);
        }

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

/// <summary>Importo e codice valuta espliciti.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record ExpenseMoneyRequest(
    [property: Required] decimal? Amount,
    [property: Required, StringLength(3, MinimumLength = 3)] string? Currency) : IValidatableObject
{
    private const decimal MaximumAmount = 999999999999999.9999m;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Amount is { } amount
            && (amount == 0
                || amount < -MaximumAmount
                || amount > MaximumAmount
                || ((decimal.GetBits(amount)[3] >> 16) & 0xFF) > 4))
        {
            yield return new ValidationResult(
                "L'importo deve essere diverso da zero, avere al massimo 4 decimali e rientrare in decimal(19,4).",
                [nameof(Amount)]);
        }

        if (Currency is { } currency
            && (currency.Length != 3 || currency.Any(character => !char.IsAsciiLetterUpper(character))))
        {
            yield return new ValidationResult("La valuta deve contenere 3 lettere ASCII maiuscole.", [nameof(Currency)]);
        }
    }
}
