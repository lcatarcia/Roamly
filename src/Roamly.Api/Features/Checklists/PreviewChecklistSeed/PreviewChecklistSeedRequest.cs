using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Validation;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Checklists.PreviewChecklistSeed;

/// <summary>Momento richiesto per l'anteprima del catalogo di checklist.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record PreviewChecklistSeedRequest(
    [property: FromQuery(Name = "kind"), Required] ChecklistKind? Kind) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Kind.HasValue && !Enum.IsDefined(Kind.Value))
        {
            yield return new ValidationResult("Il momento della checklist non e' valido.", [nameof(Kind)]);
        }
    }
}
