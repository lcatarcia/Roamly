using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Checklists.CreateChecklist;

/// <summary>Crea una checklist manuale nel confine di concorrenza del viaggio.</summary>
[ProtocolOutcomeHandler("404, 428, 412 e replay/conflict 200/409 sono esiti previsti della checklist.")]
public sealed class CreateChecklistHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Crea o riproduce la checklist identica con identita' client-side.</summary>
    public async Task<ChecklistResult> HandleAsync(
        Guid tripId,
        CreateChecklistRequest request,
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

        return await InsertAsync(trip, request, token, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ChecklistResult> InsertAsync(
        Trip trip,
        CreateChecklistRequest request,
        byte[] token,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var checklist = new Checklist
        {
            Id = request.Id!.Value,
            OwnerId = trip.OwnerId,
            TripId = trip.Id,
            Name = request.Name!.Trim(),
            Kind = request.Kind!.Value,
            Origin = MaintenanceOrigin.User,
            CreatedAtUtc = now,
        };
        db.Checklists.Add(checklist);
        ChecklistMutationSupport.TouchTrip(db, trip, token, now);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new ChecklistResult(
                ChecklistOutcome.Created,
                ChecklistDetailsResponse.From(checklist),
                trip.RowVersion);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ChecklistResult(ChecklistOutcome.PreconditionFailed);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return await ResolveDuplicateAsync(trip.Id, request, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ChecklistResult> ResolveDuplicateAsync(
        Guid tripId,
        CreateChecklistRequest request,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var existing = await ChecklistMutationSupport.FindOwnedChecklistByIdAsync(
            db, request.Id!.Value, cancellationToken).ConfigureAwait(false);
        var trip = await ChecklistMutationSupport.FindOwnedTripAsync(db, tripId, cancellationToken)
            .ConfigureAwait(false);
        return existing is null || trip is null
            ? throw new InvalidOperationException("La collisione checklist non e' stata risolta.")
            : ResolveReplay(existing, trip, tripId, request);
    }

    private static ChecklistResult ResolveReplay(
        Checklist existing,
        Trip trip,
        Guid tripId,
        CreateChecklistRequest request)
    {
        var sameContent = existing.TripId == tripId
            && string.Equals(existing.Name, request.Name!.Trim(), StringComparison.Ordinal)
            && existing.Kind == request.Kind!.Value
            && existing.Origin == MaintenanceOrigin.User;
        return sameContent
            ? new ChecklistResult(
                ChecklistOutcome.Replayed,
                ChecklistDetailsResponse.From(existing),
                trip.RowVersion)
            : new ChecklistResult(ChecklistOutcome.Conflict);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
}
