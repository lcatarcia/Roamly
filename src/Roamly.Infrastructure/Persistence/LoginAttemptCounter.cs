namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Contatore infrastrutturale a finestra fissa. La chiave e' un HMAC, mai l'account o l'IP
/// originali; l'entita' non e' owned e non appartiene al grafo di export/cancellazione.
/// </summary>
public sealed class LoginAttemptCounter
{
    /// <summary>HMAC-SHA-256 esadecimale dell'ambito e dell'identificativo.</summary>
    public string BucketKey { get; set; } = string.Empty;

    /// <summary>Tipo funzionale del contatore o del marker temporaneo di ammissione IP.</summary>
    public string CounterKind { get; set; } = string.Empty;

    /// <summary>Inizio UTC della finestra fissa.</summary>
    public DateTime WindowStartUtc { get; set; }

    /// <summary>Numero di tentativi nella finestra.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Scadenza dopo la quale la riga puo' essere eliminata.</summary>
    public DateTime ExpiresAtUtc { get; set; }
}
