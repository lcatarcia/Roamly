using Roamly.Infrastructure.Persistence;
using Roamly.Domain.Entities;
using Roamly.Api.Common.Http;

namespace Roamly.Api.Features.Checklists.UpdateChecklist;

/// <summary>Riordina tutte le voci con una sostituzione atomica dell'ordine.</summary>
[ProtocolOutcomeHandler("404/428/412 e ID di voce non appartenenti alla checklist sono esiti previsti.")]
public sealed class ReorderChecklistItemsHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Applica l'ordine solo se contiene una volta ciascun ID della checklist.</summary>
    public async Task<ChecklistResult> HandleAsync(
        Guid tripId,
        Guid checklistId,
        ReorderChecklistItemsRequest request,
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

        if (!ContainsExactlyTheChecklistItems(checklist, request.ItemIds!))
        {
            return new ChecklistResult(ChecklistOutcome.InvalidItemIds);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        ApplyOrder(checklist, request.ItemIds!, now);
        var saved = await ChecklistMutationSupport.SaveTripMutationAsync(
            db, trip, token, now, cancellationToken).ConfigureAwait(false);
        return saved
            ? ChecklistMutationSupport.UpdatedResult(checklist, trip)
            : new ChecklistResult(ChecklistOutcome.PreconditionFailed);
    }

    private static bool ContainsExactlyTheChecklistItems(
        Checklist checklist,
        Guid[] itemIds)
    {
        return itemIds.Length == checklist.Items.Count
            && itemIds.Distinct().Count() == itemIds.Length
            && itemIds.All(id => checklist.Items.Any(item => item.Id == id));
    }

    private static void ApplyOrder(Checklist checklist, Guid[] itemIds, DateTime nowUtc)
    {
        for (var index = 0; index < itemIds.Length; index++)
        {
            var item = checklist.Items.Single(candidate => candidate.Id == itemIds[index]);
            item.SortOrder = index + 1;
            item.UpdatedAtUtc = nowUtc;
        }
    }
}
