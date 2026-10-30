using Roamly.Infrastructure.Persistence;
using Roamly.Api.Common.Http;

namespace Roamly.Api.Features.Checklists.DeleteChecklist;

/// <summary>Elimina una checklist e tutte le sue voci.</summary>
[ProtocolOutcomeHandler("404 uniforme e 428/412 per If-Match sono esiti previsti.")]
public sealed class DeleteChecklistHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Rimuove la checklist solo con l'ETag corrente del viaggio.</summary>
    public async Task<ChecklistResult> HandleAsync(
        Guid tripId,
        Guid checklistId,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        var trip = await ChecklistMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        var checklist = trip is null ? null : await ChecklistMutationSupport.FindOwnedChecklistAsync(
            db, tripId, checklistId, cancellationToken).ConfigureAwait(false);
        if (trip is null || checklist is null)
        {
            return ChecklistMutationSupport.NotFoundResult();
        }

        var precondition = ChecklistMutationSupport.CheckPrecondition(trip, ifMatch, out var token);
        if (precondition is not null)
        {
            return new ChecklistResult(precondition.Value);
        }

        db.Checklists.Remove(checklist);
        var saved = await ChecklistMutationSupport.SaveTripMutationAsync(
            db, trip, token, clock.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false);
        return saved
            ? new ChecklistResult(ChecklistOutcome.Deleted, RowVersion: trip.RowVersion)
            : new ChecklistResult(ChecklistOutcome.PreconditionFailed);
    }
}
