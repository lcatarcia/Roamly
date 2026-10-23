using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Checklists.CreateChecklist;

/// <summary>Corpo di <c>POST /api/v1/trips/{tripId}/checklists</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record CreateChecklistRequest(
    [property: Required] Guid? Id,
    [property: Required, StringLength(80)] string? Name,
    [property: Required] ChecklistKind? Kind) : IValidatableObject
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

        if (Kind.HasValue && !Enum.IsDefined(Kind.Value))
        {
            yield return new ValidationResult("Il momento della checklist non e' valido.", [nameof(Kind)]);
        }
    }
}
