using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;
using Roamly.Api.Features.Campers.CreateCamper;
using Roamly.Api.Features.Campers.CreateMaintenance;
using Roamly.Api.Features.Campers.GetCamperDashboard;
using Roamly.Api.Features.Campers.GetCamperProfile;
using Roamly.Api.Features.Campers.ListMaintenance;
using Roamly.Api.Features.Campers.ListMaintenanceLogs;
using Roamly.Api.Features.Campers.ListCampers;
using Roamly.Api.Features.Campers.RecordMaintenanceLog;
using Roamly.Api.Features.Campers.UpdateCamperProfile;

namespace Roamly.Api.Features.Campers;

/// <summary>Endpoint delle risorse Camper.</summary>
public static class CampersEndpoints
{
    private const string ContentConflictProblemType = "https://roamly.dev/problems/camper-id-content-conflict";
    private const string NotFoundProblemType = "https://roamly.dev/problems/not-found";
    private const string InvalidCursorProblemType = "https://roamly.dev/problems/invalid-cursor";
    private const string MaintenanceConflictProblemType =
        "https://roamly.dev/problems/maintenance-id-content-conflict";
    private const string MaintenanceLogConflictProblemType =
        "https://roamly.dev/problems/maintenance-log-id-content-conflict";
    private const string OdometerConflictProblemType =
        "https://roamly.dev/problems/odometer-reading-conflict";
    private const string MaintenanceProjectionRangeProblemType =
        "https://roamly.dev/problems/maintenance-log-projection-out-of-range";
    private const string MaintenanceStateConflictProblemType =
        "https://roamly.dev/problems/maintenance-state-conflict";
    private const string ConcurrencyProblemType = "https://roamly.dev/problems/concurrency-precondition";

