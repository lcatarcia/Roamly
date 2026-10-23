using Roamly.Infrastructure.Persistence;
using Roamly.Api.Common.Http;

namespace Roamly.Api.Features.Checklists.GetChecklist;

/// <summary>Legge una checklist del viaggio owner-scoped.</summary>
[ProtocolOutcomeHandler("Una checklist assente o non posseduta produce il 404 uniforme.")]
public sealed class GetChecklistHandler(RoamlyDbContext db)
{
    /// <summary>Restituisce checklist, voci ordinate e rowversion del viaggio.</summary>
    public async Task<ChecklistResult> HandleAsync(
        Guid tripId,
        Guid checklistId,
        CancellationToken cancellationToken)
    {
        var trip = await ChecklistMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        if (trip is null)
        {
            return ChecklistMutationSupport.NotFoundResult();
        }

        var checklist = await ChecklistMutationSupport.FindOwnedChecklistAsync(
            db, tripId, checklistId, cancellationToken).ConfigureAwait(false);
        return checklist is null
            ? ChecklistMutationSupport.NotFoundResult()
            : new ChecklistResult(
                ChecklistOutcome.Found,
                ChecklistDetailsResponse.From(checklist),
                trip.RowVersion);
    }
}
