using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;

namespace Roamly.Api.Features.Places;

/// <summary>Endpoint owner-scoped di creazione e consultazione dei luoghi salvati.</summary>
public static class SavedPlaceEndpoints
{
    private const string NotFoundProblemType = "https://roamly.dev/problems/not-found";
    private const string ContentConflictProblemType = "https://roamly.dev/problems/saved-place-id-content-conflict";
    private const string InvalidCursorProblemType = "https://roamly.dev/problems/invalid-cursor";

    /// <summary>Registra gli endpoint della raccolta personale di luoghi.</summary>
    public static IEndpointRouteBuilder MapSavedPlaceEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/v1/saved-places", CreateAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithMetadata(new IdempotencyPolicy(IdempotencyMode.IdentityBased))
            .WithName("CreateSavedPlace")
            .WithTags("Saved places")
            .WithSummary("Salva un luogo con dati inseriti manualmente")
            .Produces<SavedPlaceResponse>(StatusCodes.Status201Created)
            .Produces<SavedPlaceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/v1/saved-places", ListAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("ListSavedPlaces")
            .WithTags("Saved places")
            .WithSummary("Elenca i luoghi salvati dall'utente corrente")
            .Produces<Page<SavedPlaceResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithProblemDetailsOrValidationOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/v1/saved-places/{id:guid}", GetAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("GetSavedPlace")
            .WithTags("Saved places")
            .WithSummary("Legge un luogo salvato dell'utente corrente")
            .Produces<SavedPlaceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateSavedPlaceRequest request,
        CreateSavedPlaceHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            CreateSavedPlaceOutcome.Created => Results.Created(
                $"/api/v1/saved-places/{result.Place!.Id:D}",
                result.Place),
            CreateSavedPlaceOutcome.Replayed => Results.Ok(result.Place),
            _ => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status409Conflict,
                "L'identificativo e' gia' associato a un luogo con contenuto diverso.",
                ContentConflictProblemType)),
        };
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] ListSavedPlacesRequest request,
        ListSavedPlacesHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var page = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
        return page is null
            ? Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status400BadRequest,
                "Il cursore non e' valido.",
                InvalidCursorProblemType))
            : Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        GetSavedPlaceHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var place = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
        return place is null
            ? Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status404NotFound,
                "Risorsa non trovata.",
                NotFoundProblemType))
            : Results.Ok(place);
    }
}
