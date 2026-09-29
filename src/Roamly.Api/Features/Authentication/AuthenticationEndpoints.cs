using Microsoft.AspNetCore.Antiforgery;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;
using Roamly.Api.Features.Authentication.ConfirmEmail;
using Roamly.Api.Features.Authentication.Login;
using Roamly.Api.Features.Authentication.Logout;
using Roamly.Api.Features.Authentication.Register;

namespace Roamly.Api.Features.Authentication;

/// <summary>
/// Un solo <c>Map*Endpoints</c> per modulo (R54): registra tutti gli endpoint di
/// <c>Features/Authentication/</c>. Ogni endpoint dichiara esplicitamente la sua politica di
/// rate limiting (R61) — <c>"None"</c> compreso — perche' un'omissione sia un errore visibile,
/// non un default implicito.
/// </summary>
public static class AuthenticationEndpoints
{
    private const string InvalidCredentialsProblemType = "https://roamly.dev/problems/invalid-credentials";
    private const string TooManyAttemptsProblemType = "https://roamly.dev/problems/too-many-attempts";
    private const string InvalidConfirmationLinkProblemType = "https://roamly.dev/problems/invalid-confirmation-link";

    /// <summary>Registra gli endpoint di autenticazione sotto <c>/api/v1</c>.</summary>
    /// <param name="app">Route builder dell'applicazione.</param>
    /// <returns>Il route builder, per concatenare le altre registrazioni.</returns>
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/csrf-token", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        })
        .AllowAnonymous()
        .WithRateLimitPolicy("None");

        var auth = app.MapGroup("/api/v1/auth");

        auth.MapPost("/register", async (RegisterRequest request, RegisterHandler handler, CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);

            return result.Outcome == RegisterOutcome.PasswordRejected
                ? Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = [.. result.PasswordErrors] })
                : Results.Accepted(value: new { message = "Se l'indirizzo non e' gia' registrato, riceverai un'email di conferma." });
        })
        .AllowAnonymous()
        // Nessuna chiave per-account praticabile prima che l'account esista; il rate limiting per
        // IP resta fuori scope finche' la decisione #9 (deploy, ForwardedHeaders) non e' chiusa.
        .WithRateLimitPolicy("None");

        auth.MapPost("/email/confirm", async (ConfirmEmailRequest request, ConfirmEmailHandler handler, HttpContext context) =>
        {
            var outcome = await handler.HandleAsync(request).ConfigureAwait(false);

            return outcome == ConfirmEmailOutcome.Confirmed
                ? Results.Ok(new { message = "Email confermata." })
                : Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Link di conferma non valido o scaduto.",
                    InvalidConfirmationLinkProblemType));
        })
        .AllowAnonymous()
        .WithRateLimitPolicy("None");

        auth.MapPost("/login", async (LoginRequest request, LoginHandler handler, HttpContext context) =>
        {
            var result = await handler.HandleAsync(request).ConfigureAwait(false);

            return result.Outcome switch
            {
                LoginOutcome.Success => Results.Ok(result.Profile),
                LoginOutcome.TooManyAttempts => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status429TooManyRequests,
                    "Troppi tentativi di login. Riprova piu' tardi.",
                    TooManyAttemptsProblemType)),
                _ => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "Credenziali non valide.",
                    InvalidCredentialsProblemType)),
            };
        })
        .AllowAnonymous()
        .WithRateLimitPolicy("PerAccount");

        auth.MapPost("/logout", async (LogoutHandler handler) =>
        {
            await handler.HandleAsync().ConfigureAwait(false);
            return Results.NoContent();
        })
        // Idempotente di proposito: disconnettersi quando gia' disconnessi non e' un errore.
        .AllowAnonymous()
        .WithRateLimitPolicy("None");

        return app;
    }
}
