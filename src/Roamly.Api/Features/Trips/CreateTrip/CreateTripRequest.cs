using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Trips.CreateTrip;

/// <summary>Corpo di <c>POST /api/v1/campers/{camperId}/trips</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record CreateTripRequest(
    [property: Required] Guid? Id,
    [property: Required, StringLength(80)] string? Name,
    [property: Required, MinLength(1)] CreateTripStopRequest[]? Stops) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id == Guid.Empty)
        {
            yield return new ValidationResult("L'identificativo non puo' essere vuoto.", [nameof(Id)]);
        }

        if (Name is not null && string.IsNullOrWhiteSpace(Name.Trim()))
        {
            yield return new ValidationResult("Il nome e' obbligatorio.", [nameof(Name)]);
        }

        if (Stops is not null && Array.Exists(Stops, stop => stop is null))
        {
            yield return new ValidationResult("Ogni tappa deve essere valorizzata.", [nameof(Stops)]);
        }
    }
}

/// <summary>Richiesta di una tappa; la sequenza e' determinata dall'ordine nell'array.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record CreateTripStopRequest([property: Required, StringLength(80)] string? Name)
    : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name is not null && string.IsNullOrWhiteSpace(Name.Trim()))
        {
            yield return new ValidationResult("Il nome della tappa e' obbligatorio.", [nameof(Name)]);
        }
    }
}
