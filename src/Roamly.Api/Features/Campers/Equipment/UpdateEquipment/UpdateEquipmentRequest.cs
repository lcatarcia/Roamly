using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Api.Features.Campers.Equipment.CreateEquipment;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Campers.Equipment.UpdateEquipment;

/// <summary>Corpo di <c>PUT /api/v1/campers/{camperId}/equipment/{equipmentId}</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record UpdateEquipmentRequest(
    [property: Required, StringLength(60)] string? Name,
    [property: Required, EnumDataType(typeof(EquipmentCategory))] EquipmentCategory? Category,
    [property: StringLength(500)] string? Notes,
    DateOnly? InstalledOnUtc) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var error in EquipmentRequestValidation.ValidateFields(Name, Category))
        {
            yield return error;
        }
    }
}
