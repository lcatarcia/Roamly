using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;
using Roamly.Api.Features.Journal.CreateJournalEntry;
using Roamly.Api.Features.Journal.DeleteJournalEntry;
using Roamly.Api.Features.Journal.GetJournalEntry;
using Roamly.Api.Features.Journal.GetJournalAttachment;
using Roamly.Api.Features.Journal.GetJournalAttachmentDownload;
using Roamly.Api.Features.Journal.ListJournalEntries;
using Roamly.Api.Features.Journal.UpdateJournalEntry;
using Roamly.Api.Features.Journal.UploadJournalAttachment;
using Roamly.Api.Features.Journal.DeleteJournalAttachment;

namespace Roamly.Api.Features.Journal;

/// <summary>Endpoint owner-scoped per le voci e le immagini del Journal di un viaggio.</summary>
public static class JournalEndpoints
{
    private const string NotFoundProblemType = "https://roamly.dev/problems/not-found";
    private const string ConcurrencyProblemType = "https://roamly.dev/problems/concurrency-precondition";
    private const string ConflictProblemType = "https://roamly.dev/problems/journal-entry-id-content-conflict";
    private const string InvalidCursorProblemType = "https://roamly.dev/problems/invalid-cursor";

    public static IEndpointRouteBuilder MapJournalEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/v1/trips/{tripId:guid}/journal-entries", CreateEntryAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithMetadata(new IdempotencyPolicy(IdempotencyMode.IdentityBased))
            .WithName("CreateJournalEntry")
            .WithTags("Journal")
            .WithSummary("Crea un ricordo nel Journal del viaggio")
            .Produces<JournalEntryResponse>(StatusCodes.Status201Created)
            .Produces<JournalEntryResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .WithLocationResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapGet("/api/v1/trips/{tripId:guid}/journal-entries", ListEntriesAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("ListJournalEntries")
            .WithTags("Journal")
            .WithSummary("Elenca le voci del Journal del viaggio")
            .Produces<Page<JournalEntryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithProblemDetailsOrValidationOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/trips/{tripId:guid}/journal-entries/{entryId:guid}", GetEntryAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("GetJournalEntry")
            .WithTags("Journal")
            .WithSummary("Legge una voce del Journal e i metadati delle immagini")
            .Produces<JournalEntryResponse>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/api/v1/trips/{tripId:guid}/journal-entries/{entryId:guid}", UpdateEntryAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("UpdateJournalEntry")
            .WithTags("Journal")
            .WithSummary("Aggiorna un ricordo del Journal")
            .Produces<JournalEntryResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapDelete("/api/v1/trips/{tripId:guid}/journal-entries/{entryId:guid}", DeleteEntryAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("DeleteJournalEntry")
            .WithTags("Journal")
            .WithSummary("Elimina un ricordo e le sue immagini originali")
            .Produces(StatusCodes.Status204NoContent)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapPost(
                "/api/v1/trips/{tripId:guid}/journal-entries/{entryId:guid}/attachments",
                UploadAttachmentAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithMetadata(new IdempotencyPolicy(IdempotencyMode.NotRequired))
            .WithName("UploadJournalAttachment")
            .WithTags("Journal")
            .WithSummary("Carica in streaming un'immagine originale")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<JournalAttachmentResponse>(StatusCodes.Status201Created)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .WithLocationResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapGet(
                "/api/v1/trips/{tripId:guid}/journal-entries/{entryId:guid}/attachments/{attachmentId:guid}",
                GetAttachmentAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("GetJournalAttachment")
            .WithTags("Journal")
            .WithSummary("Legge i metadati owner-scoped di un'immagine")
            .Produces<JournalAttachmentResponse>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet(
                "/api/v1/trips/{tripId:guid}/journal-entries/{entryId:guid}/attachments/{attachmentId:guid}/download",
                GetAttachmentDownloadAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("GetJournalAttachmentDownload")
            .WithTags("Journal")
            .WithSummary("Emette un URL read-only a scadenza di cinque minuti")
            .Produces<JournalAttachmentDownloadResponse>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapDelete(
                "/api/v1/trips/{tripId:guid}/journal-entries/{entryId:guid}/attachments/{attachmentId:guid}",
                DeleteAttachmentAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("DeleteJournalAttachment")
            .WithTags("Journal")
            .WithSummary("Elimina un'immagine originale")
            .Produces(StatusCodes.Status204NoContent)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return app;
    }

    private static async Task<IResult> CreateEntryAsync(
        Guid tripId,
        CreateJournalEntryRequest request,
        CreateJournalEntryHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId, request, context.Request.Headers.IfMatch.ToString(), cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            JournalOutcome.Created => Created(tripId, result, context),
            JournalOutcome.Replayed => Replayed(result, context),
            JournalOutcome.NotFound => NotFound(context),
            JournalOutcome.MissingPrecondition => PreconditionRequired(context),
            JournalOutcome.PreconditionFailed => PreconditionFailed(context),
            _ => IdConflict(context),
        };
    }

    private static async Task<IResult> ListEntriesAsync(
        Guid tripId,
        [AsParameters] ListJournalEntriesRequest request,
        ListJournalEntriesHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(tripId, request, cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            JournalOutcome.Found => Results.Ok(result.Page),
            JournalOutcome.InvalidCursor => InvalidCursor(context),
            _ => NotFound(context),
        };
    }

    private static async Task<IResult> GetEntryAsync(
        Guid tripId,
        Guid entryId,
        GetJournalEntryHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(tripId, entryId, cancellationToken).ConfigureAwait(false);
        return result.Outcome == JournalOutcome.NotFound
            ? NotFound(context)
            : Found(result.Entry!, result.RowVersion, context);
    }

    private static async Task<IResult> UpdateEntryAsync(
        Guid tripId,
        Guid entryId,
        UpdateJournalEntryRequest request,
        UpdateJournalEntryHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId,
            entryId,
            request,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            JournalOutcome.Updated => Found(result.Entry!, result.RowVersion, context),
            JournalOutcome.NotFound => NotFound(context),
            JournalOutcome.MissingPrecondition => PreconditionRequired(context),
            _ => PreconditionFailed(context),
        };
    }

    private static async Task<IResult> DeleteEntryAsync(
        Guid tripId,
        Guid entryId,
        DeleteJournalEntryHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId,
            entryId,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            JournalOutcome.Deleted => Deleted(result.RowVersion, context),
            JournalOutcome.NotFound => NotFound(context),
            JournalOutcome.MissingPrecondition => PreconditionRequired(context),
            _ => PreconditionFailed(context),
        };
    }

    private static async Task<IResult> UploadAttachmentAsync(
        Guid tripId,
        Guid entryId,
        UploadJournalAttachmentHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var upload = await JournalImageMultipartReader.ReadAsync(context, cancellationToken).ConfigureAwait(false);
        if (!upload.IsValid)
        {
            return ValidationProblem(
                context,
                "file",
                upload.IsUnsupported
                    ? "Sono ammessi soltanto JPEG, PNG o WebP validati dalla firma del file."
                    : "Invia un singolo file immagine nel campo multipart 'file'.");
        }

        try
        {
            var result = await handler.HandleAsync(
                tripId,
                entryId,
                upload.Stream!,
                upload.ContentType!,
                context.Request.Headers.IfMatch.ToString(),
                cancellationToken).ConfigureAwait(false);
            return result.Outcome switch
            {
                JournalOutcome.Created => AttachmentCreated(tripId, entryId, result, context),
                JournalOutcome.NotFound => NotFound(context),
                JournalOutcome.MissingPrecondition => PreconditionRequired(context),
                JournalOutcome.PreconditionFailed => PreconditionFailed(context),
                _ => AttachmentLimitReached(context),
            };
        }
        catch (JournalImageTooLargeException)
        {
            return ValidationProblem(context, "file", "Ogni immagine puo' pesare al massimo 10 MiB.");
        }
        catch (InvalidDataException)
        {
            return ValidationProblem(context, "file", "Invia un solo file immagine.");
        }
    }

    private static async Task<IResult> GetAttachmentAsync(
        Guid tripId,
        Guid entryId,
        Guid attachmentId,
        GetJournalAttachmentHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId, entryId, attachmentId, cancellationToken).ConfigureAwait(false);
        if (result.Outcome == JournalOutcome.NotFound)
        {
            return NotFound(context);
        }

        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Attachment);
    }

