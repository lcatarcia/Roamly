using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Places;

/// <summary>Corpo di <c>PUT /api/v1/saved-places/{id}</c>: sostituzione dei dati modificabili.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record UpdateSavedPlaceRequest(
    [property: Required, StringLength(120)] string? Name,
    [property: Required] SavedPlaceCategory? Category,
    SavedPlacePositionRequest? Position,
    [property: StringLength(300)] string? Address,
    [property: StringLength(1000)] string? Notes,
    [property: StringLength(2048)] string? GoogleMapsUrl) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var error in SavedPlaceContent.Validate(Name, Category, GoogleMapsUrl))
        {
            yield return error;
        }
    }
}