    /// <summary>Registra le operazioni esposte sui camper.</summary>
    public static IEndpointRouteBuilder MapCamperEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/campers/{camperId:guid}", GetCamperProfileAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("GetCamperProfile")
            .WithTags("Campers")
            .WithSummary("Legge il profilo tecnico completo del camper")
            .Produces<CamperProfileResponse>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/api/v1/campers/{camperId:guid}", UpdateCamperProfileAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("UpdateCamperProfile")
            .WithTags("Campers")
            .WithSummary("Aggiorna il profilo tecnico completo del camper")
            .Produces<CamperProfileResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapGet("/api/v1/campers/{camperId:guid}/dashboard", async (
            Guid camperId,
            GetCamperDashboardHandler handler,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var dashboard = await handler.HandleAsync(camperId, cancellationToken).ConfigureAwait(false);
            return dashboard is null
                ? Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status404NotFound,
                    "Risorsa non trovata.",
                    NotFoundProblemType))
                : Results.Ok(dashboard);
        })
        .RequireAuthorization()
        .WithRateLimitPolicy("None")
        .WithName("GetCamperDashboard")
        .WithTags("Campers")
        .WithSummary("Legge la dashboard del camper")
        .Produces<CamperDashboardResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/campers", async (
            [AsParameters] ListCampersRequest request,
            ListCampersHandler handler,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var page = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            return page is null
                ? Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Il cursore non e' valido.",
                    InvalidCursorProblemType))
                : Results.Ok(page);
        })
        .RequireAuthorization()
        .WithRateLimitPolicy("None")
        .WithName("ListCampers")
        .WithTags("Campers")
        .WithSummary("Elenca i camper dell'utente corrente")
        .Produces<Page<ListCampersResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .WithProblemDetailsOrValidationOpenApi()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/v1/campers", async (
            CreateCamperRequest request,
            CreateCamperHandler handler,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            return ToHttpResult(result, context);
        })
        .RequireAuthorization()
        .WithRateLimitPolicy("None")
        .WithMetadata(new IdempotencyPolicy(IdempotencyMode.IdentityBased))
        .WithName("CreateCamper")
        .WithTags("Campers")
        .WithSummary("Crea un camper")
        .Produces<CreateCamperResponse>(StatusCodes.Status201Created)
        .Produces<CreateCamperResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost(
            "/api/v1/campers/{camperId:guid}/maintenance/{maintenanceItemId:guid}/logs",
            async (
                Guid camperId,
                Guid maintenanceItemId,
                RecordMaintenanceLogRequest request,
                RecordMaintenanceLogHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    camperId,
                    maintenanceItemId,
                    request,
                    cancellationToken).ConfigureAwait(false);
                return ToHttpResult(result, camperId, maintenanceItemId, context);
            })
        .RequireAuthorization()
        .WithRateLimitPolicy("None")
        .WithMetadata(new IdempotencyPolicy(IdempotencyMode.IdentityBased))
        .WithName("RecordMaintenanceLog")
        .WithTags("Maintenance")
        .WithSummary("Registra un intervento di manutenzione")
        .Produces<RecordMaintenanceLogResponse>(StatusCodes.Status201Created)
        .Produces<RecordMaintenanceLogResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/api/v1/campers/{camperId:guid}/maintenance", async (
            Guid camperId,
            CreateMaintenanceRequest request,
            CreateMaintenanceHandler handler,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(camperId, request, cancellationToken).ConfigureAwait(false);
            return ToHttpResult(result, camperId, context);
        })
        .RequireAuthorization()
        .WithRateLimitPolicy("None")
        .WithMetadata(new IdempotencyPolicy(IdempotencyMode.IdentityBased))
        .WithName("CreateMaintenance")
        .WithTags("Maintenance")
        .WithSummary("Crea una regola di manutenzione ricorrente")
        .Produces<CreateMaintenanceResponse>(StatusCodes.Status201Created)
        .Produces<CreateMaintenanceResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/v1/campers/{camperId:guid}/maintenance", async (
            Guid camperId,
            [AsParameters] ListMaintenanceRequest request,
            ListMaintenanceHandler handler,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(camperId, request, cancellationToken)
                .ConfigureAwait(false);
            if (result.Page is not null)
            {
                return Results.Ok(result.Page);
            }

            return Results.Problem(ProblemFactory.Create(
                context,
                result.InvalidCursor ? StatusCodes.Status400BadRequest : StatusCodes.Status404NotFound,
                result.InvalidCursor ? "Il cursore non e' valido." : "Risorsa non trovata.",
                result.InvalidCursor ? InvalidCursorProblemType : NotFoundProblemType));
        })
        .RequireAuthorization()
        .WithRateLimitPolicy("None")
        .WithName("ListMaintenance")
        .WithTags("Maintenance")
        .WithSummary("Elenca le manutenzioni ricorrenti del camper")
        .Produces<Page<ListMaintenanceResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .WithProblemDetailsOrValidationOpenApi()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet(
            "/api/v1/campers/{camperId:guid}/maintenance/{maintenanceItemId:guid}/logs",
            async (
                Guid camperId,
                Guid maintenanceItemId,
                [AsParameters] ListMaintenanceLogsRequest request,
                ListMaintenanceLogsHandler handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    camperId,
                    maintenanceItemId,
                    request,
                    cancellationToken).ConfigureAwait(false);
                if (result.Page is not null)
                {
                    return Results.Ok(result.Page);
                }

                return Results.Problem(ProblemFactory.Create(
                    context,
                    result.InvalidCursor ? StatusCodes.Status400BadRequest : StatusCodes.Status404NotFound,
                    result.InvalidCursor ? "Il cursore non e' valido." : "Risorsa non trovata.",
                    result.InvalidCursor ? InvalidCursorProblemType : NotFoundProblemType));
            })
        .RequireAuthorization()
        .WithRateLimitPolicy("None")
        .WithName("ListMaintenanceLogs")
        .WithTags("Maintenance")
        .WithSummary("Legge lo storico degli interventi di manutenzione")
        .Produces<Page<ListMaintenanceLogsResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .WithProblemDetailsOrValidationOpenApi()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetCamperProfileAsync(
        Guid camperId,
        GetCamperProfileHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(camperId, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return NotFound(context);
        }

        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Profile);
    }

    private static async Task<IResult> UpdateCamperProfileAsync(
        Guid camperId,
        UpdateCamperProfileRequest request,
        UpdateCamperProfileHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            camperId,
            request,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return ToHttpResult(result, context);
    }

    private static IResult ToHttpResult(UpdateCamperProfileResult result, HttpContext context)
    {
        return result.Outcome switch
        {
            UpdateCamperProfileOutcome.Updated => ProfileUpdated(result, context),
            UpdateCamperProfileOutcome.NotFound => NotFound(context),
            UpdateCamperProfileOutcome.MissingPrecondition => PreconditionRequired(context),
            _ => PreconditionFailed(context),
        };
    }

    private static IResult ProfileUpdated(UpdateCamperProfileResult result, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Profile);
    }

    private static void SetEtag(HttpContext context, byte[]? rowVersion)
    {
        var etag = RowVersionEtag.Format(rowVersion);
        if (etag is not null)
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
            "La risorsa e' cambiata. Ricarica i dati e riprova.",
            ConcurrencyProblemType));
    }

    private static IResult ToHttpResult(CreateCamperResult result, HttpContext context)
    {
        return result.Outcome switch
        {
            CreateCamperOutcome.Created => Results.Created(
                $"/api/v1/campers/{result.Camper!.Id:D}",
                result.Camper),
            CreateCamperOutcome.Replayed => Results.Ok(result.Camper),
            _ => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status409Conflict,
                "L'identificativo e' gia' associato a un camper con contenuto diverso.",
                ContentConflictProblemType)),
        };
    }

    private static IResult ToHttpResult(
        CreateMaintenanceResult result,
        Guid camperId,
        HttpContext context)
    {
        return result.Outcome switch
        {
            CreateMaintenanceOutcome.Created => Results.Created(
                $"/api/v1/campers/{camperId:D}/maintenance/{result.Item!.Id:D}",
                result.Item),
            CreateMaintenanceOutcome.Replayed => Results.Ok(result.Item),
            CreateMaintenanceOutcome.NotFound => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status404NotFound,
                "Risorsa non trovata.",
                NotFoundProblemType)),
            _ => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status409Conflict,
                "L'identificativo e' gia' associato a una manutenzione con contenuto diverso.",
                MaintenanceConflictProblemType)),
        };
    }

    private static IResult ToHttpResult(
        RecordMaintenanceLogResult result,
        Guid camperId,
        Guid maintenanceItemId,
        HttpContext context)
    {
        return result.Outcome switch
        {
            RecordMaintenanceLogOutcome.Created => Results.Created(
                $"/api/v1/campers/{camperId:D}/maintenance/{maintenanceItemId:D}/logs/{result.Log!.Id:D}",
                result.Log),
            RecordMaintenanceLogOutcome.Replayed => Results.Ok(result.Log),
            RecordMaintenanceLogOutcome.NotFound => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status404NotFound,
                "Risorsa non trovata.",
                NotFoundProblemType)),
            RecordMaintenanceLogOutcome.OdometerConflict => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status409Conflict,
                "La lettura del contachilometri di quel giorno ha un valore diverso.",
                OdometerConflictProblemType)),
            RecordMaintenanceLogOutcome.ProjectionOutOfRange => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status400BadRequest,
                "La scadenza calcolata supera il range supportato.",
                MaintenanceProjectionRangeProblemType)),
            RecordMaintenanceLogOutcome.ConcurrencyConflict => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status409Conflict,
                "La manutenzione e' cambiata durante la registrazione. Riprova.",
                MaintenanceStateConflictProblemType)),
            _ => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status409Conflict,
                "L'identificativo e' gia' associato a un intervento con contenuto diverso.",
                MaintenanceLogConflictProblemType)),
        };
    }
}
