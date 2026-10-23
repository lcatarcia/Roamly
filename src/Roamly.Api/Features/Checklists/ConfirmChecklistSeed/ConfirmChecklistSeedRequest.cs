using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;
using Roamly.Domain.Enums;

namespace Roamly.Api.Features.Checklists.ConfirmChecklistSeed;

/// <summary>Identita' stabile e catalogo richiesto per una conferma bulk.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record ConfirmChecklistSeedRequest(
    [property: Required] Guid? Id,
    [property: Required] ChecklistKind? Kind,
    [property: Required, MinLength(1), MaxLength(32)]
    ConfirmChecklistSeedItemRequest[]? Items) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id == Guid.Empty)
        {
            yield return new ValidationResult("L'identificativo non puo' essere vuoto.", [nameof(Id)]);
        }

        if (Kind.HasValue && !Enum.IsDefined(Kind.Value))
        {
            yield return new ValidationResult("Il momento della checklist non e' valido.", [nameof(Kind)]);
        }

        if (Items is null)
        {
            yield break;
        }

        var ids = new HashSet<Guid>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Items)
        {
            if (item is null)
            {
                yield return new ValidationResult("La voce della checklist non e' valida.", [nameof(Items)]);
                continue;
            }

            if (item.Id is null || item.Id == Guid.Empty || !ids.Add(item.Id.Value))
            {
                yield return new ValidationResult("Gli identificativi delle voci devono essere univoci.", [nameof(Items)]);
            }

            if (string.IsNullOrWhiteSpace(item.TemplateKey)
                || !keys.Add(item.TemplateKey))
            {
                yield return new ValidationResult("Le chiavi catalogo devono essere presenti e univoche.", [nameof(Items)]);
            }
        }
    }
}

/// <summary>Identita' della voce, senza testo client-side: testo e ordine derivano dal catalogo server.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record ConfirmChecklistSeedItemRequest(
    [property: Required] Guid? Id,
    [property: Required, StringLength(64)] string? TemplateKey);
