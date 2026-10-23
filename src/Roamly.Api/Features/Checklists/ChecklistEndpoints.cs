using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;
using Roamly.Api.Features.Checklists.CreateChecklist;
using Roamly.Api.Features.Checklists.DeleteChecklist;
using Roamly.Api.Features.Checklists.GetChecklist;
using Roamly.Api.Features.Checklists.GetChecklistItem;
using Roamly.Api.Features.Checklists.ListChecklists;
using Roamly.Api.Features.Checklists.PreviewChecklistSeed;
using Roamly.Api.Features.Checklists.ConfirmChecklistSeed;
using Roamly.Api.Features.Checklists.UpdateChecklist;

namespace Roamly.Api.Features.Checklists;

/// <summary>Endpoint owner-scoped per le checklist manuali dei viaggi.</summary>
public static class ChecklistEndpoints
{
    private const string NotFoundProblemType = "https://roamly.dev/problems/not-found";
    private const string InvalidCursorProblemType = "https://roamly.dev/problems/invalid-cursor";
    private const string ConcurrencyProblemType = "https://roamly.dev/problems/concurrency-precondition";
    private const string IdConflictProblemType = "https://roamly.dev/problems/checklist-id-content-conflict";
    private const string ItemIdConflictProblemType = "https://roamly.dev/problems/checklist-item-id-content-conflict";

