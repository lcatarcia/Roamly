using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Campers.ParseCamperManual;

/// <summary>Richiesta di estrazione bozza tecnica e misure da PDF, URL esterno o testo del manuale.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record ParseCamperManualRequest(
    [property: StringLength(200)] string? SourceName = null,
    [property: StringLength(500)] string? SourceUrl = null,
    [property: StringLength(20_000_000)] string? PdfBase64 = null,
    [property: StringLength(200_000)] string? RawText = null)
    : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasPdf = !string.IsNullOrWhiteSpace(PdfBase64);
        var hasUrl = !string.IsNullOrWhiteSpace(SourceUrl);
        var hasText = !string.IsNullOrWhiteSpace(RawText);

        if (!hasPdf && !hasUrl && !hasText)
        {
            yield return new ValidationResult(
                "Fornisci un file PDF, un link HTTPS alla scheda tecnica oppure il testo del manuale.",
                [nameof(PdfBase64), nameof(SourceUrl), nameof(RawText)]);
        }

        if (hasUrl)
        {
            if (!Uri.TryCreate(SourceUrl!.Trim(), UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps)
            {
                yield return new ValidationResult(
                    "Il link esterno deve essere un URL HTTPS valido.",
                    [nameof(SourceUrl)]);
            }
        }
    }
}
