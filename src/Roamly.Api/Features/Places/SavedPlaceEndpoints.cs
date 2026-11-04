using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;

namespace Roamly.Api.Features.Places;

/// <summary>Endpoint owner-scoped di creazione e consultazione dei luoghi salvati.</summary>
public static class SavedPlaceEndpoints
{
    private const string NotFoundProblemType = "https://roamly.dev/problems/not-found";
    private const string ContentConflictProblemType = "https://roamly.dev/problems/saved-place-id-content-conflict";
    private const string InvalidCursorProblemType = "https://roamly.dev/problems/invalid-cursor";
    private const string ConcurrencyProblemType = "https://roamly.dev/problems/concurrency-precondition";
    private const string ReferencedProblemType = "https://roamly.dev/problems/saved-place-referenced";

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
            .WithEtagResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPut("/api/v1/saved-places/{id:guid}", UpdateAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("UpdateSavedPlace")
            .WithTags("Saved places")
            .WithSummary("Sostituisce i dati di un luogo salvato, con If-Match")
            .Produces<SavedPlaceResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapDelete("/api/v1/saved-places/{id:guid}", DeleteAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("DeleteSavedPlace")
            .WithTags("Saved places")
            .WithSummary("Elimina un luogo salvato, con If-Match")
            .Produces(StatusCodes.Status204NoContent)
            .WithIfMatchOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

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
            .WithEtagResponseOpenApi()
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
            CreateSavedPlaceOutcome.Created => Created(result.Place!, context),
            CreateSavedPlaceOutcome.Replayed => Replayed(result.Place!, context),
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
        if (place is null)
        {
            return NotFound(context);
        }

        SetEtag(context, place.RowVersion);
        return Results.Ok(place.Place);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateSavedPlaceRequest request,
        UpdateSavedPlaceHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            id,
            request,
            context.Request.Headers.IfMatch.ToString() is { Length: > 0 } ifMatch ? ifMatch : null,
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            UpdateSavedPlaceOutcome.Updated => Updated(result.Place!, context),
            UpdateSavedPlaceOutcome.NotFound => NotFound(context),
            UpdateSavedPlaceOutcome.MissingPrecondition => PreconditionRequired(context),
            _ => PreconditionFailed(context),
        };
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        DeleteSavedPlaceHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var outcome = await handler.HandleAsync(
            id,
            context.Request.Headers.IfMatch.ToString() is { Length: > 0 } ifMatch ? ifMatch : null,
            cancellationToken).ConfigureAwait(false);
        return outcome switch
        {
            DeleteSavedPlaceOutcome.Deleted => Results.NoContent(),
            DeleteSavedPlaceOutcome.NotFound => NotFound(context),
            DeleteSavedPlaceOutcome.MissingPrecondition => PreconditionRequired(context),
            DeleteSavedPlaceOutcome.Referenced => Referenced(context),
            _ => PreconditionFailed(context),
        };
    }

    private static IResult Created(SavedPlaceSnapshot place, HttpContext context)
    {
        SetEtag(context, place.RowVersion);
        return Results.Created($"/api/v1/saved-places/{place.Place.Id:D}", place.Place);
    }

    private static IResult Replayed(SavedPlaceSnapshot place, HttpContext context)
    {
        SetEtag(context, place.RowVersion);
        return Results.Ok(place.Place);
    }

    private static IResult Updated(SavedPlaceSnapshot place, HttpContext context)
    {
        SetEtag(context, place.RowVersion);
        return Results.Ok(place.Place);
    }

    private static void SetEtag(HttpContext context, byte[]? rowVersion)
    {
        if (RowVersionEtag.Format(rowVersion) is { } etag)
        {
            context.Response.Headers.ETag = etag;
        }
    }

    private static IResult NotFound(HttpContext context)
    {
        return Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status404NotFound,
            "Risorsa non trovata.",
            NotFoundProblemType));
    }

    private static IResult Referenced(HttpContext context)
    {
        return Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status409Conflict,
            "Il luogo e' usato in una tappa di viaggio e non puo' essere eliminato.",
            ReferencedProblemType));
    }

    private static IResult PreconditionRequired(HttpContext context)
    {
        return Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status428PreconditionRequired,
            "La richiesta deve includere If-Match.",
            ConcurrencyProblemType));
    }

    private static IResult PreconditionFailed(HttpContext context)
    {
        return Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status412PreconditionFailed,
            "Il luogo e' cambiato. Ricarica i dati e riprova.",
            ConcurrencyProblemType));
    }
}
