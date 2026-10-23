using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;
using Roamly.Api.Features.Campers.Equipment.CreateEquipment;
using Roamly.Api.Features.Campers.Equipment.DeleteEquipment;
using Roamly.Api.Features.Campers.Equipment.GetEquipment;
using Roamly.Api.Features.Campers.Equipment.ListEquipment;
using Roamly.Api.Features.Campers.Equipment.UpdateEquipment;

namespace Roamly.Api.Features.Campers.Equipment;

/// <summary>Endpoint owner-scoped per la gestione dell'inventario di bordo.</summary>
public static class EquipmentEndpoints
{
    private const string NotFoundProblemType = "https://roamly.dev/problems/not-found";
    private const string InvalidCursorProblemType = "https://roamly.dev/problems/invalid-cursor";
    private const string ContentConflictProblemType = "https://roamly.dev/problems/equipment-id-content-conflict";
    private const string ConcurrencyProblemType = "https://roamly.dev/problems/concurrency-precondition";

    /// <summary>Registra le operazioni HTTP per aggiunta, lettura, modifica e rimozione.</summary>
    public static IEndpointRouteBuilder MapEquipmentEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        MapCollectionEndpoints(app);
        MapItemEndpoints(app);
        return app;
    }

    private static void MapCollectionEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/campers/{camperId:guid}/equipment", ListEquipmentAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("ListEquipment")
            .WithTags("Equipment")
            .WithSummary("Elenca l'equipaggiamento del camper")
            .Produces<Page<EquipmentResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/api/v1/campers/{camperId:guid}/equipment", CreateEquipmentAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithMetadata(new IdempotencyPolicy(IdempotencyMode.IdentityBased))
            .WithName("CreateEquipment")
            .WithTags("Equipment")
            .WithSummary("Aggiunge un elemento all'equipaggiamento del camper")
            .Produces<EquipmentResponse>(StatusCodes.Status201Created)
            .Produces<EquipmentResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapItemEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/campers/{camperId:guid}/equipment/{equipmentId:guid}", GetEquipmentAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("GetEquipment")
            .WithTags("Equipment")
            .WithSummary("Legge il dettaglio di un elemento di equipaggiamento")
            .Produces<EquipmentResponse>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/api/v1/campers/{camperId:guid}/equipment/{equipmentId:guid}", UpdateEquipmentAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("UpdateEquipment")
            .WithTags("Equipment")
            .WithSummary("Modifica un elemento di equipaggiamento")
            .Produces<EquipmentResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapDelete("/api/v1/campers/{camperId:guid}/equipment/{equipmentId:guid}", DeleteEquipmentAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("DeleteEquipment")
            .WithTags("Equipment")
            .WithSummary("Rimuove un elemento di equipaggiamento")
            .Produces(StatusCodes.Status204NoContent)
            .WithIfMatchOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);
    }

    private static async Task<IResult> ListEquipmentAsync(
        Guid camperId,
        [AsParameters] ListEquipmentRequest request,
        ListEquipmentHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(camperId, request, cancellationToken).ConfigureAwait(false);
        if (result.Page is not null)
        {
            return Results.Ok(result.Page);
        }

        return result.InvalidCursor
            ? Problem(context, StatusCodes.Status400BadRequest, "Il cursore non e' valido.", InvalidCursorProblemType)
            : NotFound(context);
    }

    private static async Task<IResult> CreateEquipmentAsync(
        Guid camperId,
        CreateEquipmentRequest request,
        CreateEquipmentHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(camperId, request, cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            CreateEquipmentOutcome.Created => Results.Created(
                $"/api/v1/campers/{camperId:D}/equipment/{result.Equipment!.Id:D}",
                result.Equipment),
            CreateEquipmentOutcome.Replayed => Results.Ok(result.Equipment),
            CreateEquipmentOutcome.NotFound => NotFound(context),
            _ => Problem(
                context,
                StatusCodes.Status409Conflict,
                "L'identificativo e' gia' associato a un accessorio con contenuto diverso.",
                ContentConflictProblemType),
        };
    }

    private static async Task<IResult> GetEquipmentAsync(
        Guid camperId,
        Guid equipmentId,
        GetEquipmentHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(camperId, equipmentId, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return NotFound(context);
        }

        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Equipment);
    }

    private static async Task<IResult> UpdateEquipmentAsync(
        Guid camperId,
        Guid equipmentId,
        UpdateEquipmentRequest request,
        UpdateEquipmentHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            camperId,
            equipmentId,
            request,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            UpdateEquipmentOutcome.Updated => EquipmentUpdated(context, result),
            UpdateEquipmentOutcome.NotFound => NotFound(context),
            UpdateEquipmentOutcome.MissingPrecondition => PreconditionRequired(context),
            _ => PreconditionFailed(context),
        };
    }

    private static async Task<IResult> DeleteEquipmentAsync(
        Guid camperId,
        Guid equipmentId,
        DeleteEquipmentHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var outcome = await handler.HandleAsync(
            camperId,
            equipmentId,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return outcome switch
        {
            DeleteEquipmentOutcome.Deleted => Results.NoContent(),
            DeleteEquipmentOutcome.NotFound => NotFound(context),
            DeleteEquipmentOutcome.MissingPrecondition => PreconditionRequired(context),
            _ => PreconditionFailed(context),
        };
    }

    private static IResult EquipmentUpdated(HttpContext context, UpdateEquipmentResult result)
    {
        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Equipment);
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
        return Problem(context, StatusCodes.Status404NotFound, "Risorsa non trovata.", NotFoundProblemType);
    }

    private static IResult PreconditionRequired(HttpContext context)
    {
        return Problem(
            context,
            StatusCodes.Status428PreconditionRequired,
            "La richiesta deve includere If-Match.",
            ConcurrencyProblemType);
    }

    private static IResult PreconditionFailed(HttpContext context)
    {
        return Problem(
            context,
            StatusCodes.Status412PreconditionFailed,
            "La risorsa e' cambiata. Ricarica i dati e riprova.",
            ConcurrencyProblemType);
    }

    private static IResult Problem(HttpContext context, int status, string title, string type)
    {
        return Results.Problem(ProblemFactory.Create(context, status, title, type));
    }
}