    private static async Task<IResult> GetAttachmentDownloadAsync(
        Guid tripId,
        Guid entryId,
        Guid attachmentId,
        GetJournalAttachmentDownloadHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId, entryId, attachmentId, cancellationToken).ConfigureAwait(false);
        if (result.Outcome == JournalOutcome.NotFound)
        {
            return NotFound(context);
        }

        context.Response.Headers.CacheControl = "no-store";
        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Download);
    }

    private static async Task<IResult> DeleteAttachmentAsync(
        Guid tripId,
        Guid entryId,
        Guid attachmentId,
        DeleteJournalAttachmentHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            tripId,
            entryId,
            attachmentId,
            context.Request.Headers.IfMatch.ToString(),
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            JournalOutcome.Deleted => Deleted(result.RowVersion, context),
            JournalOutcome.NotFound => NotFound(context),
            JournalOutcome.MissingPrecondition => PreconditionRequired(context),
            _ => PreconditionFailed(context),
        };
    }

    private static IResult Created(Guid tripId, JournalMutationResult result, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Created(
            $"/api/v1/trips/{tripId:D}/journal-entries/{result.Entry!.Id:D}",
            result.Entry);
    }

    private static IResult Replayed(JournalMutationResult result, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Entry);
    }

    private static IResult Found(JournalEntryResponse entry, byte[]? rowVersion, HttpContext context)
    {
        SetEtag(context, rowVersion);
        return Results.Ok(entry);
    }

    private static IResult Deleted(byte[]? rowVersion, HttpContext context)
    {
        SetEtag(context, rowVersion);
        return Results.NoContent();
    }

    private static IResult AttachmentCreated(
        Guid tripId,
        Guid entryId,
        JournalAttachmentMutationResult result,
        HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Created(
            $"/api/v1/trips/{tripId:D}/journal-entries/{entryId:D}/attachments/{result.Attachment!.Id:D}",
            result.Attachment);
    }

    private static IResult AttachmentLimitReached(HttpContext context) =>
        Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status409Conflict,
            "Una voce del Journal puo' contenere al massimo 10 immagini.",
            "https://roamly.dev/problems/journal-attachment-limit"));

    private static IResult ValidationProblem(HttpContext context, string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    private static void SetEtag(HttpContext context, byte[]? rowVersion)
    {
        if (RowVersionEtag.Format(rowVersion) is { } etag)
        {
            context.Response.Headers.ETag = etag;
        }
    }

    private static IResult NotFound(HttpContext context) =>
        Results.Problem(ProblemFactory.Create(
            context, StatusCodes.Status404NotFound, "Risorsa non trovata.", NotFoundProblemType));

    private static IResult IdConflict(HttpContext context) =>
        Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status409Conflict,
            "L'identificativo e' gia' associato a una voce Journal con contenuto diverso.",
            ConflictProblemType));

    private static IResult InvalidCursor(HttpContext context) =>
        Results.Problem(ProblemFactory.Create(
            context, StatusCodes.Status400BadRequest, "Il cursore non e' valido.", InvalidCursorProblemType));

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
