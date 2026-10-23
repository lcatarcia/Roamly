using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Journal.CreateJournalEntry;

#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record CreateJournalEntryRequest(
    [property: Required] Guid? Id,
    [property: Required] DateOnly? EntryOnUtc,
    [property: StringLength(120)] string? Title,
    [property: Required] string? Body,
    Guid? TripStopId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id == Guid.Empty)
        {
            yield return new ValidationResult("L'identificativo non puo' essere vuoto.", [nameof(Id)]);
        }

        if (Title is not null && Title.Length > 120)
        {
            yield return new ValidationResult("Il titolo supera i 120 caratteri.", [nameof(Title)]);
        }

        if (Body is not null && string.IsNullOrWhiteSpace(Body))
        {
            yield return new ValidationResult("Il testo del ricordo e' obbligatorio.", [nameof(Body)]);
        }
    }
}
