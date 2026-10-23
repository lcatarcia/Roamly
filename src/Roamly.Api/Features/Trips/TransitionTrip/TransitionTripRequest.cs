using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Validation;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Trips.TransitionTrip;

/// <summary>Destinazione della transizione di stato di un viaggio.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record TransitionTripRequest([property: JsonRequired] TripStatus Status) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Status))
        {
            yield return new ValidationResult(
                "Lo stato di destinazione non e' valido.",
                [nameof(Status)]);
        }
    }
}
