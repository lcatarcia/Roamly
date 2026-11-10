using System.ComponentModel.DataAnnotations;
using Roamly.Domain.Enums;
using Roamly.Domain.ValueObjects;

namespace Roamly.Api.Features.Places;

/// <summary>Regole di contenuto condivise tra creazione e modifica di un luogo salvato.</summary>
internal static class SavedPlaceContent
{
    /// <summary>Valida le regole non esprimibili con i soli attributi di campo.</summary>
    public static IEnumerable<ValidationResult> Validate(
        string? name,
        SavedPlaceCategory? category,
        string? googleMapsUrl)
    {
        if (name is not null && string.IsNullOrWhiteSpace(name.Trim()))
        {
            yield return new ValidationResult("Il nome e' obbligatorio.", ["Name"]);
        }

        if (category.HasValue && !Enum.IsDefined(category.Value))
        {
            yield return new ValidationResult("La categoria non e' valida.", ["Category"]);
        }

        // Il riferimento originale resta conservato; l'estrazione delle coordinate non usa rete.
        if (googleMapsUrl is not null
            && (!Uri.TryCreate(googleMapsUrl.Trim(), UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrWhiteSpace(uri.Host)))
        {
            yield return new ValidationResult("Il link deve essere un URL HTTPS assoluto.", ["GoogleMapsUrl"]);
        }
    }

    /// <summary>Normalizza un testo opzionale: vuoto diventa null, altrimenti viene rifilato.</summary>
    public static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Converte le coordinate validate nel complex type di dominio.</summary>
    public static Coordinates? ToPosition(SavedPlacePositionRequest? position, string? googleMapsUrl)
    {
        return position is null
            ? GoogleMapsPosition.Parse(googleMapsUrl)
            : new Coordinates(position.Latitude!.Value, position.Longitude!.Value);
    }
}
