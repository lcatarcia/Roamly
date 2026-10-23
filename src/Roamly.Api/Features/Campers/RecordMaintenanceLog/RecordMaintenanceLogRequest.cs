using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Campers.RecordMaintenanceLog;

/// <summary>Corpo di <c>POST /api/v1/campers/{camperId}/maintenance/{itemId}/logs</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record RecordMaintenanceLogRequest(
    [property: Required] Guid? Id,
    [property: Required] DateOnly? PerformedOnUtc,
    [property: Range(0, int.MaxValue)] int? OdometerKm,
    CostRequest? Cost,
    [property: StringLength(80)] string? Workshop,
    [property: StringLength(1000)] string? Notes) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id == Guid.Empty)
        {
            yield return new ValidationResult("L'identificativo non puo' essere vuoto.", [nameof(Id)]);
        }

        if (Cost is { } cost)
        {
            foreach (var validation in cost.ValidateCost())
            {
                yield return validation;
            }
        }
    }
}

/// <summary>Importo multi-valuta opzionale del service log.</summary>
public sealed record CostRequest(decimal? Amount, string? Currency)
{
    /// <summary>Valida il range e la precisione decimal(19,4), oltre al codice valuta persistibile.</summary>
    public IEnumerable<ValidationResult> ValidateCost()
    {
        const decimal maximum = 999_999_999_999_999.9999m;
        if (Amount is null || Amount.Value < -maximum || Amount.Value > maximum)
        {
            yield return new ValidationResult(
                "L'importo deve rientrare nel range decimal(19,4).",
                ["cost.amount"]);
        }

        if (Amount is { } amount && decimal.Round(amount, 4) != amount)
        {
            yield return new ValidationResult(
                "L'importo non puo' avere piu' di quattro cifre decimali.",
                ["cost.amount"]);
        }

        if (Currency is null || Currency.Length != 3
            || Currency.Any(character => !char.IsAsciiLetterUpper(character)))
        {
            yield return new ValidationResult(
                "La valuta deve essere composta da tre lettere maiuscole ISO 4217.",
                ["cost.currency"]);
        }
    }
}
