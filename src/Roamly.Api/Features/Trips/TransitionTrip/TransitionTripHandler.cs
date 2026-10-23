using System.Data;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Ownership;
using Roamly.Api.Features.Trips;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Trips.TransitionTrip;

/// <summary>Applica una transizione owner-scoped al viaggio con concorrenza ottimistica.</summary>
[ProtocolOutcomeHandler("404 uniforme, 409 per arco non ammesso, 428/412 per If-Match.")]
public sealed class TransitionTripHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Transisce il viaggio posseduto dopo aver verificato il precondizionale.</summary>
    public async Task<TransitionTripResult> HandleAsync(
        Guid tripId,
        TransitionTripRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var transaction = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        var trip = await FindOwnedTripAsync(tripId, cancellationToken).ConfigureAwait(false);
        if (trip is null)
        {
            return new TransitionTripResult(TransitionTripOutcome.NotFound);
        }

        var precondition = CheckPrecondition(trip, ifMatch, out var token);
        if (precondition is not null)
        {
            return new TransitionTripResult(precondition.Value);
        }

        var result = await TransitionAsync(trip, request.Status, token, cancellationToken)
            .ConfigureAwait(false);
        if (result.Outcome == TransitionTripOutcome.Transitioned)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private Task<Trip?> FindOwnedTripAsync(Guid tripId, CancellationToken cancellationToken)
    {
        return TripAssociationLockQueries.FindForTransitionAsync(db, tripId, cancellationToken);
    }

    private static TransitionTripOutcome? CheckPrecondition(Trip trip, string? ifMatch, out byte[] token)
    {
        token = [];
        if (ifMatch is null)
        {
            return TransitionTripOutcome.MissingPrecondition;
        }

        return !RowVersionEtag.TryParse(ifMatch, out token)
            || trip.RowVersion is not { Length: > 0 } current
            || !current.AsSpan().SequenceEqual(token)
                ? TransitionTripOutcome.PreconditionFailed
                : null;
    }

    private async Task<TransitionTripResult> TransitionAsync(
        Trip trip,
        TripStatus target,
        byte[] token,
        CancellationToken cancellationToken)
    {
        var previousStatus = trip.Status;
        try
        {
            trip.TransitionTo(target);
        }
        catch (InvalidOperationException)
        {
            return new TransitionTripResult(TransitionTripOutcome.InvalidTransition);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        ApplyActualDates(trip, previousStatus, target, DateOnly.FromDateTime(now));
        trip.UpdatedAtUtc = now;
        db.Entry(trip).Property(item => item.RowVersion).OriginalValue = token;

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new TransitionTripResult(TransitionTripOutcome.PreconditionFailed);
        }

        return new TransitionTripResult(
            TransitionTripOutcome.Transitioned,
            TripDetailsResponse.From(trip),
            trip.RowVersion);
    }

    private static void ApplyActualDates(
        Trip trip,
        TripStatus previousStatus,
        TripStatus target,
        DateOnly today)
    {
        if (previousStatus == TripStatus.Planned && target == TripStatus.Active)
        {
            trip.ActualStartOnUtc = today;
        }
        else if (previousStatus == TripStatus.Active && target == TripStatus.Completed)
        {
            trip.ActualEndOnUtc = today;
        }
        else if (previousStatus == TripStatus.Completed && target == TripStatus.Active)
        {
            trip.ActualEndOnUtc = null;
        }
    }
}
