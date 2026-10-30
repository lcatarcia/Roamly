using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Roamly.Api.Common.Http;
using Roamly.Api.Features.Checklists.PreviewChecklistSeed;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Checklists.ConfirmChecklistSeed;

/// <summary>Conferma dal catalogo una checklist completa sotto il token del Trip.</summary>
[ProtocolOutcomeHandler("404, 428, 412 e replay/conflict 200/409 sono esiti previsti della conferma checklist.")]
public sealed class ConfirmChecklistSeedHandler(RoamlyDbContext db, TimeProvider clock)
{
    public async Task<ChecklistResult> HandleAsync(
        Guid tripId,
        ConfirmChecklistSeedRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var trip = await ChecklistMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        if (trip is null)
        {
            return ChecklistMutationSupport.NotFoundResult();
        }

        var existing = await ChecklistMutationSupport.FindOwnedChecklistByIdAsync(
            db, request.Id!.Value, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return ResolveReplay(existing, trip, tripId, request);
        }

        var precondition = ChecklistMutationSupport.CheckPrecondition(trip, ifMatch, out var token);
        if (precondition is not null)
        {
            return new ChecklistResult(precondition.Value);
        }

        return await ConfirmSerializableAsync(trip, tripId, request, token, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ChecklistResult> ConfirmSerializableAsync(
        Trip trip,
        Guid tripId,
        ConfirmChecklistSeedRequest request,
        byte[] token,
        CancellationToken cancellationToken)
    {
        ChecklistResult? result;
        bool duplicate;
        ChecklistOutcome missingIdentityOutcome;
        await using (var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false))
        {
            (result, duplicate, missingIdentityOutcome) = await ExecuteSerializableAsync(
                trip, request, token, transaction, cancellationToken).ConfigureAwait(false);
        }

        return duplicate
            ? await ResolveDuplicateAsync(
                trip.Id,
                request,
                missingIdentityOutcome,
                cancellationToken).ConfigureAwait(false)
            : result!;
    }

    private async Task<(ChecklistResult? Result, bool Duplicate, ChecklistOutcome MissingIdentityOutcome)>
        ExecuteSerializableAsync(
        Trip trip,
        ConfirmChecklistSeedRequest request,
        byte[] token,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await SaveAndFinishAsync(trip, request, token, transaction, cancellationToken)
                .ConfigureAwait(false);
            return (result, false, ChecklistOutcome.Conflict);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackAndClearAsync(transaction).ConfigureAwait(false);
            return (null, true, ChecklistOutcome.PreconditionFailed);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await RollbackAndClearAsync(transaction).ConfigureAwait(false);
            return (null, true, ChecklistOutcome.Conflict);
        }
        catch
        {
            await RollbackAndClearAsync(transaction).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<ChecklistResult> SaveAndFinishAsync(
        Trip trip,
        ConfirmChecklistSeedRequest request,
        byte[] token,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        var result = await BuildAndSaveAsync(trip, request, token, cancellationToken).ConfigureAwait(false);
        await FinishTransactionAsync(transaction, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<ChecklistResult> BuildAndSaveAsync(
        Trip trip,
        ConfirmChecklistSeedRequest request,
        byte[] token,
        CancellationToken cancellationToken)
    {
        var proposal = await BuildCurrentProposalAsync(trip, request.Kind!.Value, cancellationToken)
            .ConfigureAwait(false);
        if (proposal is null)
        {
            return ChecklistMutationSupport.NotFoundResult();
        }

        if (!TryMatchRequestedItems(proposal.Items, request.Items!, out var itemIds))
        {
            return new ChecklistResult(ChecklistOutcome.PreconditionFailed);
        }

        return await SaveSeedAsync(trip, request, proposal, itemIds, token, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task FinishTransactionAsync(
        IDbContextTransaction transaction,
        ChecklistResult result,
        CancellationToken cancellationToken)
    {
        if (result.Outcome == ChecklistOutcome.Created)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await RollbackAndClearAsync(transaction).ConfigureAwait(false);
    }

    private async Task<SeedProposal?> BuildCurrentProposalAsync(
        Trip trip,
        ChecklistKind kind,
        CancellationToken cancellationToken)
    {
        var ownsCamper = await db.Campers.AsNoTracking().AnyAsync(
            camper => camper.OwnerId == db.CurrentUserId && camper.Id == trip.CamperId,
            cancellationToken).ConfigureAwait(false);
        if (!ownsCamper)
        {
            return null;
        }

        var equipment = await db.Equipment.AsNoTracking()
            .Where(item => item.OwnerId == db.CurrentUserId && item.CamperId == trip.CamperId)
            .Select(item => item.Category)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);
        var items = ChecklistSeedCatalog.Build(kind, trip.PlannedStartOnUtc, trip.PlannedEndOnUtc, equipment);
        return new SeedProposal(
            ChecklistSeedCatalog.NameFor(kind),
            $"checklist.{kind.ToString().ToLowerInvariant()}",
            ChecklistSeedCatalog.Version,
            items);
    }

    private static bool TryMatchRequestedItems(
        IReadOnlyList<ChecklistSeedPreviewItemResponse> currentItems,
        ConfirmChecklistSeedItemRequest[] requestedItems,
        out Dictionary<string, Guid> itemIds)
    {
        var requestedIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        if (currentItems.Count != requestedItems.Length)
        {
            itemIds = requestedIds;
            return false;
        }

        foreach (var item in requestedItems)
        {
            requestedIds.Add(item.TemplateKey!, item.Id!.Value);
        }

        itemIds = requestedIds;
        return currentItems.All(item => requestedIds.ContainsKey(item.TemplateKey));
    }

    private async Task<ChecklistResult> SaveSeedAsync(
        Trip trip,
        ConfirmChecklistSeedRequest request,
        SeedProposal proposal,
        IReadOnlyDictionary<string, Guid> itemIds,
        byte[] token,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var checklist = CreateChecklist(trip, request.Kind!.Value, request.Id!.Value, proposal, now);
        var items = CreateItems(trip.OwnerId, checklist.Id, proposal.Items, itemIds, now);
        db.Checklists.Add(checklist);
        db.ChecklistItems.AddRange(items);
        ChecklistMutationSupport.TouchTrip(db, trip, token, now);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ChecklistResult(
            ChecklistOutcome.Created,
            ChecklistDetailsResponse.From(checklist),
            trip.RowVersion);
    }

    private async Task RollbackAndClearAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task<ChecklistResult> ResolveDuplicateAsync(
        Guid tripId,
        ConfirmChecklistSeedRequest request,
        ChecklistOutcome missingIdentityOutcome,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var existing = await ChecklistMutationSupport.FindOwnedChecklistByIdAsync(
            db, request.Id!.Value, cancellationToken).ConfigureAwait(false);
        var trip = await ChecklistMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        if (trip is null)
        {
            return ChecklistMutationSupport.NotFoundResult();
        }

        return existing is null
            ? new ChecklistResult(missingIdentityOutcome)
            : ResolveReplay(existing, trip, tripId, request);
    }

    private static ChecklistResult ResolveReplay(
        Checklist existing,
        Trip trip,
        Guid tripId,
        ConfirmChecklistSeedRequest request)
    {
        var sameChecklist = existing.TripId == tripId
            && existing.Kind == request.Kind!.Value
            && existing.Origin == MaintenanceOrigin.Seed
            && existing.SeedTemplateKey == $"checklist.{request.Kind.Value.ToString().ToLowerInvariant()}"
            && existing.SeedCatalogVersion == ChecklistSeedCatalog.Version
            && existing.Name == ChecklistSeedCatalog.NameFor(request.Kind.Value);
        return sameChecklist && SameItems(existing.Items, request.Items!)
            ? new ChecklistResult(
                ChecklistOutcome.Replayed,
                ChecklistDetailsResponse.From(existing),
                trip.RowVersion)
            : new ChecklistResult(ChecklistOutcome.Conflict);
    }

    private static bool SameItems(
        ICollection<ChecklistItem> existingItems,
        ConfirmChecklistSeedItemRequest[] requestedItems)
    {
        if (existingItems.Count != requestedItems.Length)
        {
            return false;
        }

        var existingById = existingItems.ToDictionary(item => item.Id);
        for (var index = 0; index < requestedItems.Length; index++)
        {
            var requested = requestedItems[index];
            var text = ChecklistSeedCatalog.TextForKey(requested.TemplateKey!);
            if (text is null
                || !existingById.TryGetValue(requested.Id!.Value, out var existing)
                || existing.Text != text
                || existing.SortOrder != index + 1
                || existing.IsDone)
            {
                return false;
            }
        }

        return true;
    }

    private static Checklist CreateChecklist(
        Trip trip,
        ChecklistKind kind,
        Guid id,
        SeedProposal proposal,
        DateTime now) =>
        new()
        {
            Id = id,
            OwnerId = trip.OwnerId,
            TripId = trip.Id,
            Name = proposal.Name,
            Kind = kind,
            Origin = MaintenanceOrigin.Seed,
            SeedTemplateKey = proposal.TemplateKey,
            SeedCatalogVersion = proposal.CatalogVersion,
            CreatedAtUtc = now,
        };

    private static ChecklistItem[] CreateItems(
        Guid ownerId,
        Guid checklistId,
        IReadOnlyList<ChecklistSeedPreviewItemResponse> proposedItems,
        IReadOnlyDictionary<string, Guid> itemIds,
        DateTime now) =>
        proposedItems.Select((item, index) => new ChecklistItem
        {
            Id = itemIds[item.TemplateKey],
            OwnerId = ownerId,
            ChecklistId = checklistId,
            Text = item.Text,
            IsDone = false,
            SortOrder = index + 1,
            CreatedAtUtc = now,
        }).ToArray();

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };

    private sealed record SeedProposal(
        string Name,
        string TemplateKey,
        string CatalogVersion,
        IReadOnlyList<ChecklistSeedPreviewItemResponse> Items);
}
