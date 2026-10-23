using Roamly.Domain.Entities;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Checklists.UpdateChecklist;

/// <summary>Modifica testo o stato completato di una voce.</summary>
[ProtocolOutcomeHandler("404 uniforme e 428/412 per If-Match sono esiti previsti.")]
public sealed class UpdateChecklistItemHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Aggiorna la voce solo nel checklist e nel viaggio indicati.</summary>
    public async Task<ChecklistResult> HandleAsync(
        Guid tripId,
        Guid checklistId,
        Guid itemId,
        UpdateChecklistItemRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var trip = await ChecklistMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        var checklist = trip is null ? null : await ChecklistMutationSupport.FindOwnedChecklistAsync(
            db, tripId, checklistId, cancellationToken).ConfigureAwait(false);
        var item = checklist?.Items.SingleOrDefault(candidate => candidate.Id == itemId);
        if (trip is null || checklist is null || item is null)
        {
            return ChecklistMutationSupport.NotFoundResult();
        }

        var precondition = ChecklistMutationSupport.CheckPrecondition(trip, ifMatch, out var token);
        if (precondition is not null)
        {
            return new ChecklistResult(precondition.Value);
        }

        ApplyUpdate(item, request, clock.GetUtcNow().UtcDateTime);
        return await SaveAsync(trip, checklist, token, item.UpdatedAtUtc!.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ApplyUpdate(ChecklistItem item, UpdateChecklistItemRequest request, DateTime nowUtc)
    {
        item.Text = request.Text!.Trim();
        item.IsDone = request.IsDone!.Value;
        item.UpdatedAtUtc = nowUtc;
    }

    private async Task<ChecklistResult> SaveAsync(
        Trip trip,
        Checklist checklist,
        byte[] token,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var saved = await ChecklistMutationSupport.SaveTripMutationAsync(
            db, trip, token, nowUtc, cancellationToken).ConfigureAwait(false);
        return saved
            ? ChecklistMutationSupport.UpdatedResult(checklist, trip)
            : new ChecklistResult(ChecklistOutcome.PreconditionFailed);
    }
}
