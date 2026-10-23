using Roamly.Infrastructure.Persistence;
using Roamly.Domain.Entities;
using Roamly.Api.Common.Http;

namespace Roamly.Api.Features.Checklists.DeleteChecklist;

/// <summary>Elimina una voce e compatta l'ordine residuo.</summary>
[ProtocolOutcomeHandler("404 uniforme e 428/412 per If-Match sono esiti previsti.")]
public sealed class DeleteChecklistItemHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Rimuove la voce del checklist solo con l'ETag corrente del viaggio.</summary>
    public async Task<ChecklistResult> HandleAsync(
        Guid tripId,
        Guid checklistId,
        Guid itemId,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
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

        var now = clock.GetUtcNow().UtcDateTime;
        checklist.Items.Remove(item);
        db.ChecklistItems.Remove(item);
        CompactOrder(checklist, now);
        var saved = await ChecklistMutationSupport.SaveTripMutationAsync(
            db, trip, token, now, cancellationToken).ConfigureAwait(false);
        return saved
            ? ChecklistMutationSupport.UpdatedResult(checklist, trip)
            : new ChecklistResult(ChecklistOutcome.PreconditionFailed);
    }

    private static void CompactOrder(Checklist checklist, DateTime nowUtc)
    {
        var ordered = checklist.Items.OrderBy(candidate => candidate.SortOrder)
            .ThenBy(candidate => candidate.Id)
            .ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            ordered[index].SortOrder = index + 1;
            ordered[index].UpdatedAtUtc = nowUtc;
        }
    }
}
