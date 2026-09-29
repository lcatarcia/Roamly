namespace Roamly.Api.Features.Authentication.Login;

/// <summary>Esito di <see cref="LoginHandler.HandleAsync"/>.</summary>
internal enum LoginOutcome
{
    /// <summary>Credenziali valide, cookie emesso.</summary>
    Success,

    /// <summary>
    /// Credenziali non valide, in qualunque forma: utente inesistente, password sbagliata, email
    /// non confermata o account bloccato. Unificati deliberatamente — distinguerli sarebbe un
    /// oracolo sullo stato dell'account (stesso principio del 404 di ownership).
    /// </summary>
    InvalidCredentials,

    /// <summary>Soglia di tentativi per-account superata (SECURITY.md §6).</summary>
    TooManyAttempts,
}

/// <summary>Profilo minimo restituito da un login riuscito.</summary>
/// <param name="Id">Identificativo utente.</param>
/// <param name="Email">Email.</param>
/// <param name="DisplayName">Nome visualizzato, se impostato.</param>
public sealed record LoginProfile(Guid Id, string Email, string? DisplayName);

/// <summary>Esito tipizzato dell'handler di login.</summary>
/// <param name="Outcome">Esito.</param>
/// <param name="Profile">Popolato solo su <see cref="LoginOutcome.Success"/>.</param>
internal sealed record LoginResult(LoginOutcome Outcome, LoginProfile? Profile);
