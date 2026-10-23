using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Ownership;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Domain.ValueObjects;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Expenses.CreateExpense;

/// <summary>Crea una spesa owner-scoped e risolve i replay IdentityBased di R50.</summary>
[ProtocolOutcomeHandler("R50 richiede replay 200 e conflitto 409 per contenuto diverso.")]
public sealed class CreateExpenseHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Crea una spesa del camper o restituisce replay, conflitto o lookup non trovato.</summary>
    public async Task<CreateExpenseResult> HandleAsync(
        Guid camperId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ownerId = db.CurrentUserId;
        if (!await db.Campers.AnyAsync(
                camper => camper.OwnerId == ownerId && camper.Id == camperId,
                cancellationToken).ConfigureAwait(false))
        {
            return Result(CreateExpenseOutcome.NotFound);
        }

        var existing = await FindAsync(request.Id!.Value, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Resolve(existing, camperId, request);
        }

        if (request.TripId is null)
        {
            return await InsertOrResolveDuplicateAsync(
                ownerId,
                camperId,
                request,
                cancellationToken).ConfigureAwait(false);
        }

        return await CreateWithTripAssociationAsync(
            ownerId,
            camperId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<CreateExpenseResult> CreateWithTripAssociationAsync(
        Guid ownerId,
        Guid camperId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        DbUpdateException? duplicateException = null;
        await using (var transaction = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false))
        {
            var trip = await FindTripForAssociationAsync(
                camperId,
                request.TripId!.Value,
                cancellationToken).ConfigureAwait(false);
            if (trip is null)
            {
                return Result(CreateExpenseOutcome.NotFound);
            }

            if (trip.Status == TripStatus.Cancelled)
            {
                return Result(CreateExpenseOutcome.CancelledTrip);
            }

            try
            {
                var result = await InsertAsync(ownerId, camperId, request, cancellationToken)
                    .ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return result;
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                duplicateException = exception;
            }
        }

        return await ResolveDuplicateAsync(
            duplicateException!,
            camperId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    private Task<Roamly.Domain.Entities.Trip?> FindTripForAssociationAsync(
        Guid camperId,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return TripAssociationLockQueries.FindForExpenseAssociationAsync(
            db,
            tripId,
            camperId,
            cancellationToken);
    }

    private async Task<CreateExpenseResult> InsertOrResolveDuplicateAsync(
        Guid ownerId,
        Guid camperId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await InsertAsync(ownerId, camperId, request, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return await ResolveDuplicateAsync(exception, camperId, request, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<CreateExpenseResult> ResolveDuplicateAsync(
        DbUpdateException exception,
        Guid camperId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var existing = await FindAsync(request.Id!.Value, cancellationToken).ConfigureAwait(false);
        return existing is null
            ? throw exception
            : Resolve(existing, camperId, request);
    }

    private async Task<CreateExpenseResult> InsertAsync(
        Guid ownerId,
        Guid camperId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var expense = new Expense
        {
            Id = request.Id!.Value,
            OwnerId = ownerId,
            CamperId = camperId,
            TripId = request.TripId,
            Amount = new Money(request.Amount!.Amount!.Value, request.Amount.Currency!),
            Category = request.Category!.Value,
            IncurredOnUtc = request.IncurredOnUtc!.Value,
            Description = NormalizeDescription(request.Description),
            OdometerKm = request.OdometerKm,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Expenses.Add(expense);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CreateExpenseResult(
            CreateExpenseOutcome.Created,
            ExpenseDetailsResponse.From(expense),
            expense.RowVersion);
    }

    private Task<Expense?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.Expenses.AsNoTracking()
            .SingleOrDefaultAsync(
                expense => expense.OwnerId == db.CurrentUserId && expense.Id == id,
                cancellationToken);
    }

    private static CreateExpenseResult Resolve(Expense existing, Guid camperId, CreateExpenseRequest request)
    {
        var sameContent = existing.CamperId == camperId
            && existing.TripId == request.TripId
            && existing.Amount == new Money(request.Amount!.Amount!.Value, request.Amount.Currency!)
            && existing.Category == request.Category!.Value
            && existing.IncurredOnUtc == request.IncurredOnUtc!.Value
            && string.Equals(existing.Description, NormalizeDescription(request.Description), StringComparison.Ordinal)
            && existing.OdometerKm == request.OdometerKm;

        return sameContent
            ? new CreateExpenseResult(
                CreateExpenseOutcome.Replayed,
                ExpenseDetailsResponse.From(existing),
                existing.RowVersion)
            : Result(CreateExpenseOutcome.Conflict);
    }

    private static string? NormalizeDescription(string? description)
    {
        return description?.Trim() is { Length: > 0 } normalized ? normalized : null;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
    }

    private static CreateExpenseResult Result(CreateExpenseOutcome outcome) => new(outcome, null, null);
}
