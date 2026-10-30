using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.Extensions.Validation;
using Roamly.Domain.ValueObjects;

namespace Roamly.Api.Features.Trips.UpdateTrip;

/// <summary>Corpo completo di <c>PUT /api/v1/trips/{tripId}</c>.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed class UpdateTripRequest : IValidatableObject
{
    /// <summary>Nome del viaggio.</summary>
    [Required, StringLength(80)]
    public string? Name { get; init; }

    /// <summary>Inizio pianificato, azzerabile.</summary>
    public DateOnly? PlannedStartOnUtc { get; init; }

    /// <summary>Fine pianificata, azzerabile.</summary>
    public DateOnly? PlannedEndOnUtc { get; init; }

    /// <summary>Distanza totale stimata manualmente, come stringa decimale esatta in km.</summary>
    [RegularExpression(@"^\d{1,16}(?:\.\d{1,2})?$")]
    public string? EstimatedDistanceKm { get; init; }

    /// <summary>Durata totale stimata manualmente per l'intero viaggio, in minuti.</summary>
    [Range(0, int.MaxValue)]
    public int? EstimatedDurationMinutes { get; init; }

    /// <summary>Prezzo manuale per litro; null o omesso lo cancella.</summary>
    public UpdateTripFuelPriceRequest? FuelPricePerLiter { get; init; }

    /// <summary>Elenco completo delle tappe in ordine.</summary>
    [Required, MinLength(1)]
    public UpdateTripStopRequest[]? Stops { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult("Il nome del viaggio e' obbligatorio.", [nameof(Name)]);
        }

        if (PlannedStartOnUtc is { } start && PlannedEndOnUtc is { } end && start > end)
        {
            yield return new ValidationResult(
                "La data di inizio pianificata deve precedere o coincidere con la fine.",
                [nameof(PlannedStartOnUtc), nameof(PlannedEndOnUtc)]);
        }

        if (EstimatedDistanceKm is { } distance
            && (!decimal.TryParse(
                    distance,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var parsedDistance)
                || parsedDistance < 0m))
        {
            yield return new ValidationResult(
                "La distanza stimata deve essere una stringa decimale non negativa, con al massimo 16 cifre intere e 2 decimali.",
                [nameof(EstimatedDistanceKm)]);
        }

        if (Stops is null)
        {
            yield break;
        }

        if (Array.Exists(Stops, stop => stop is null))
        {
            yield return new ValidationResult("Ogni tappa deve essere valorizzata.", [nameof(Stops)]);
        }

        var ids = Stops.Where(stop => stop?.Id is not null).Select(stop => stop!.Id!.Value).ToArray();
        if (ids.Contains(Guid.Empty))
        {
            yield return new ValidationResult("L'identificativo di una tappa non puo' essere vuoto.", [nameof(Stops)]);
        }

        if (ids.Distinct().Count() != ids.Length)
        {
            yield return new ValidationResult("Gli identificativi delle tappe devono essere univoci.", [nameof(Stops)]);
        }
    }
}

/// <summary>Prezzo carburante unitario in una valuta esplicita.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record UpdateTripFuelPriceRequest(
    [property: Required] decimal? Amount,
    [property: Required, StringLength(3, MinimumLength = 3)] string? Currency) : IValidatableObject
{
    private const decimal MaximumAmount = 999999999999999.9999m;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Amount is { } amount
            && (amount <= 0m
                || amount > MaximumAmount
                || ((decimal.GetBits(amount)[3] >> 16) & 0xFF) > 4))
        {
            yield return new ValidationResult(
                "Il prezzo per litro deve essere positivo, avere al massimo 4 decimali e rientrare in decimal(19,4).",
                [nameof(Amount)]);
        }

        if (Currency is { } currency
            && (currency.Length != 3 || currency.Any(character => !char.IsAsciiLetterUpper(character))))
        {
            yield return new ValidationResult(
                "La valuta deve contenere 3 lettere ASCII maiuscole.",
                [nameof(Currency)]);
        }
    }
}

/// <summary>Una tappa completa; senza ID rappresenta una nuova tappa.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed class UpdateTripStopRequest : IValidatableObject
{
    /// <summary>Identificativo della tappa esistente; omesso o null per una nuova tappa.</summary>
    public Guid? Id { get; init; }

    /// <summary>Nome della tappa.</summary>
    [Required, StringLength(80)]
    public string? Name { get; init; }

    /// <summary>Data di arrivo.</summary>
    public DateOnly? ArrivalOnUtc { get; init; }

    /// <summary>Data di partenza.</summary>
    public DateOnly? DepartureOnUtc { get; init; }

    /// <summary>Coordinate WGS84 opzionali.</summary>
    public UpdateTripPositionRequest? Position { get; init; }

    /// <summary>Note della tappa.</summary>
    [StringLength(1000)]
    public string? Notes { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult("Il nome della tappa e' obbligatorio.", [nameof(Name)]);
        }

        if (ArrivalOnUtc is { } arrival && DepartureOnUtc is { } departure && arrival > departure)
        {
            yield return new ValidationResult(
                "L'arrivo deve precedere o coincidere con la partenza.",
                [nameof(ArrivalOnUtc), nameof(DepartureOnUtc)]);
        }
    }
}

/// <summary>Coordinate WGS84 opzionali di una tappa.</summary>
#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed class UpdateTripPositionRequest
{
    /// <summary>Latitudine in [-90, 90].</summary>
    [Required, Range(typeof(decimal), "-90", "90")]
    public decimal? Latitude { get; init; }

    /// <summary>Longitudine in [-180, 180].</summary>
    [Required, Range(typeof(decimal), "-180", "180")]
    public decimal? Longitude { get; init; }
}
