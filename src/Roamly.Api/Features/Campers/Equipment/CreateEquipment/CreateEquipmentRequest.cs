using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Campers.Equipment.CreateEquipment;

/// <summary>Corpo di <c>POST /api/v1/campers/{camperId}/equipment</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record CreateEquipmentRequest(
    [property: Required] Guid? Id,
    [property: Required, StringLength(60)] string? Name,
    [property: Required, EnumDataType(typeof(EquipmentCategory))] EquipmentCategory? Category,
    [property: StringLength(500)] string? Notes,
    DateOnly? InstalledOnUtc) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id == Guid.Empty)
        {
            yield return new ValidationResult("L'identificativo non puo' essere vuoto.", [nameof(Id)]);
        }

        foreach (var error in EquipmentRequestValidation.ValidateFields(Name, Category))
        {
            yield return error;
        }
    }
}

/// <summary>Regole condivise di validazione e normalizzazione delle richieste equipment.</summary>
internal static class EquipmentRequestValidation
{
    public static IEnumerable<ValidationResult> ValidateFields(string? name, EquipmentCategory? category)
    {
        if (name is not null && string.IsNullOrWhiteSpace(name))
        {
            yield return new ValidationResult("Il nome e' obbligatorio.", [nameof(CreateEquipmentRequest.Name)]);
        }

        if (category is { } value && !Enum.IsDefined(value))
        {
            yield return new ValidationResult(
                "La categoria selezionata non e' valida.",
                [nameof(CreateEquipmentRequest.Category)]);
        }
    }

    public static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
