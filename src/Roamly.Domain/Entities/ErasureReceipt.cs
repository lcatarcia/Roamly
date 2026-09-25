namespace Roamly.Domain.Entities;

/// <summary>
/// Prova che una cancellazione e' avvenuta (CONTEXT.md §2.2, ADR-0004).
/// <b>Non e' owned e non ha alcuna FK</b>: una ricevuta cancellata insieme all'utente
/// sarebbe inutile. Non implementa <c>IOwnedResource</c> e non riceve il query filter.
/// </summary>
public class ErasureReceipt
{
    /// <summary>Chiave primaria semplice: questa entita' non ha <c>OwnerId</c>.</summary>
    public Guid Id { get; set; }

    /// <summary>Hash dell'identificativo del soggetto: pseudonimizzazione, non anonimizzazione.</summary>
    public string SubjectHash { get; set; } = string.Empty;

    /// <summary>Quando la cancellazione e' stata richiesta.</summary>
    public DateTime RequestedAtUtc { get; set; }

    /// <summary>Quando la cancellazione e' stata eseguita.</summary>
    public DateTime ErasedAtUtc { get; set; }

    /// <summary>Versione dello schema al momento della cancellazione.</summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>Conteggi diagnostici per tabella, serializzati in JSON.</summary>
    public string DeletedRowCounts { get; set; } = string.Empty;

    /// <summary>Conferma della bonifica dello storage degli oggetti binari.</summary>
    public bool StorageSweepConfirmed { get; set; }
}