    /// <summary>Registra le operazioni HTTP previste per checklist e voci.</summary>
    public static IEndpointRouteBuilder MapChecklistEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/v1/trips/{tripId:guid}/checklists", CreateChecklistAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithMetadata(new IdempotencyPolicy(IdempotencyMode.IdentityBased))
            .WithName("CreateChecklist")
            .WithTags("Checklists")
            .WithSummary("Crea una checklist manuale del viaggio")
            .Produces<ChecklistDetailsResponse>(StatusCodes.Status201Created)
            .Produces<ChecklistDetailsResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .WithLocationResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapGet("/api/v1/trips/{tripId:guid}/checklists", ListChecklistsAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("ListChecklists")
            .WithTags("Checklists")
            .WithSummary("Elenca le checklist del viaggio")
            .Produces<Page<ChecklistDetailsResponse>>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .WithProblemDetailsOrValidationOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet(
                "/api/v1/trips/{tripId:guid}/checklists/seed-preview",
                PreviewChecklistSeedAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("PreviewChecklistSeed")
            .WithTags("Checklists")
            .WithSummary("Mostra un'anteprima delle voci suggerite per il viaggio")
            .Produces<ChecklistSeedPreviewResponse>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/api/v1/trips/{tripId:guid}/checklists/seed", ConfirmChecklistSeedAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithMetadata(new IdempotencyPolicy(IdempotencyMode.IdentityBased))
            .WithName("ConfirmChecklistSeed")
            .WithTags("Checklists")
            .WithSummary("Conferma atomicamente una checklist suggerita")
            .Produces<ChecklistDetailsResponse>(StatusCodes.Status201Created)
            .Produces<ChecklistDetailsResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .WithLocationResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapGet("/api/v1/trips/{tripId:guid}/checklists/{checklistId:guid}", GetChecklistAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("GetChecklist")
            .WithTags("Checklists")
            .WithSummary("Legge una checklist e le sue voci")
            .Produces<ChecklistDetailsResponse>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet(
                "/api/v1/trips/{tripId:guid}/checklists/{checklistId:guid}/items/{itemId:guid}",
                GetChecklistItemAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("GetChecklistItem")
            .WithTags("Checklists")
            .WithSummary("Legge una voce della checklist")
            .Produces<ChecklistItemDetailsResponse>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/api/v1/trips/{tripId:guid}/checklists/{checklistId:guid}", UpdateChecklistAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("UpdateChecklist")
            .WithTags("Checklists")
            .WithSummary("Aggiorna il nome o il momento della checklist")
            .Produces<ChecklistDetailsResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapDelete("/api/v1/trips/{tripId:guid}/checklists/{checklistId:guid}", DeleteChecklistAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("DeleteChecklist")
            .WithTags("Checklists")
            .WithSummary("Elimina una checklist e le sue voci")
            .Produces(StatusCodes.Status204NoContent)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapPost(
                "/api/v1/trips/{tripId:guid}/checklists/{checklistId:guid}/items",
                CreateChecklistItemAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithMetadata(new IdempotencyPolicy(IdempotencyMode.IdentityBased))
            .WithName("CreateChecklistItem")
            .WithTags("Checklists")
            .WithSummary("Aggiunge una voce alla checklist")
            .Produces<ChecklistDetailsResponse>(StatusCodes.Status201Created)
            .Produces<ChecklistDetailsResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .WithLocationResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapPut(
                "/api/v1/trips/{tripId:guid}/checklists/{checklistId:guid}/items/order",
                ReorderChecklistItemsAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("ReorderChecklistItems")
            .WithTags("Checklists")
            .WithSummary("Sostituisce l'ordine completo delle voci")
            .Produces<ChecklistDetailsResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapPut(
                "/api/v1/trips/{tripId:guid}/checklists/{checklistId:guid}/items/{itemId:guid}",
                UpdateChecklistItemAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("UpdateChecklistItem")
            .WithTags("Checklists")
            .WithSummary("Aggiorna o completa una voce")
            .Produces<ChecklistDetailsResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapDelete(
                "/api/v1/trips/{tripId:guid}/checklists/{checklistId:guid}/items/{itemId:guid}",
                DeleteChecklistItemAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("DeleteChecklistItem")
            .WithTags("Checklists")
            .WithSummary("Rimuove una voce dalla checklist")
            .Produces<ChecklistDetailsResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return app;
    }

    private static async Task<IResult> ListChecklistsAsync(
        Guid tripId,
        [AsParameters] ListChecklistsRequest request,
        ListChecklistsHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(tripId, request, cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            ListChecklistsOutcome.Found => ListChecklists(result, context),
            ListChecklistsOutcome.NotFound => NotFound(context),
            _ => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status400BadRequest,
                "Il cursore non e' valido.",
                InvalidCursorProblemType)),
        };
    }

    private static async Task<IResult> PreviewChecklistSeedAsync(
        Guid tripId,
        [AsParameters] PreviewChecklistSeedRequest request,
        PreviewChecklistSeedHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(tripId, request, cancellationToken).ConfigureAwait(false);
        if (result.Outcome == PreviewChecklistSeedOutcome.NotFound)
        {
            return NotFound(context);
        }

        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Preview);
    }

    private static async Task<IResult> ConfirmChecklistSeedAsync(
        Guid tripId,
        ConfirmChecklistSeedRequest request,
        ConfirmChecklistSeedHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId,
            request,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            ChecklistOutcome.Created => CreatedChecklist(tripId, result, context),
            ChecklistOutcome.Replayed => Replayed(result, context),
            ChecklistOutcome.NotFound => NotFound(context),
            ChecklistOutcome.MissingPrecondition => PreconditionRequired(context),
            ChecklistOutcome.PreconditionFailed => PreconditionFailed(context),
            _ => IdConflict(context),
        };
    }

    private static IResult ListChecklists(ListChecklistsResult result, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Page);
    }

    private static async Task<IResult> CreateChecklistAsync(
        Guid tripId,
        CreateChecklistRequest request,
        CreateChecklistHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId, request, context.Request.Headers.IfMatch.ToString(), cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            ChecklistOutcome.Created => CreatedChecklist(tripId, result, context),
            ChecklistOutcome.Replayed => Replayed(result, context),
            ChecklistOutcome.NotFound => NotFound(context),
            ChecklistOutcome.MissingPrecondition => PreconditionRequired(context),
            ChecklistOutcome.PreconditionFailed => PreconditionFailed(context),
            _ => IdConflict(context),
        };
    }

    private static async Task<IResult> GetChecklistAsync(
        Guid tripId,
        Guid checklistId,
        GetChecklistHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(tripId, checklistId, cancellationToken).ConfigureAwait(false);
        if (result.Outcome == ChecklistOutcome.NotFound)
        {
            return NotFound(context);
        }

        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Checklist);
    }

    private static async Task<IResult> GetChecklistItemAsync(
        Guid tripId,
        Guid checklistId,
        Guid itemId,
        GetChecklistItemHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(tripId, checklistId, itemId, cancellationToken)
            .ConfigureAwait(false);
        if (result.Outcome == ChecklistOutcome.NotFound)
        {
            return NotFound(context);
        }

        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Item);
    }

