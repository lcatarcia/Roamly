namespace Roamly.Common;

/// <summary>
/// Identita' dell'utente corrente. Alimenta il query filter nominato "OwnerScope" (R2, ADR-0003).
/// <para>
/// Due soli membri di lettura, deliberatamente. Non c'e' un terzo membro "IsSystem": un'identita'
/// di sistema (per i job futuri della Phase 1, ADR-0004) non e' un'identita' corrente ma un modo
/// diverso di eseguire codice, e vivra' su un tipo separato (un futuro <c>ICurrentUserScope</c> con
/// <c>RunAsUser</c>/<c>RunAsSystem</c>) quando i job esisteranno. Tenerla fuori di qui e' cio' che
/// permette a <see cref="Id"/> di restare una domanda con una sola risposta valida: l'utente
/// autenticato, o un'eccezione.
/// </para>
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Identificativo dell'utente autenticato. Lancia se <see cref="IsAuthenticated"/> e' <c>false</c>:
    /// non esiste un valore di ripiego sicuro (R6, fail-closed) — <c>Guid.Empty</c> e' anche il
    /// <c>default</c> di <c>OwnerId</c>, quindi userlo come "nessuno" renderebbe silenziosamente
    /// vuoto anche il presidio di <c>SaveChangesInterceptor</c> sullo stato <c>Added</c> (R3).
    /// </summary>
    Guid Id { get; }

    /// <summary>Se esiste un'identita' autenticata leggibile da <see cref="Id"/>.</summary>
    bool IsAuthenticated { get; }
}
