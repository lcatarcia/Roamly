using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Campers.CreateCamper;

/// <summary>Corpo di <c>POST /api/v1/campers</c>.</summary>
// R48 (ADR-0005) richiede i metadati di validazione source-generated di .NET 10 su ogni request.
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record CreateCamperRequest(
    [property: Required] Guid? Id,
    [property: Required, StringLength(60)] string? Name,
    [property: Required, EnumDataType(typeof(VehicleKind))] VehicleKind? VehicleKind)
    : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id == Guid.Empty)
        {
            yield return new ValidationResult("L'identificativo non puo' essere vuoto.", [nameof(Id)]);
        }

        if (Name is not null && string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult("Il nome e' obbligatorio.", [nameof(Name)]);
        }
    }
}
