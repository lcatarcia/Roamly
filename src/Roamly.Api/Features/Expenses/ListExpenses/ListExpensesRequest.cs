using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Expenses.ListExpenses;

/// <summary>Filtri whitelisted della collection spese.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record ListExpensesRequest(
    [property: FromQuery(Name = "limit"), RegularExpression(@"\A(?:[1-9]|[1-9][0-9]|1[0-9]{2}|200)\z")]
    string? Limit,
    [property: FromQuery(Name = "after")] string? After,
    [property: FromQuery(Name = "tripId")] Guid? TripId) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TripId == Guid.Empty)
        {
            yield return new ValidationResult("L'identificativo del viaggio non puo' essere vuoto.", [nameof(TripId)]);
        }
    }
}
