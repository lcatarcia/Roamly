using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Checklists.GetChecklistItem;

/// <summary>Legge una voce della checklist owner-scoped sotto il viaggio proprietario.</summary>
[ProtocolOutcomeHandler("Una voce assente o non posseduta produce il 404 uniforme.")]
public sealed class GetChecklistItemHandler(RoamlyDbContext db)
{
    /// <summary>Restituisce la voce e il rowversion del viaggio che ne protegge le mutazioni.</summary>
    public async Task<ChecklistItemResult> HandleAsync(
        Guid tripId,
        Guid checklistId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var trip = await ChecklistMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        if (trip is null)
        {
            return new ChecklistItemResult(ChecklistOutcome.NotFound);
        }

        var checklist = await ChecklistMutationSupport.FindOwnedChecklistAsync(
            db, tripId, checklistId, cancellationToken).ConfigureAwait(false);
        var item = checklist?.Items.SingleOrDefault(candidate =>
            candidate.OwnerId == db.CurrentUserId && candidate.Id == itemId);
        return item is null
            ? new ChecklistItemResult(ChecklistOutcome.NotFound)
            : new ChecklistItemResult(
                ChecklistOutcome.Found,
                ChecklistItemDetailsResponse.From(item),
                trip.RowVersion);
    }
}

/// <summary>Esito pubblico della lettura owner-scoped di una voce checklist.</summary>
public sealed record ChecklistItemResult(
    ChecklistOutcome Outcome,
    ChecklistItemDetailsResponse? Item = null,
    byte[]? RowVersion = null);
