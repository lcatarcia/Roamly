using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Features.Trips;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Trips.GetTrip;

/// <summary>Legge il dettaglio di un viaggio dell'utente corrente.</summary>
[ProtocolOutcomeHandler("Un viaggio assente o non posseduto restituisce il 404 uniforme.")]
public sealed class GetTripHandler(RoamlyDbContext db)
{
    /// <summary>Restituisce il dettaglio ordinato o null per un lookup non posseduto.</summary>
    public async Task<GetTripResult?> HandleAsync(
        Guid tripId,
        Guid? camperId,
        CancellationToken cancellationToken)
    {
        var ownerId = db.CurrentUserId;
        var trip = await db.Trips
            .AsNoTracking()
            .Include(item => item.Stops)
            .SingleOrDefaultAsync(
                item => item.OwnerId == ownerId
                    && item.Id == tripId
                    && (!camperId.HasValue || item.CamperId == camperId.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return trip is null ? null : new GetTripResult(TripDetailsResponse.From(trip), trip.RowVersion);
    }
}
