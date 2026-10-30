namespace Roamly.Common.Security;

/// <summary>Risultato del controllo dei limiti di login.</summary>
public enum LoginAttemptLimitStatus
{
    /// <summary>Richiesta autorizzata a procedere con la verifica delle credenziali.</summary>
    Allowed,

    /// <summary>Il limite per-IP e' stato raggiunto; il contratto HTTP risponde 429.</summary>
    IpTooManyAttempts,

    /// <summary>Il limite per-account e' stato raggiunto; il contratto resta 401 generico.</summary>
    AccountTooManyAttempts,

    /// <summary>Lo stato condiviso non e' verificabile; il login deve fallire chiuso.</summary>
    Unavailable,
}

/// <summary>
/// Riserva un tentativo di login prima di verificare la password. Gli errori di credenziale
/// mantengono la prenotazione; una credenziale valida la rimuove.
/// </summary>
public interface ILoginAttemptRateLimiter
{
    /// <summary>Verifica cap e soglie IP prima di interrogare Identity.</summary>
    /// <param name="clientIpAddress">Indirizzo IP canonico risolto dal server.</param>
    /// <param name="cancellationToken">Token di annullamento.</param>
    Task<LoginAttemptLimitStatus> CheckIpAsync(
        string clientIpAddress,
        CancellationToken cancellationToken);

    /// <summary>Applica il limite account a un utente Identity già risolto.</summary>
    /// <param name="identityUserId">ID stabile dell'utente Identity.</param>
    /// <param name="cancellationToken">Token di annullamento.</param>
    Task<LoginAttemptStartResult> BeginAccountAsync(
        Guid identityUserId,
        CancellationToken cancellationToken);
}

/// <summary>Manutenzione dei contatori scaduti nel job amministrativo one-shot esistente.</summary>
public interface ILoginAttemptCounterMaintenance
{
    /// <summary>Elimina a batch tutte le righe scadute e restituisce il numero totale eliminato.</summary>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken);
}

/// <summary>Esito e prenotazione di un tentativo login.</summary>
/// <param name="Status">Esito del controllo.</param>
/// <param name="Reservation">Prenotazione presente quando un account esistente è ammesso.</param>
public sealed record LoginAttemptStartResult(
    LoginAttemptLimitStatus Status,
    ILoginAttemptReservation? Reservation = null);

/// <summary>Conclude la prenotazione mantenendo solo gli errori di credenziale.</summary>
public interface ILoginAttemptReservation : IAsyncDisposable
{
    /// <summary>
    /// Finalizza il tentativo. Restituisce <see langword="false"/> se SQL non ha confermato
    /// l'aggiornamento; in tal caso il chiamante non deve emettere una sessione.
    /// </summary>
    /// <param name="credentialsInvalid">Vero se account assente o credenziali errate.</param>
    /// <param name="cancellationToken">Token di annullamento.</param>
    Task<bool> CompleteAsync(bool credentialsInvalid, CancellationToken cancellationToken);
}
