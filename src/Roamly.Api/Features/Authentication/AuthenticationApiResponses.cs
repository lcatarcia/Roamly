namespace Roamly.Api.Features.Authentication;

/// <summary>Messaggio di risposta dei flussi pubblici di autenticazione.</summary>
/// <param name="Message">Messaggio mostrato al client.</param>
public sealed record AuthenticationMessageResponse(string Message);

/// <summary>Token antiforgery restituito per l'header della richiesta successiva.</summary>
/// <param name="Token">Token XSRF, assente solo se non emesso dall'implementazione.</param>
public sealed record CsrfTokenResponse(string? Token);
