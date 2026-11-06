using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Places;

/// <summary>Legge un luogo personale senza rivelare risorse di altri proprietari.</summary>
[ProtocolOutcomeHandler("Una risorsa assente o non posseduta produce il 404 uniforme previsto da R7.")]
public sealed class GetSavedPlaceHandler(RoamlyDbContext db)
{
    /// <summary>Restituisce la risorsa posseduta dall'utente corrente o null.</summary>
    public async Task<SavedPlaceResponse?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var place = await db.SavedPlaces.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OwnerId == db.CurrentUserId && item.Id == id,
                cancellationToken)
            .ConfigureAwait(false);
        return place is null ? null : SavedPlaceResponse.From(place);
    }
}