    private static async Task<IResult> UpdateChecklistAsync(
        Guid tripId,
        Guid checklistId,
        UpdateChecklistRequest request,
        UpdateChecklistHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId,
            checklistId,
            request,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            ChecklistOutcome.Updated => Updated(result, context),
            ChecklistOutcome.NotFound => NotFound(context),
            ChecklistOutcome.MissingPrecondition => PreconditionRequired(context),
            _ => PreconditionFailed(context),
        };
    }

    private static async Task<IResult> DeleteChecklistAsync(
        Guid tripId,
        Guid checklistId,
        DeleteChecklistHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId,
            checklistId,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            ChecklistOutcome.Deleted => Deleted(result, context),
            ChecklistOutcome.NotFound => NotFound(context),
            ChecklistOutcome.MissingPrecondition => PreconditionRequired(context),
            _ => PreconditionFailed(context),
        };
    }

    private static async Task<IResult> CreateChecklistItemAsync(
        Guid tripId,
        Guid checklistId,
        CreateChecklistItemRequest request,
        CreateChecklistItemHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId,
            checklistId,
            request,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            ChecklistOutcome.Created => CreatedItem(tripId, checklistId, request.Id!.Value, result, context),
            ChecklistOutcome.Replayed => Replayed(result, context),
            ChecklistOutcome.NotFound => NotFound(context),
            ChecklistOutcome.MissingPrecondition => PreconditionRequired(context),
            ChecklistOutcome.PreconditionFailed => PreconditionFailed(context),
            _ => ItemIdConflict(context),
        };
    }

    private static async Task<IResult> UpdateChecklistItemAsync(
        Guid tripId,
        Guid checklistId,
        Guid itemId,
        UpdateChecklistItemRequest request,
        UpdateChecklistItemHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId,
            checklistId,
            itemId,
            request,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            ChecklistOutcome.Updated => Updated(result, context),
            ChecklistOutcome.NotFound => NotFound(context),
            ChecklistOutcome.MissingPrecondition => PreconditionRequired(context),
            _ => PreconditionFailed(context),
        };
    }

    private static async Task<IResult> ReorderChecklistItemsAsync(
        Guid tripId,
        Guid checklistId,
        ReorderChecklistItemsRequest request,
        ReorderChecklistItemsHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId,
            checklistId,
            request,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            ChecklistOutcome.Updated => Updated(result, context),
            ChecklistOutcome.NotFound => NotFound(context),
            ChecklistOutcome.MissingPrecondition => PreconditionRequired(context),
            ChecklistOutcome.InvalidItemIds => Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["itemIds"] = ["L'elenco deve contenere una volta ciascun ID della checklist."],
                }),
            _ => PreconditionFailed(context),
        };
    }

    private static async Task<IResult> DeleteChecklistItemAsync(
        Guid tripId,
        Guid checklistId,
        Guid itemId,
        DeleteChecklistItemHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId,
            checklistId,
            itemId,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            ChecklistOutcome.Updated => Updated(result, context),
            ChecklistOutcome.NotFound => NotFound(context),
            ChecklistOutcome.MissingPrecondition => PreconditionRequired(context),
            _ => PreconditionFailed(context),
        };
    }

    private static IResult CreatedChecklist(Guid tripId, ChecklistResult result, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Created($"/api/v1/trips/{tripId:D}/checklists/{result.Checklist!.Id:D}", result.Checklist);
    }

    private static IResult CreatedItem(
        Guid tripId,
        Guid checklistId,
        Guid itemId,
        ChecklistResult result,
        HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Created(
            $"/api/v1/trips/{tripId:D}/checklists/{checklistId:D}/items/{itemId:D}",
            result.Checklist);
    }

    private static IResult Replayed(ChecklistResult result, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Checklist);
    }

    private static IResult Updated(ChecklistResult result, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Checklist);
    }

    private static IResult Deleted(ChecklistResult result, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.NoContent();
    }

    private static void SetEtag(HttpContext context, byte[]? rowVersion)
    {
        if (RowVersionEtag.Format(rowVersion) is { } etag)
        {
            context.Response.Headers.ETag = etag;
        }
    }

    private static IResult NotFound(HttpContext context) =>
        Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status404NotFound,
            "Risorsa non trovata.",
            NotFoundProblemType));

    private static IResult IdConflict(HttpContext context) =>
        Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status409Conflict,
            "L'identificativo e' gia' associato a una checklist con contenuto diverso.",
            IdConflictProblemType));

    private static IResult ItemIdConflict(HttpContext context) =>
        Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status409Conflict,
            "L'identificativo e' gia' associato a una voce con contenuto diverso.",
            ItemIdConflictProblemType));

    private static IResult PreconditionRequired(HttpContext context) =>
        Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status428PreconditionRequired,
            "La richiesta deve includere If-Match.",
            ConcurrencyProblemType));

    private static IResult PreconditionFailed(HttpContext context) =>
        Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status412PreconditionFailed,
            "Il viaggio e' cambiato. Ricarica i dati e riprova.",
            ConcurrencyProblemType));
}
