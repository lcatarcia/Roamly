using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;
using Roamly.Api.Features.Expenses.CreateExpense;
using Roamly.Api.Features.Expenses.GetExpense;
using Roamly.Api.Features.Expenses.ListExpenses;
using Roamly.Api.Features.Expenses.UpdateExpense;
using Roamly.Api.Features.Expenses.DeleteExpense;
using Microsoft.AspNetCore.WebUtilities;

namespace Roamly.Api.Features.Expenses;

/// <summary>Endpoint owner-scoped per spese.</summary>
public static class ExpensesEndpoints
{
    private const string NotFoundProblemType = "https://roamly.dev/problems/not-found";
    private const string ConflictProblemType = "https://roamly.dev/problems/expense-id-content-conflict";
    private const string CancelledTripProblemType = "https://roamly.dev/problems/cancelled-trip-expense";

    /// <summary>Registra le operazioni HTTP previste per le spese.</summary>
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapPost("/api/v1/campers/{camperId:guid}/expenses", CreateExpenseAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithMetadata(new IdempotencyPolicy(IdempotencyMode.IdentityBased))
            .WithName("CreateExpense")
            .WithTags("Expenses")
            .WithSummary("Registra una spesa del camper")
            .Produces<ExpenseDetailsResponse>(StatusCodes.Status201Created)
            .Produces<ExpenseDetailsResponse>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .WithLocationResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/v1/expenses/{expenseId:guid}", GetExpenseAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("GetExpense")
            .WithTags("Expenses")
            .WithSummary("Legge il dettaglio di una spesa")
            .Produces<ExpenseDetailsResponse>(StatusCodes.Status200OK)
            .WithEtagResponseOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/campers/{camperId:guid}/expenses", ListExpensesAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("ListExpenses")
            .WithTags("Expenses")
            .WithSummary("Elenca le spese del camper")
            .Produces<Page<ExpenseDetailsResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .WithProblemDetailsOrValidationOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/api/v1/expenses/{expenseId:guid}", UpdateExpenseAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("UpdateExpense")
            .WithTags("Expenses")
            .WithSummary("Sostituisce i dati modificabili di una spesa")
            .Produces<ExpenseDetailsResponse>(StatusCodes.Status200OK)
            .WithIfMatchOpenApi()
            .WithEtagResponseOpenApi()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        app.MapDelete("/api/v1/expenses/{expenseId:guid}", DeleteExpenseAsync)
            .RequireAuthorization()
            .WithRateLimitPolicy("None")
            .WithName("DeleteExpense")
            .WithTags("Expenses")
            .WithSummary("Elimina una spesa")
            .Produces(StatusCodes.Status204NoContent)
            .WithIfMatchOpenApi()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return app;
    }

    private static async Task<IResult> CreateExpenseAsync(
        Guid camperId,
        CreateExpenseRequest request,
        CreateExpenseHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(camperId, request, cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            CreateExpenseOutcome.Created => Created(result, camperId, context),
            CreateExpenseOutcome.Replayed => Replayed(result, context),
            CreateExpenseOutcome.NotFound => NotFound(context),
            CreateExpenseOutcome.CancelledTrip => CancelledTrip(context),
            _ => Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status409Conflict,
                "L'identificativo e' gia' associato a una spesa con contenuto diverso.",
                ConflictProblemType)),
        };
    }

    private static IResult Created(CreateExpenseResult result, Guid camperId, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Created($"/api/v1/expenses/{result.Expense!.Id:D}", result.Expense);
    }

    private static IResult Replayed(CreateExpenseResult result, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Expense);
    }

    private static async Task<IResult> GetExpenseAsync(
        Guid expenseId,
        GetExpenseHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(expenseId, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return NotFound(context);
        }

        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Expense);
    }

    private static async Task<IResult> ListExpensesAsync(
        Guid camperId,
        [AsParameters] ListExpensesRequest request,
        ListExpensesHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var query = QueryHelpers.ParseQuery(context.Request.QueryString.Value ?? string.Empty);
        var hasAfter = query.TryGetValue("after", out var after);
        request = request with { After = hasAfter ? after.ToString() : null };
        if (hasAfter && string.IsNullOrEmpty(request.After))
        {
            return InvalidCursor(context);
        }

        var result = await handler.HandleAsync(camperId, request, cancellationToken).ConfigureAwait(false);
        if (!result.CamperFound)
        {
            return NotFound(context);
        }

        return result.Page is null ? InvalidCursor(context) : Results.Ok(result.Page);
    }

    private static IResult InvalidCursor(HttpContext context)
    {
        return Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status400BadRequest,
            "Il cursore non e' valido.",
            "https://roamly.dev/problems/invalid-cursor"));
    }

    private static async Task<IResult> UpdateExpenseAsync(
        Guid expenseId,
        UpdateExpenseRequest request,
        UpdateExpenseHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            expenseId,
            request,
            context.Request.Headers.IfMatch.ToString() is { Length: > 0 } ifMatch ? ifMatch : null,
            cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            UpdateExpenseOutcome.Updated => Updated(result, context),
            UpdateExpenseOutcome.NotFound => NotFound(context),
            UpdateExpenseOutcome.MissingPrecondition => PreconditionRequired(context),
            UpdateExpenseOutcome.CancelledTrip => CancelledTrip(context),
            _ => PreconditionFailed(context),
        };
    }

    private static async Task<IResult> DeleteExpenseAsync(
        Guid expenseId,
        DeleteExpenseHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            expenseId,
            context.Request.Headers.IfMatch.ToString() is { Length: > 0 } ifMatch ? ifMatch : null,
            cancellationToken).ConfigureAwait(false);
        return result switch
        {
            DeleteExpenseOutcome.Deleted => Results.NoContent(),
            DeleteExpenseOutcome.NotFound => NotFound(context),
            DeleteExpenseOutcome.MissingPrecondition => PreconditionRequired(context),
            _ => PreconditionFailed(context),
        };
    }

    private static IResult Updated(UpdateExpenseResult result, HttpContext context)
    {
        SetEtag(context, result.RowVersion);
        return Results.Ok(result.Expense);
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

    private static IResult CancelledTrip(HttpContext context)
    {
        return Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status409Conflict,
            "Un viaggio annullato non puo' ricevere nuove spese.",
            CancelledTripProblemType));
    }

    private static IResult PreconditionRequired(HttpContext context)
    {
        return Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status428PreconditionRequired,
            "La richiesta deve includere If-Match.",
            "https://roamly.dev/problems/concurrency-precondition"));
    }

    private static IResult PreconditionFailed(HttpContext context)
    {
        return Results.Problem(ProblemFactory.Create(
            context,
            StatusCodes.Status412PreconditionFailed,
            "La spesa e' cambiata. Ricarica i dati e riprova.",
            "https://roamly.dev/problems/concurrency-precondition"));
    }
}
