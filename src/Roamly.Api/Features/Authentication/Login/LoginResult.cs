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

    /// <summary>Soglia di tentativi per-IP superata; è l'unico esito login che restituisce 429.</summary>
    TooManyAttempts,

    /// <summary>Indirizzo client o contatori SQL non verificabili; il login fallisce chiuso.</summary>
    Unavailable,
}

/// <summary>Profilo minimo restituito da un login riuscito.</summary>
/// <param name="Id">Identificativo utente.</param>
/// <param name="Email">Email.</param>
/// <param name="DisplayName">Nome visualizzato, se impostato.</param>
/// <param name="SessionMode">Presente solo quando il login emette una sessione limitata.</param>
public sealed record LoginProfile(
    Guid Id,
    string Email,
    string? DisplayName,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? SessionMode = null);

/// <summary>Esito tipizzato dell'handler di login.</summary>
/// <param name="Outcome">Esito.</param>
/// <param name="Profile">Popolato solo su <see cref="LoginOutcome.Success"/>.</param>
internal sealed record LoginResult(LoginOutcome Outcome, LoginProfile? Profile);
