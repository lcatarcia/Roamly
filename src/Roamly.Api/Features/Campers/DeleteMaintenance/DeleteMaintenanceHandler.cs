using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.DeleteMaintenance;

/// <summary>Rimuove fisicamente una manutenzione posseduta e il relativo storico, protetto da If-Match.</summary>
[ProtocolOutcomeHandler("404, 428 e 412 sono esiti HTTP distinti previsti da ADR-0005.")]
public sealed class DeleteMaintenanceHandler(RoamlyDbContext db)
{
    /// <summary>Elimina la riga solo con l'ETag corrente e con ownership verificata.</summary>
    public async Task<DeleteMaintenanceOutcome> HandleAsync(
        Guid camperId,
        Guid maintenanceItemId,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        var item = await FindOwnedMaintenanceAsync(camperId, maintenanceItemId, cancellationToken)
            .ConfigureAwait(false);
        if (item is null)
        {
            return DeleteMaintenanceOutcome.NotFound;
        }

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return DeleteMaintenanceOutcome.MissingPrecondition;
        }

        if (!RowVersionEtag.TryParse(ifMatch, out var token)
            || item.RowVersion is not { Length: > 0 } current
            || !current.AsSpan().SequenceEqual(token))
        {
            return DeleteMaintenanceOutcome.PreconditionFailed;
        }

        db.Entry(item).Property(maintenance => maintenance.RowVersion).OriginalValue = token;
        db.MaintenanceItems.Remove(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return DeleteMaintenanceOutcome.Deleted;
        }
        catch (DbUpdateConcurrencyException)
        {
            return DeleteMaintenanceOutcome.PreconditionFailed;
        }
    }

    private Task<Roamly.Domain.Entities.MaintenanceItem?> FindOwnedMaintenanceAsync(
        Guid camperId,
        Guid maintenanceItemId,
        CancellationToken cancellationToken)
    {
        return db.MaintenanceItems
            .Where(item =>
                item.OwnerId == db.CurrentUserId
                && item.CamperId == camperId
                && item.Id == maintenanceItemId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
