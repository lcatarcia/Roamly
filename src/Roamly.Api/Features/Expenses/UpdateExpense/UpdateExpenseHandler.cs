using System.Data;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Ownership;
using Roamly.Domain.Enums;
using Roamly.Domain.ValueObjects;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Expenses.UpdateExpense;

/// <summary>Sostituisce i campi modificabili mantenendo camper e ownership invariati.</summary>
[ProtocolOutcomeHandler("If-Match mancante o non corrente produce gli status R49 428/412.")]
public sealed class UpdateExpenseHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Applica la sostituzione con un solo SaveChanges e il token originale del client.</summary>
    public async Task<UpdateExpenseResult> HandleAsync(
        Guid expenseId,
        UpdateExpenseRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var expense = await FindAsync(expenseId, cancellationToken).ConfigureAwait(false);
        if (expense is null)
        {
            return Result(UpdateExpenseOutcome.NotFound);
        }

        if (ifMatch is null)
        {
            return Result(UpdateExpenseOutcome.MissingPrecondition);
        }

        if (!RowVersionEtag.TryParse(ifMatch, out var originalRowVersion))
        {
            return Result(UpdateExpenseOutcome.PreconditionFailed);
        }

        if (request.TripId is { } requestedTripId && requestedTripId != expense.TripId)
        {
            return await UpdateWithTripAssociationAsync(
                expense,
                request,
                originalRowVersion,
                requestedTripId,
                cancellationToken).ConfigureAwait(false);
        }

        return await SaveUpdateAsync(expense, request, originalRowVersion, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<UpdateExpenseResult> UpdateWithTripAssociationAsync(
        Roamly.Domain.Entities.Expense expense,
        UpdateExpenseRequest request,
        byte[] originalRowVersion,
        Guid requestedTripId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        var trip = await TripAssociationLockQueries.FindForExpenseAssociationAsync(
            db,
            requestedTripId,
            expense.CamperId,
            cancellationToken).ConfigureAwait(false);
        if (trip is null)
        {
            return Result(UpdateExpenseOutcome.NotFound);
        }

        if (trip.Status == TripStatus.Cancelled)
        {
            return Result(UpdateExpenseOutcome.CancelledTrip);
        }

        var result = await SaveUpdateAsync(
            expense,
            request,
            originalRowVersion,
            cancellationToken).ConfigureAwait(false);
        if (result.Outcome == UpdateExpenseOutcome.Updated)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<UpdateExpenseResult> SaveUpdateAsync(
        Roamly.Domain.Entities.Expense expense,
        UpdateExpenseRequest request,
        byte[] originalRowVersion,
        CancellationToken cancellationToken)
    {
        ApplyChanges(expense, request, originalRowVersion);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new UpdateExpenseResult(
                UpdateExpenseOutcome.Updated,
                ExpenseDetailsResponse.From(expense),
                expense.RowVersion);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result(UpdateExpenseOutcome.PreconditionFailed);
        }
    }

    private Task<Roamly.Domain.Entities.Expense?> FindAsync(Guid expenseId, CancellationToken cancellationToken)
    {
        return db.Expenses.SingleOrDefaultAsync(
            expense => expense.OwnerId == db.CurrentUserId && expense.Id == expenseId,
            cancellationToken);
    }

    private void ApplyChanges(
        Roamly.Domain.Entities.Expense expense,
        UpdateExpenseRequest request,
        byte[] originalRowVersion)
    {
        expense.TripId = request.TripId;
        expense.Amount = new Money(request.Amount!.Amount!.Value, request.Amount.Currency!);
        expense.Category = request.Category!.Value;
        expense.IncurredOnUtc = request.IncurredOnUtc!.Value;
        expense.Description = NormalizeDescription(request.Description);
        expense.OdometerKm = request.OdometerKm;
        expense.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        db.Entry(expense).Property(candidate => candidate.RowVersion).OriginalValue = originalRowVersion;
    }

    private static string? NormalizeDescription(string? description)
    {
        return description?.Trim() is { Length: > 0 } normalized ? normalized : null;
    }

    private static UpdateExpenseResult Result(UpdateExpenseOutcome outcome) => new(outcome, null, null);
}

/// <summary>Esito della sostituzione della spesa.</summary>
public sealed record UpdateExpenseResult(
    UpdateExpenseOutcome Outcome,
    ExpenseDetailsResponse? Expense,
    byte[]? RowVersion);

/// <summary>Esiti HTTP previsti dall'update.</summary>
public enum UpdateExpenseOutcome
{
    /// <summary>Aggiornata.</summary>
    Updated,

    /// <summary>Non trovata o non posseduta.</summary>
    NotFound,

    /// <summary>If-Match mancante.</summary>
    MissingPrecondition,

    /// <summary>ETag malformato, non corrente o conflitto EF.</summary>
    PreconditionFailed,

    /// <summary>Trip incompatibile col camper.</summary>
    CancelledTrip,
}
