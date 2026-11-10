using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.GetMaintenance;

/// <summary>Legge il dettaglio di una manutenzione attiva solo se appartiene al camper dell'utente corrente.</summary>
[ProtocolOutcomeHandler("Una risorsa assente o non posseduta restituisce il 404 uniforme.")]
public sealed class GetMaintenanceHandler(RoamlyDbContext db)
{
    /// <summary>Restituisce il dettaglio e il token di concorrenza, oppure null.</summary>
    public async Task<GetMaintenanceResult?> HandleAsync(
        Guid camperId,
        Guid maintenanceItemId,
        CancellationToken cancellationToken)
    {
        var item = await db.MaintenanceItems.AsNoTracking()
            .Where(maintenance =>
                maintenance.OwnerId == db.CurrentUserId
                && maintenance.CamperId == camperId
                && maintenance.Id == maintenanceItemId
                && maintenance.IsActive)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (item?.RowVersion is not { Length: > 0 } rowVersion)
        {
            return null;
        }

        var latestLog = await db.MaintenanceLogs.AsNoTracking()
            .Where(log => log.OwnerId == db.CurrentUserId && log.MaintenanceItemId == maintenanceItemId)
            .OrderByDescending(log => log.PerformedOnUtc)
            .ThenByDescending(log => log.CreatedAtUtc)
            .ThenByDescending(log => log.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new GetMaintenanceResult(MaintenanceDetailResponse.From(item, latestLog), rowVersion);
    }
}
