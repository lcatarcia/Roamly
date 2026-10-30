namespace Roamly.Api.Features.Campers.RecordMaintenanceLog;

/// <summary>Esito del POST immutabile di un intervento.</summary>
public sealed record RecordMaintenanceLogResult(
    RecordMaintenanceLogOutcome Outcome,
    RecordMaintenanceLogResponse? Log);

/// <summary>Esito HTTP tradotto dall'endpoint.</summary>
public enum RecordMaintenanceLogOutcome
{
    /// <summary>Log creato.</summary>
    Created,

    /// <summary>Retry equivalente dello stesso log.</summary>
    Replayed,

    /// <summary>ID riutilizzato con contenuto diverso.</summary>
    Conflict,

    /// <summary>Lettura giornaliera esistente con chilometraggio discordante.</summary>
    OdometerConflict,

    /// <summary>Scadenza derivata non rappresentabile nei tipi supportati.</summary>
    ProjectionOutOfRange,

    /// <summary>Conflitto RowVersion persistente anche dopo un retry.</summary>
    ConcurrencyConflict,

    /// <summary>Camper o manutenzione assenti o non posseduti.</summary>
    NotFound,
}
