using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;

namespace Roamly.Api.Features.Identity.Consents;

internal static class ConsentEndpointMappings
{
    public static void MapConsentRegistry(IEndpointRouteBuilder app)
    {
        MapTransition(app, "grants", "grant", "GrantConsent");
        MapTransition(app, "revocations", "revoke", "RevokeConsent");
        app.MapGet("/api/v1/me/consents/{purpose}", ReadAsync)
        .RequireAuthorization()
        .WithRateLimitPolicy("None")
        .WithName("GetConsent")
        .WithTags("Privacy")
        .WithSummary("Legge lo stato locale e la definizione corrente della finalita'")
        .Produces<ConsentStateResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ReadAsync(
        string purpose, GetConsentHandler handler, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var response = await handler.HandleAsync(purpose, cancellationToken).ConfigureAwait(false);
        return response is null
            ? Problem(context, 404, "not-found", "Risorsa non trovata.")
            : Results.Ok(response);
    }

    private static void MapTransition(IEndpointRouteBuilder app, string path, string action, string name)
    {
        var endpoint = app.MapPost("/api/v1/me/consents/{purpose}/" + path, async (
            string purpose, RecordConsentRequest request, RecordConsentHandler handler,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = await handler.HandleAsync(purpose, action, request, cancellationToken).ConfigureAwait(false);
            return ToHttpResult(context, result);
        });
        DescribeTransition(endpoint, name);
    }

    private static void DescribeTransition(RouteHandlerBuilder endpoint, string name) =>
        endpoint
        .RequireAuthorization()
        .WithRateLimitPolicy("PerAccount")
        .WithMetadata(new IdempotencyPolicy(IdempotencyMode.IdentityBased))
        .WithName(name)
        .WithTags("Privacy")
        .WithSummary("Registra una transizione locale; ricevuta storica, non autorizzazione Maps")
        .Produces<ConsentReceiptResponse>(StatusCodes.Status201Created)
        .Produces<ConsentReceiptResponse>()
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static IResult ToHttpResult(HttpContext context, RecordConsentResult result) => result.Outcome switch
    {
        RecordConsentOutcome.Created => Results.Json(result.Receipt, statusCode: StatusCodes.Status201Created),
        RecordConsentOutcome.Replayed => Results.Ok(result.Receipt),
        RecordConsentOutcome.NotFound => Problem(context, 404, "not-found", "Risorsa non trovata."),
        RecordConsentOutcome.AccountRestricted => Problem(context, 403, "account-restricted",
            "L'account non consente questa operazione."),
        RecordConsentOutcome.Inactive => Problem(context, 409, "consent-purpose-inactive",
            "La finalita' e' DRAFT/inattiva; nessun consenso effettivo viene raccolto."),
        RecordConsentOutcome.DefinitionChanged => Problem(context, 409, "consent-definition-changed",
            "Versione o testo non correnti. Rileggi la definizione."),
        _ => Problem(context, 409, "idempotency-conflict", "Identificativo gia' usato con contenuto diverso."),
    };

    private static IResult Problem(HttpContext context, int status, string type, string title) =>
        Results.Problem(ProblemFactory.Create(context, status, title, "https://roamly.dev/problems/" + type));
}
