using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Features.Campers.Equipment;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.Equipment.GetEquipment;

/// <summary>Legge un accessorio solo se appartiene al camper dell'utente corrente.</summary>
[ProtocolOutcomeHandler("Una risorsa assente o non posseduta restituisce il 404 uniforme.")]
public sealed class GetEquipmentHandler(RoamlyDbContext db)
{
    /// <summary>Restituisce il dettaglio e il token di concorrenza, oppure null.</summary>
    public async Task<GetEquipmentResult?> HandleAsync(
        Guid camperId,
        Guid equipmentId,
        CancellationToken cancellationToken)
    {
        var item = await db.Equipment.AsNoTracking()
            .Where(equipment =>
                equipment.OwnerId == db.CurrentUserId
                && equipment.CamperId == camperId
                && equipment.Id == equipmentId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return item?.RowVersion is { Length: > 0 } rowVersion
            ? new GetEquipmentResult(EquipmentResponse.From(item), rowVersion)
            : null;
    }
}
