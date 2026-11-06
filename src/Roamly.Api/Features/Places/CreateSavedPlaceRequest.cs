using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Places;

/// <summary>Corpo di <c>POST /api/v1/saved-places</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record CreateSavedPlaceRequest(
    [property: Required] Guid? Id,
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
        if (Id == Guid.Empty)
        {
            yield return new ValidationResult("L'identificativo non puo' essere vuoto.", [nameof(Id)]);
        }

        if (Name is not null && string.IsNullOrWhiteSpace(Name.Trim()))
        {
            yield return new ValidationResult("Il nome e' obbligatorio.", [nameof(Name)]);
        }

        if (Category.HasValue && !Enum.IsDefined(Category.Value))
        {
            yield return new ValidationResult("La categoria non e' valida.", [nameof(Category)]);
        }

        if (GoogleMapsUrl is not null
            && (!Uri.TryCreate(GoogleMapsUrl.Trim(), UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrWhiteSpace(uri.Host)))
        {
            yield return new ValidationResult(
                "Il link deve essere un URL HTTPS assoluto.",
                [nameof(GoogleMapsUrl)]);
        }
    }
}

/// <summary>Coordinate inserite manualmente; non viene consultato alcun provider.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record SavedPlacePositionRequest(
    [property: Required, Range(typeof(decimal), "-90", "90")] decimal? Latitude,
    [property: Required, Range(typeof(decimal), "-180", "180")] decimal? Longitude)
    : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Latitude.HasValue && decimal.Round(Latitude.Value, 6) != Latitude.Value)
        {
            yield return new ValidationResult(
                "La latitudine supporta al massimo sei decimali.",
                [nameof(Latitude)]);
        }

        if (Longitude.HasValue && decimal.Round(Longitude.Value, 6) != Longitude.Value)
        {
            yield return new ValidationResult(
                "La longitudine supporta al massimo sei decimali.",
                [nameof(Longitude)]);
        }
    }
}
