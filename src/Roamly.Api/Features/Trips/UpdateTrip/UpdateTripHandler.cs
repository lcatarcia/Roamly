using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Features.Trips;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Trips.UpdateTrip;

/// <summary>Sostituisce atomicamente i campi modificabili del viaggio e delle sue tappe.</summary>
[ProtocolOutcomeHandler("404, 428, 412 e 409 sono esiti HTTP previsti dal PUT aggregato.")]
public sealed class UpdateTripHandler(RoamlyDbContext db, TimeProvider clock, IIdGenerator ids)
{
    private const string JournalStopForeignKey = "FK_JournalEntries_TripStops_OwnerId_TripStopId";

    /// <summary>Applica il PUT solo al viaggio posseduto e con rowversion corrente.</summary>
    public async Task<UpdateTripResult> HandleAsync(
        Guid tripId,
        UpdateTripRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var trip = await FindOwnedTripAsync(tripId, cancellationToken).ConfigureAwait(false);
        if (trip is null)
        {
            return new UpdateTripResult(UpdateTripOutcome.NotFound);
        }

        var precondition = CheckPrecondition(trip, ifMatch, out var token);
        if (precondition is not null)
        {
            return new UpdateTripResult(precondition.Value);
        }

        if (!ContainsOnlyOwnedStopIds(trip, request.Stops!))
        {
            return new UpdateTripResult(UpdateTripOutcome.InvalidStopIds);
        }

        return await ReplaceAggregateAsync(trip, request, token, cancellationToken).ConfigureAwait(false);
    }

    private Task<Trip?> FindOwnedTripAsync(Guid tripId, CancellationToken cancellationToken)
    {
        return db.Trips
            .Include(item => item.Stops)
            .SingleOrDefaultAsync(
                item => item.OwnerId == db.CurrentUserId && item.Id == tripId,
                cancellationToken);
    }

    private static UpdateTripOutcome? CheckPrecondition(Trip trip, string? ifMatch, out byte[] token)
    {
        token = [];
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return UpdateTripOutcome.MissingPrecondition;
        }

        return !RowVersionEtag.TryParse(ifMatch, out token)
            || trip.RowVersion is not { Length: > 0 } current
            || !current.AsSpan().SequenceEqual(token)
                ? UpdateTripOutcome.PreconditionFailed
                : null;
    }

    private static bool ContainsOnlyOwnedStopIds(Trip trip, UpdateTripStopRequest[] stops)
    {
        var knownIds = trip.Stops.Select(stop => stop.Id).ToHashSet();
        return stops.Where(stop => stop.Id.HasValue).All(stop => knownIds.Contains(stop.Id!.Value));
    }

    private async Task<UpdateTripResult> ReplaceAggregateAsync(
        Trip trip,
        UpdateTripRequest request,
        byte[] token,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        ApplyTripFields(trip, request);
        ReplaceStops(trip, request.Stops!, now);
        trip.UpdatedAtUtc = now;
        db.Entry(trip).Property(item => item.RowVersion).OriginalValue = token;

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UpdateTripResult(UpdateTripOutcome.PreconditionFailed);
        }
        catch (DbUpdateException exception) when (IsJournalStopReference(exception))
        {
            return new UpdateTripResult(UpdateTripOutcome.ReferencedStopConflict);
        }

        return new UpdateTripResult(UpdateTripOutcome.Updated, TripDetailsResponse.From(trip), trip.RowVersion);
    }

    private static void ApplyTripFields(Trip trip, UpdateTripRequest request)
    {
        trip.Name = request.Name!.Trim();
        trip.PlannedStartOnUtc = request.PlannedStartOnUtc;
        trip.PlannedEndOnUtc = request.PlannedEndOnUtc;
        trip.EstimatedDistanceKm = request.EstimatedDistanceKm is { } distance
            ? decimal.Parse(distance, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)
            : null;
        trip.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
        trip.FuelPricePerLiter = request.FuelPricePerLiter is { } fuelPrice
            ? new Money(fuelPrice.Amount!.Value, fuelPrice.Currency!)
            : null;
    }

    private void ReplaceStops(Trip trip, UpdateTripStopRequest[] requestedStops, DateTime now)
    {
        var requestedIds = requestedStops.Where(stop => stop.Id.HasValue)
            .Select(stop => stop.Id!.Value)
            .ToHashSet();

        foreach (var removed in trip.Stops.Where(stop => !requestedIds.Contains(stop.Id)).ToArray())
        {
            trip.Stops.Remove(removed);
            db.TripStops.Remove(removed);
        }

        for (var index = 0; index < requestedStops.Length; index++)
        {
            var request = requestedStops[index];
            var stop = request.Id.HasValue
                ? trip.Stops.Single(item => item.Id == request.Id.Value)
                : CreateStop(trip, now);
            ApplyStopFields(stop, request, index + 1, now);
            if (!request.Id.HasValue)
            {
                trip.Stops.Add(stop);
            }
        }
    }

    private TripStop CreateStop(Trip trip, DateTime now)
    {
        return new TripStop
        {
            Id = ids.NewId(),
            OwnerId = trip.OwnerId,
            TripId = trip.Id,
            CreatedAtUtc = now,
        };
    }

    private static void ApplyStopFields(TripStop stop, UpdateTripStopRequest request, int sequenceNo, DateTime now)
    {
        stop.SequenceNo = sequenceNo;
        stop.Name = request.Name!.Trim();
        stop.ArrivalOnUtc = request.ArrivalOnUtc;
        stop.DepartureOnUtc = request.DepartureOnUtc;
        stop.Position = request.Position is { } position
            ? new Coordinates(position.Latitude!.Value, position.Longitude!.Value)
            : null;
        stop.Notes = request.Notes;
        if (request.Id.HasValue)
        {
            stop.UpdatedAtUtc = now;
        }
    }

    private static bool IsJournalStopReference(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 547 } sqlException
            && sqlException.Message.Contains(JournalStopForeignKey, StringComparison.Ordinal);
    }
}
