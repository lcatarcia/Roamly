using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Checklists.UpdateChecklist;

/// <summary>Ordine completo delle voci inviato dal client.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record ReorderChecklistItemsRequest(
    [property: Required] Guid[]? ItemIds) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ItemIds is not null && ItemIds.Any(id => id == Guid.Empty))
        {
            yield return new ValidationResult("Gli identificativi non possono essere vuoti.", [nameof(ItemIds)]);
        }
    }
}
