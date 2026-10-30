using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;
using Roamly.Api.Features.Identity.AccountExport;
using Roamly.Api.Features.Identity.AccountDeletion;
using Roamly.Api.Features.Identity.GetMe;

namespace Roamly.Api.Features.Identity;

/// <summary>Un solo <c>Map*Endpoints</c> per modulo (R54): registra gli endpoint di <c>Features/Identity/</c>.</summary>
public static class IdentityEndpoints
{
    private const string NotFoundProblemType = "https://roamly.dev/problems/not-found";
    private const string InvalidPasswordProblemType = "https://roamly.dev/problems/invalid-reauthentication";
    private const string TooManyAttemptsProblemType = "https://roamly.dev/problems/too-many-attempts";
    private const string AccountStateChangedProblemType = "https://roamly.dev/problems/account-state-changed";
    private const string RecentPasswordRequiredProblemType = "https://roamly.dev/problems/recent-password-required";

    /// <summary>Registra <c>GET /api/v1/me</c>.</summary>
    /// <param name="app">Route builder dell'applicazione.</param>
    /// <returns>Il route builder, per concatenare le altre registrazioni.</returns>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/me", async (GetMeHandler handler, HttpContext context) =>
        {
            var response = await handler.HandleAsync().ConfigureAwait(false);

            return response is null
                ? Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status404NotFound,
                    "Risorsa non trovata.",
                    NotFoundProblemType))
                : Results.Ok(response);
        })
        .RequireAuthorization()
        .WithMetadata(new AllowPasswordChangeSessionAttribute())
        .WithRateLimitPolicy("None")
        .Produces<GetMeResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/me/export", (AccountExportHandler handler, HttpContext context) =>
        {
            var preparation = handler.Prepare(context.User);
            context.Response.Headers.CacheControl = "no-store";

            return preparation.Outcome switch
            {
                AccountExportOutcome.Ready => preparation.Result!,
                AccountExportOutcome.PasswordAuthenticationRequired => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "Per scaricare i dati, accedi di nuovo con la password.",
                    RecentPasswordRequiredProblemType)),
                _ => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status429TooManyRequests,
                    "Troppi tentativi. Riprova piu' tardi.",
                    TooManyAttemptsProblemType)),
            };
        })
        .RequireAuthorization()
        .WithRateLimitPolicy("PerAccount")
        .WithName("ExportAccountData")
        .WithTags("Privacy")
        .WithSummary("Scarica i dati personali in un archivio ZIP")
        .Produces<Stream>(StatusCodes.Status200OK, contentType: "application/zip")
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        app.MapGet("/api/v1/me/deletion/preview", async (
            GetAccountDeletionPreviewHandler handler,
            CancellationToken cancellationToken) =>
        {
            var response = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithRateLimitPolicy("None")
        .WithName("PreviewAccountDeletion")
        .WithTags("Privacy")
        .WithSummary("Mostra conteggi owned informativi prima della richiesta di cancellazione")
        .Produces<AccountDeletionPreviewResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapPost("/api/v1/me/deletion", async (
            RequestAccountDeletionRequest request,
            RequestAccountDeletionHandler handler,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);

            return result.Outcome switch
            {
                RequestAccountDeletionOutcome.Scheduled => Results.Ok(result.Response),
                RequestAccountDeletionOutcome.InvalidPassword => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "Autenticazione richiesta.",
                    InvalidPasswordProblemType)),
                RequestAccountDeletionOutcome.TooManyAttempts => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status429TooManyRequests,
                    "Troppi tentativi. Riprova piu' tardi.",
                    TooManyAttemptsProblemType)),
                RequestAccountDeletionOutcome.AccountNotFound => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status404NotFound,
                    "Risorsa non trovata.",
                    NotFoundProblemType)),
                _ => Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status409Conflict,
                    "Lo stato dell'account e' cambiato. Riprova.",
                    AccountStateChangedProblemType)),
            };
        })
        .RequireAuthorization()
        .WithRateLimitPolicy("PerAccount")
        .WithMetadata(new IdempotencyPolicy(IdempotencyMode.NotRequired))
        .WithName("RequestAccountDeletion")
        .WithTags("Privacy")
        .WithSummary("Richiede la cancellazione dell'account con 30 giorni di grazia")
        .Produces<AccountDeletionResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        app.MapDelete("/api/v1/me/deletion", async (
            RevokeAccountDeletionHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithRateLimitPolicy("None")
        .WithMetadata(new AllowRestrictedAccountSessionAttribute())
        .WithMetadata(new IdempotencyPolicy(IdempotencyMode.NotRequired))
        .WithName("RevokeAccountDeletion")
        .WithTags("Privacy")
        .WithSummary("Revoca la richiesta di cancellazione in modo idempotente")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapGet("/api/v1/me/deletion", async (
            GetAccountDeletionStatusHandler handler,
            CancellationToken cancellationToken) =>
        {
            var response = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            return response is null ? Results.NoContent() : Results.Ok(response);
        })
        .RequireAuthorization()
        .WithRateLimitPolicy("None")
        .WithMetadata(new AllowRestrictedAccountSessionAttribute())
        .WithName("GetAccountDeletionStatus")
        .WithTags("Privacy")
        .WithSummary("Legge solo stato e date della richiesta di cancellazione")
        .Produces<AccountDeletionStatusResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
