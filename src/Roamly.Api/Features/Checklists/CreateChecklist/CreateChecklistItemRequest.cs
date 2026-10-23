using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Checklists.CreateChecklist;

/// <summary>Corpo di <c>POST /api/v1/trips/{tripId}/checklists/{checklistId}/items</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record CreateChecklistItemRequest(
    [property: Required] Guid? Id,
    [property: Required, StringLength(200)] string? Text) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id == Guid.Empty)
        {
            yield return new ValidationResult("L'identificativo non puo' essere vuoto.", [nameof(Id)]);
        }

        if (Text is not null && string.IsNullOrWhiteSpace(Text.Trim()))
        {
            yield return new ValidationResult("Il testo della voce e' obbligatorio.", [nameof(Text)]);
        }
    }
}
