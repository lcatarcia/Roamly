using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Campers.CreateMaintenance;

/// <summary>Corpo di <c>POST /api/v1/campers/{camperId}/maintenance</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record CreateMaintenanceRequest(
    [property: Required] Guid? Id,
    [property: Required, StringLength(80)] string? Name,
    [property: Required, EnumDataType(typeof(MaintenanceCategory))] MaintenanceCategory? Category,
    [property: Range(1, int.MaxValue)] int? IntervalKm,
    [property: Range(1, short.MaxValue)] short? IntervalMonths) : IValidatableObject
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

        if (IntervalKm is null && IntervalMonths is null)
        {
            yield return new ValidationResult(
                "Indica almeno una ricorrenza in chilometri o in mesi.",
                [nameof(IntervalKm), nameof(IntervalMonths)]);
        }
    }
}
