using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Api.Features.Campers.RecordMaintenanceLog;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Campers.UpdateMaintenance;

/// <summary>Corpo di <c>PUT /api/v1/campers/{camperId}/maintenance/{maintenanceItemId}</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record UpdateMaintenanceRequest(
    [property: Required, StringLength(80)] string? Name,
    [property: Required, EnumDataType(typeof(MaintenanceCategory))] MaintenanceCategory? Category,
    [property: Range(1, int.MaxValue)] int? IntervalKm,
    [property: Range(1, short.MaxValue)] short? IntervalMonths,
    DateOnly? PerformedOnUtc = null,
    [property: Range(0, int.MaxValue)] int? OdometerKm = null,
    CostRequest? Cost = null,
    [property: StringLength(80)] string? Workshop = null,
    [property: StringLength(1000)] string? Notes = null,
    bool? IsUnderWarranty = null) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name is not null && string.IsNullOrWhiteSpace(Name.Trim()))
        {
            yield return new ValidationResult("Il nome e' obbligatorio.", [nameof(Name)]);
        }

        if (IntervalKm is null && IntervalMonths is null && PerformedOnUtc is null)
        {
            yield return new ValidationResult(
                "Indica almeno una ricorrenza in chilometri o in mesi, oppure la data dell'intervento non periodico.",
                [nameof(IntervalKm), nameof(IntervalMonths)]);
        }

        if (PerformedOnUtc is null && HasServiceDetails())
        {
            yield return new ValidationResult(
                "Indica la data dell'intervento quando compili i dettagli dell'intervento.",
                [nameof(PerformedOnUtc)]);
        }

        if (Cost is { } cost)
        {
            foreach (var validation in cost.ValidateCost())
            {
                yield return validation;
            }
        }

        foreach (var rangeError in ValidateProjectionBounds())
        {
            yield return rangeError;
        }
    }

    private bool HasServiceDetails()
    {
        return OdometerKm.HasValue
            || Cost is not null
            || !string.IsNullOrWhiteSpace(Workshop)
            || !string.IsNullOrWhiteSpace(Notes)
            || IsUnderWarranty == true;
    }

    private IEnumerable<ValidationResult> ValidateProjectionBounds()
    {
        if (PerformedOnUtc is { } performedOn
            && IntervalMonths is { } intervalMonths and >= 1
            && performedOn > DateOnly.MaxValue.AddMonths(-intervalMonths))
        {
            yield return new ValidationResult(
                "La data dell'intervento produce una scadenza fuori dal range supportato.",
                [nameof(PerformedOnUtc)]);
        }

        if (PerformedOnUtc.HasValue
            && IntervalKm is { } intervalKm and >= 1
            && OdometerKm is { } odometerKm
            && (long)odometerKm + intervalKm > int.MaxValue)
        {
            yield return new ValidationResult(
                "Il chilometraggio dell'intervento produce una scadenza fuori dal range supportato.",
                [nameof(OdometerKm)]);
        }
    }
}
