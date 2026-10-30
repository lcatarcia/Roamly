using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Checklists.UpdateChecklist;

/// <summary>Corpo di <c>PUT /api/v1/trips/{tripId}/checklists/{checklistId}/items/{itemId}</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record UpdateChecklistItemRequest(
    [property: Required, StringLength(200)] string? Text,
    [property: Required] bool? IsDone) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Text is not null && string.IsNullOrWhiteSpace(Text.Trim()))
        {
            yield return new ValidationResult("Il testo della voce e' obbligatorio.", [nameof(Text)]);
        }
    }
}
