using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Features.Trips;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Trips.CreateTrip;

/// <summary>Crea un viaggio owner-scoped con tappe ordinate dalla richiesta.</summary>
[ProtocolOutcomeHandler("404 uniforme e 409 per una collisione d'idempotenza R50.")]
public sealed class CreateTripHandler(RoamlyDbContext db, TimeProvider clock, IIdGenerator ids)
{
    /// <summary>Crea il viaggio o restituisce replay, conflitto o camper non posseduto.</summary>
    public async Task<CreateTripResult> HandleAsync(
        Guid camperId,
        CreateTripRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ownerId = db.CurrentUserId;
        if (!await HasCamperAsync(ownerId, camperId, cancellationToken).ConfigureAwait(false))
        {
            return new CreateTripResult(CreateTripOutcome.NotFound, null);
        }

        var existing = await FindAsync(request.Id!.Value, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Resolve(existing, camperId, request);
        }

        return await InsertAsync(ownerId, camperId, request, cancellationToken).ConfigureAwait(false);
    }

    private Task<bool> HasCamperAsync(Guid ownerId, Guid camperId, CancellationToken cancellationToken)
    {
        return db.Campers.AnyAsync(
            camper => camper.OwnerId == ownerId && camper.Id == camperId,
            cancellationToken);
    }

    private async Task<CreateTripResult> InsertAsync(
        Guid ownerId,
        Guid camperId,
        CreateTripRequest request,
        CancellationToken cancellationToken)
    {
        var createdAt = clock.GetUtcNow().UtcDateTime;
        var trip = BuildTrip(ownerId, camperId, request, createdAt);
        db.Trips.Add(trip);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new CreateTripResult(CreateTripOutcome.Created, TripDetailsResponse.From(trip));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            var existing = await FindAsync(trip.Id, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                throw;
            }

            return Resolve(existing, camperId, request);
        }
    }

    private Trip BuildTrip(Guid ownerId, Guid camperId, CreateTripRequest request, DateTime createdAt)
    {
        var trip = new Trip
        {
            Id = request.Id!.Value,
            OwnerId = ownerId,
            CamperId = camperId,
            Name = request.Name!.Trim(),
            CreatedAtUtc = createdAt,
        };

        for (var index = 0; index < request.Stops!.Length; index++)
        {
            trip.Stops.Add(new TripStop
            {
                Id = ids.NewId(),
                OwnerId = ownerId,
                TripId = trip.Id,
                SequenceNo = index + 1,
                Name = request.Stops[index].Name!.Trim(),
                CreatedAtUtc = createdAt,
            });
        }

        return trip;
    }

    private Task<Trip?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.Trips.AsNoTracking()
            .Include(trip => trip.Stops)
            .SingleOrDefaultAsync(trip => trip.OwnerId == db.CurrentUserId && trip.Id == id, cancellationToken);
    }

    private static CreateTripResult Resolve(Trip existing, Guid camperId, CreateTripRequest request)
    {
        var sameContent = existing.CamperId == camperId
            && string.Equals(existing.Name.Trim(), request.Name!.Trim(), StringComparison.Ordinal)
            && StopsMatch(existing, request.Stops!);
        return sameContent
            ? new CreateTripResult(CreateTripOutcome.Replayed, TripDetailsResponse.From(existing))
            : new CreateTripResult(CreateTripOutcome.Conflict, null);
    }

    private static bool StopsMatch(Trip existing, CreateTripStopRequest[] requestedStops)
    {
        var stops = existing.Stops.OrderBy(stop => stop.SequenceNo).ToArray();
        return stops.Length == requestedStops.Length
            && stops.Select((stop, index) => string.Equals(
                    stop.Name.Trim(),
                    requestedStops[index].Name!.Trim(),
                    StringComparison.Ordinal))
                .All(matches => matches);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
    }
}
