namespace Roamly.Api.Features.Campers.ParseCamperManual;

/// <summary>Esito dell'analisi di un manuale tecnico o scheda esterna del camper.</summary>
public enum ParseCamperManualOutcome
{
    /// <summary>Analisi completata con successo.</summary>
    Parsed = 0,

    /// <summary>Il camper indicato non esiste o appartiene a un altro proprietario.</summary>
    NotFound = 1,

    /// <summary>La fonte fornita non è valida, non è raggiungibile o non contiene testo estraibile.</summary>
    UnprocessableSource = 2,
}

/// <summary>Risultato di <see cref="ParseCamperManualHandler"/>.</summary>
public sealed record ParseCamperManualResult(
    ParseCamperManualOutcome Outcome,
    CamperManualParseResponse? Response = null,
    string? ErrorDetail = null);
