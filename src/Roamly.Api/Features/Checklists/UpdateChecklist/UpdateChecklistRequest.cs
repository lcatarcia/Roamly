using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Checklists.UpdateChecklist;

/// <summary>Corpo di <c>PUT /api/v1/trips/{tripId}/checklists/{checklistId}</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record UpdateChecklistRequest(
    [property: Required, StringLength(80)] string? Name,
    [property: Required] ChecklistKind? Kind) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name is not null && string.IsNullOrWhiteSpace(Name.Trim()))
        {
            yield return new ValidationResult("Il nome e' obbligatorio.", [nameof(Name)]);
        }

        if (Kind.HasValue && !Enum.IsDefined(Kind.Value))
        {
            yield return new ValidationResult("Il momento della checklist non e' valido.", [nameof(Kind)]);
        }
    }
}
