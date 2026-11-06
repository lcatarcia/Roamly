using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Identity.Consents;

/// <summary>Identita' COMB client-side per retry; versione e riferimento devono coincidere col testo server.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record RecordConsentRequest(
    [property: Required] Guid? Id,
    [property: Required, StringLength(64)] string? Version,
    [property: Required, StringLength(256)] string? TextReference) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id == Guid.Empty)
        {
            yield return new ValidationResult("L'identificativo non puo' essere vuoto.", [nameof(Id)]);
        }
    }
}
