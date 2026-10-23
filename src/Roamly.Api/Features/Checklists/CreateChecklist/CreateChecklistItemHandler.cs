using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Checklists.CreateChecklist;

/// <summary>Aggiunge una voce manuale alla checklist.</summary>
[ProtocolOutcomeHandler("404, 428, 412 e replay/conflict 200/409 sono esiti previsti della voce.")]
public sealed class CreateChecklistItemHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Aggiunge o riproduce la voce identica secondo R50.</summary>
    public async Task<ChecklistResult> HandleAsync(
        Guid tripId,
        Guid checklistId,
        CreateChecklistItemRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var trip = await ChecklistMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        var checklist = trip is null ? null : await ChecklistMutationSupport.FindOwnedChecklistAsync(
            db, tripId, checklistId, cancellationToken).ConfigureAwait(false);
        if (trip is null || checklist is null)
        {
            return ChecklistMutationSupport.NotFoundResult();
        }

        var existing = checklist.Items.SingleOrDefault(item => item.Id == request.Id!.Value);
        if (existing is not null)
        {
            return ResolveReplay(existing, checklist, trip, request);
        }

        var precondition = ChecklistMutationSupport.CheckPrecondition(trip, ifMatch, out var token);
        if (precondition is not null)
        {
            return new ChecklistResult(precondition.Value);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        checklist.Items.Add(new ChecklistItem
        {
            Id = request.Id!.Value,
            OwnerId = trip.OwnerId,
            ChecklistId = checklist.Id,
            Text = request.Text!.Trim(),
            SortOrder = checklist.Items.Count == 0 ? 1 : checklist.Items.Max(item => item.SortOrder) + 1,
            CreatedAtUtc = now,
        });

        try
        {
            var saved = await ChecklistMutationSupport.SaveTripMutationAsync(
                db, trip, token, now, cancellationToken).ConfigureAwait(false);
            return saved
                ? ChecklistMutationSupport.UpdatedResult(checklist, trip) with { Outcome = ChecklistOutcome.Created }
                : new ChecklistResult(ChecklistOutcome.PreconditionFailed);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return await ResolveDuplicateAsync(tripId, checklistId, request, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<ChecklistResult> ResolveDuplicateAsync(
        Guid tripId,
        Guid checklistId,
        CreateChecklistItemRequest request,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var trip = await ChecklistMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        var checklist = trip is null ? null : await ChecklistMutationSupport.FindOwnedChecklistAsync(
            db, tripId, checklistId, cancellationToken).ConfigureAwait(false);
        var existing = await ChecklistMutationSupport.FindOwnedItemByIdAsync(
            db, request.Id!.Value, cancellationToken).ConfigureAwait(false);
        return trip is null || checklist is null || existing is null
            ? throw new InvalidOperationException("La collisione della voce non e' stata risolta.")
            : existing.ChecklistId != checklistId
                ? new ChecklistResult(ChecklistOutcome.Conflict)
                : ResolveReplay(existing, checklist, trip, request);
    }

    private static ChecklistResult ResolveReplay(
        ChecklistItem existing,
        Checklist checklist,
        Trip trip,
        CreateChecklistItemRequest request)
    {
        return string.Equals(existing.Text, request.Text!.Trim(), StringComparison.Ordinal)
            ? new ChecklistResult(
                ChecklistOutcome.Replayed,
                ChecklistDetailsResponse.From(checklist),
                trip.RowVersion)
            : new ChecklistResult(ChecklistOutcome.Conflict);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
}
