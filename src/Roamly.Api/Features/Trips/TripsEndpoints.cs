using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;
using Roamly.Api.Features.Trips.CreateTrip;
using Roamly.Api.Features.Trips.GetTrip;
using Roamly.Api.Features.Trips.ListTrips;
using Roamly.Api.Features.Trips.TransitionTrip;
using Roamly.Api.Features.Trips.UpdateTrip;

namespace Roamly.Api.Features.Trips;

/// <summary>Endpoint owner-scoped per la creazione e la lettura dei viaggi.</summary>
public static class TripsEndpoints
{
    private const string ContentConflictProblemType = "https://roamly.dev/problems/trip-id-content-conflict";
    private const string NotFoundProblemType = "https://roamly.dev/problems/not-found";
    private const string InvalidCursorProblemType = "https://roamly.dev/problems/invalid-cursor";
    private const string ConcurrencyProblemType = "https://roamly.dev/problems/concurrency-precondition";
    private const string ReferencedStopProblemType = "https://roamly.dev/problems/trip-stop-referenced";
    private const string InvalidTransitionProblemType = "https://roamly.dev/problems/invalid-trip-transition";

    /// <summary>Registra le operazioni HTTP previste per i viaggi.</summary>
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/v1/campers/{camperId:guid}/trips", CreateTripAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithMetadata(new IdempotencyPolicy(IdempotencyMode.IdentityBased))
            .WithName("CreateTrip")
            .WithTags("Trips")
            .WithSummary("Crea un viaggio con le sue tappe")
            .Produces<TripDetailsResponse>(StatusCodes.Status201Created)
            .Produces<TripDetailsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/v1/trips/{tripId:guid}", GetTripAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("GetTrip")
            .WithTags("Trips")
            .WithSummary("Legge il dettaglio del viaggio e delle tappe")
            .Produces<TripDetailsResponse>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/api/v1/trips/{tripId:guid}", UpdateTripAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("UpdateTrip")
            .WithTags("Trips")
            .WithSummary("Sostituisce il viaggio e le sue tappe")
            .Produces<TripDetailsResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapPost("/api/v1/trips/{tripId:guid}/transitions", TransitionTripAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithMetadata(new IdempotencyPolicy(IdempotencyMode.NotRequired))
            .WithName("TransitionTrip")
            .WithTags("Trips")
            .WithSummary("Transisce esplicitamente lo stato del viaggio")
            .Produces<TripDetailsResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapGet("/api/v1/trips", ListTripsAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("ListTrips")
            .WithTags("Trips")
            .WithSummary("Elenca i viaggi dell'utente corrente")
            .Produces<Page<ListTripsResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .WithProblemDetailsOrValidationOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Location del POST mantiene la forma della risorsa figlia e rimane direttamente leggibile.
        app.MapGet("/api/v1/campers/{camperId:guid}/trips/{tripId:guid}", GetTripForCamperAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("GetCamperTrip")
            .WithTags("Trips")
            .WithSummary("Legge un viaggio del camper")
            .Produces<TripDetailsResponse>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateTripAsync(
        Guid camperId,
        CreateTripRequest request,
        CreateTripHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(camperId, request, cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            CreateTripOutcome.Created => Results.Created(
                $"/api/v1/campers/{camperId:D}/trips/{result.Trip!.Id:D}",
                result.Trip),
            CreateTripOutcome.Replayed => Results.Ok(result.Trip),
            CreateTripOutcome.NotFound => NotFound(context),
            _ => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status409Conflict,
                "L'identificativo e' gia' associato a un viaggio con contenuto diverso.",
                ContentConflictProblemType)),
        };
    }

    private static async Task<IResult> GetTripAsync(
        Guid tripId,
        GetTripHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var trip = await handler.HandleAsync(tripId, null, cancellationToken).ConfigureAwait(false);
        if (trip is null)
        {
            return NotFound(context);
        }

        SetEtag(context, trip.RowVersion);
        return Results.Ok(trip.Trip);
    }

    private static async Task<IResult> UpdateTripAsync(
        Guid tripId,
        UpdateTripRequest request,
        UpdateTripHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId,
            request,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return ToHttpResult(result, context);
    }

    private static async Task<IResult> TransitionTripAsync(
        Guid tripId,
        TransitionTripRequest request,
        TransitionTripHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId,
            request,
            context.Request.Headers.TryGetValue("If-Match", out var ifMatch)
                ? ifMatch.ToString()
                : null,
            cancellationToken).ConfigureAwait(false);
        return ToHttpResult(result, context);
    }

    private static IResult ToHttpResult(TransitionTripResult result, HttpContext context)
    {
        return result.Outcome switch
        {
            TransitionTripOutcome.Transitioned => Transitioned(result, context),
            TransitionTripOutcome.NotFound => NotFound(context),
            TransitionTripOutcome.MissingPrecondition => PreconditionRequired(context),
            TransitionTripOutcome.PreconditionFailed => PreconditionFailed(context),
            _ => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status409Conflict,
                "La transizione di stato del viaggio non e' consentita.",
                InvalidTransitionProblemType)),
        };
    }

    private static IResult Transitioned(TransitionTripResult result, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Trip);
    }

    private static IResult ToHttpResult(UpdateTripResult result, HttpContext context)
    {
        return result.Outcome switch
        {
            UpdateTripOutcome.Updated => Updated(result, context),
            UpdateTripOutcome.NotFound => NotFound(context),
            UpdateTripOutcome.MissingPrecondition => PreconditionRequired(context),
            UpdateTripOutcome.InvalidStopIds => Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["stops"] = ["Ogni ID di tappa deve appartenere a questo viaggio."],
                }),
            UpdateTripOutcome.ReferencedStopConflict => ReferencedStopConflict(context),
            _ => PreconditionFailed(context),
        };
    }

    private static IResult Updated(UpdateTripResult result, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Trip);
    }

    private static void SetEtag(HttpContext context, byte[]? rowVersion)
    {
        var etag = RowVersionEtag.Format(rowVersion);
        if (etag is not null)
        {
            context.Response.Headers.ETag = etag;
        }
    }

    private static async Task<IResult> ListTripsAsync(
        [AsParameters] ListTripsRequest request,
        ListTripsHandler handler,
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

    private static async Task<IResult> GetTripForCamperAsync(
        Guid camperId,
        Guid tripId,
        GetTripHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var trip = await handler.HandleAsync(tripId, camperId, cancellationToken).ConfigureAwait(false);
        if (trip is null)
        {
            return NotFound(context);
        }

        SetEtag(context, trip.RowVersion);
        return Results.Ok(trip.Trip);
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
            "Il viaggio e' cambiato. Ricarica i dati e riprova.",
            ConcurrencyProblemType));
    }

    private static IResult ReferencedStopConflict(HttpContext context)
    {
        return Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status409Conflict,
            "Una tappa non puo' essere rimossa perche' e' riferita al diario.",
            ReferencedStopProblemType));
    }

    private static IResult NotFound(HttpContext context)
    {
        return Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status404NotFound,
            "Risorsa non trovata.",
            NotFoundProblemType));
    }
}
