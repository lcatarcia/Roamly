using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.GetCamperProfile;

/// <summary>Legge il profilo completo limitando il lookup al proprietario autenticato.</summary>
[ProtocolOutcomeHandler("Un camper assente o non posseduto restituisce il 404 uniforme.")]
public sealed class GetCamperProfileHandler(RoamlyDbContext db)
{
    /// <summary>Restituisce profilo e rowversion, oppure null per un lookup non posseduto.</summary>
    public async Task<GetCamperProfileResult?> HandleAsync(Guid camperId, CancellationToken cancellationToken)
    {
        var camper = await db.Campers
            .AsNoTracking()
            .Where(item => item.OwnerId == db.CurrentUserId && item.Id == camperId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return camper?.RowVersion is { Length: > 0 } rowVersion
            ? new GetCamperProfileResult(CamperProfileResponse.From(camper), rowVersion)
            : null;
    }
}
