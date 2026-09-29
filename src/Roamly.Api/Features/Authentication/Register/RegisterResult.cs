namespace Roamly.Api.Features.Authentication.Register;

/// <summary>Esito di <see cref="RegisterHandler.HandleAsync"/>.</summary>
internal enum RegisterOutcome
{
    /// <summary>
    /// Richiesta accettata. Copre sia la creazione riuscita sia il caso "email gia' registrata":
    /// i due casi **non sono distinguibili dall'esterno** (nessun oracolo sull'esistenza
    /// dell'account, stesso principio del 404 di ownership in <c>API-CONVENTIONS.md</c>).
    /// </summary>
    Accepted,

    /// <summary>La password non rispetta la policy di Identity. Non leak sull'esistenza dell'account: viene valutata prima di cercare l'utente.</summary>
    PasswordRejected,
}

/// <summary>Esito tipizzato dell'handler di registrazione.</summary>
/// <param name="Outcome">Esito.</param>
/// <param name="PasswordErrors">Errori di policy password, popolati solo se <see cref="RegisterOutcome.PasswordRejected"/>.</param>
internal sealed record RegisterResult(RegisterOutcome Outcome, IReadOnlyList<string> PasswordErrors);
