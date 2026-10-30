using Roamly.Infrastructure.Persistence;
using Roamly.Api.Common.Http;

namespace Roamly.Api.Features.Checklists.UpdateChecklist;

/// <summary>Aggiorna una checklist sotto la concorrenza del viaggio.</summary>
[ProtocolOutcomeHandler("404 uniforme e 428/412 per If-Match sono esiti previsti.")]
public sealed class UpdateChecklistHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Sostituisce nome e momento della checklist posseduta.</summary>
    public async Task<ChecklistResult> HandleAsync(
        Guid tripId,
        Guid checklistId,
        UpdateChecklistRequest request,
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

        var precondition = ChecklistMutationSupport.CheckPrecondition(trip, ifMatch, out var token);
        if (precondition is not null)
        {
            return new ChecklistResult(precondition.Value);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        checklist.Name = request.Name!.Trim();
        checklist.Kind = request.Kind!.Value;
        checklist.UpdatedAtUtc = now;
        var saved = await ChecklistMutationSupport.SaveTripMutationAsync(
            db, trip, token, now, cancellationToken).ConfigureAwait(false);
        return saved
            ? ChecklistMutationSupport.UpdatedResult(checklist, trip)
            : new ChecklistResult(ChecklistOutcome.PreconditionFailed);
    }
}
