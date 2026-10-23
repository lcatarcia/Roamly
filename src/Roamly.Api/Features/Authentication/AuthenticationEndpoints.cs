using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;
using Roamly.Api.Features.Authentication.ConfirmEmail;
using Roamly.Api.Features.Authentication.ChangePassword;
using Roamly.Api.Features.Authentication.Login;
using Roamly.Api.Features.Authentication.Logout;
using Roamly.Api.Features.Authentication.Register;
using Roamly.Api.Features.Authentication.ResendEmailConfirmation;

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
    private const string InvalidReauthenticationProblemType = "https://roamly.dev/problems/invalid-reauthentication";

    /// <summary>Registra gli endpoint di autenticazione sotto <c>/api/v1</c>.</summary>
    /// <param name="app">Route builder dell'applicazione.</param>
    /// <returns>Il route builder, per concatenare le altre registrazioni.</returns>
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/csrf-token", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new CsrfTokenResponse(tokens.RequestToken));
        })
        .AllowAnonymous()
        .WithRateLimitPolicy("None")
        .Produces<CsrfTokenResponse>(StatusCodes.Status200OK);

        var auth = app.MapGroup("/api/v1/auth");

        if (app.ServiceProvider.GetService<IWebHostEnvironment>()?.IsDevelopment() is true)
        {
            auth.MapPost(
                "/email/resend-confirmation",
                async (
                    ResendEmailConfirmationRequest request,
                    ResendEmailConfirmationHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                    return Results.Accepted(value: new AuthenticationMessageResponse(
                        "Se la richiesta e' valida, riceverai un'email di conferma."));
                })
            .AllowAnonymous()
            .WithRateLimitPolicy("None")
            .WithMetadata(new IdempotencyPolicy(IdempotencyMode.NotRequired))
            .Produces<AuthenticationMessageResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);
        }

        // Blocco temporaneo: LoggingEmailSender non recapita l'email e registra il token nel body.
        if (app.ServiceProvider.GetService<IWebHostEnvironment>()?.IsDevelopment() is true)
        {
            auth.MapPost("/register", async (RegisterRequest request, RegisterHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);

                return result.Outcome == RegisterOutcome.PasswordRejected
                    ? Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = [.. result.PasswordErrors] })
                    : Results.Accepted(value: new AuthenticationMessageResponse(
                        "Se l'indirizzo non e' gia' registrato, riceverai un'email di conferma."));
            })
            .AllowAnonymous()
            // Nessuna chiave per-account praticabile prima che l'account esista; il rate limiting per
            // IP resta fuori scope finche' la decisione #9 (deploy, ForwardedHeaders) non e' chiusa.
            .WithRateLimitPolicy("None")
            .WithMetadata(new IdempotencyPolicy(IdempotencyMode.NotRequired))
            .Produces<AuthenticationMessageResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);
        }

        auth.MapPost("/email/confirm", async (ConfirmEmailRequest request, ConfirmEmailHandler handler, HttpContext context) =>
        {
            var outcome = await handler.HandleAsync(request).ConfigureAwait(false);

            return outcome == ConfirmEmailOutcome.Confirmed
                ? Results.Ok(new AuthenticationMessageResponse("Email confermata."))
                : Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Link di conferma non valido o scaduto.",
                    InvalidConfirmationLinkProblemType));
        })
        .AllowAnonymous()
        .WithRateLimitPolicy("None")
        .WithMetadata(new IdempotencyPolicy(IdempotencyMode.NotRequired))
        .Produces<AuthenticationMessageResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        auth.MapPost("/login", async (
            LoginRequest request,
            LoginHandler handler,
            ILoginClientIpAddressProvider clientIpProvider,
            HttpContext context) =>
        {
            var clientIpAddress = clientIpProvider.GetClientIpAddress(context)?.ToString();
            var result = await handler.HandleAsync(request, clientIpAddress, context.RequestAborted)
                .ConfigureAwait(false);

            return result.Outcome switch
            {
                LoginOutcome.Success => Results.Ok(result.Profile),
                LoginOutcome.TooManyAttempts => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status429TooManyRequests,
                    "Troppi tentativi di login. Riprova piu' tardi.",
                    TooManyAttemptsProblemType)),
                LoginOutcome.Unavailable => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status503ServiceUnavailable,
                    "Login temporaneamente non disponibile.",
                    "https://roamly.dev/problems/login-unavailable")),
                _ => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "Credenziali non valide.",
                    InvalidCredentialsProblemType)),
            };
        })
        .AllowAnonymous()
        .WithRateLimitPolicy("LoginAttempt")
        .WithMetadata(new IdempotencyPolicy(IdempotencyMode.NotRequired))
        .Produces<LoginProfile>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        auth.MapPost("/password/change", async (
            ChangePasswordRequest request,
            ChangePasswordHandler handler,
            HttpContext context) =>
        {
            var result = await handler.HandleAsync(request).ConfigureAwait(false);
            return result.Outcome switch
            {
                ChangePasswordOutcome.Success => Results.NoContent(),
                ChangePasswordOutcome.InvalidCurrentPassword => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "Autenticazione richiesta.",
                    InvalidReauthenticationProblemType)),
                ChangePasswordOutcome.PasswordRejected => Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["newPassword"] = [.. result.Errors] }),
                ChangePasswordOutcome.AccountNotFound => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status404NotFound,
                    "Risorsa non trovata.",
                    "https://roamly.dev/problems/not-found")),
                _ => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "Non e' stato possibile aggiornare la password.",
                    "https://roamly.dev/problems/password-change-failed")),
            };
        })
        .RequireAuthorization()
        .WithMetadata(new AllowPasswordChangeSessionAttribute())
        .WithRateLimitPolicy("PerAccount")
        .WithMetadata(new IdempotencyPolicy(IdempotencyMode.NotRequired))
        .WithName("ChangePassword")
        .WithTags("Authentication")
        .WithSummary("Cambia la password verificando quella attuale")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        auth.MapPost("/logout", async (LogoutHandler handler) =>
        {
            await handler.HandleAsync().ConfigureAwait(false);
            return Results.NoContent();
        })
        // Idempotente di proposito: disconnettersi quando gia' disconnessi non e' un errore.
        .AllowAnonymous()
        .WithMetadata(new AllowRestrictedAccountSessionAttribute())
        .WithRateLimitPolicy("None")
        .WithMetadata(new IdempotencyPolicy(IdempotencyMode.NotRequired))
        .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}
