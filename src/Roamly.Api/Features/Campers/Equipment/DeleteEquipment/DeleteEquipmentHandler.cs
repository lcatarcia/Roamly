using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.Equipment.DeleteEquipment;

/// <summary>Rimuove fisicamente un accessorio posseduto, protetto da If-Match.</summary>
[ProtocolOutcomeHandler("404, 428 e 412 sono esiti HTTP distinti previsti da ADR-0005.")]
public sealed class DeleteEquipmentHandler(RoamlyDbContext db)
{
    /// <summary>Elimina la riga solo con l'ETag corrente e con ownership verificata.</summary>
    public async Task<DeleteEquipmentOutcome> HandleAsync(
        Guid camperId,
        Guid equipmentId,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        var item = await FindOwnedEquipmentAsync(camperId, equipmentId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return DeleteEquipmentOutcome.NotFound;
        }

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return DeleteEquipmentOutcome.MissingPrecondition;
        }

        if (!RowVersionEtag.TryParse(ifMatch, out var token)
            || item.RowVersion is not { Length: > 0 } current
            || !current.AsSpan().SequenceEqual(token))
        {
            return DeleteEquipmentOutcome.PreconditionFailed;
        }

        db.Entry(item).Property(equipment => equipment.RowVersion).OriginalValue = token;
        db.Equipment.Remove(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return DeleteEquipmentOutcome.Deleted;
        }
        catch (DbUpdateConcurrencyException)
        {
            return DeleteEquipmentOutcome.PreconditionFailed;
        }
    }

    private Task<Roamly.Domain.Entities.Equipment?> FindOwnedEquipmentAsync(
        Guid camperId,
        Guid equipmentId,
        CancellationToken cancellationToken)
    {
        return db.Equipment
            .Where(item =>
                item.OwnerId == db.CurrentUserId
                && item.CamperId == camperId
                && item.Id == equipmentId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
