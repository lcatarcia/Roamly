using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Features.Campers.Equipment.CreateEquipment;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.Equipment.UpdateEquipment;

/// <summary>Aggiorna un accessorio posseduto con concorrenza basata su rowversion.</summary>
[ProtocolOutcomeHandler("404, 428 e 412 sono esiti HTTP distinti previsti da ADR-0005.")]
public sealed class UpdateEquipmentHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Modifica l'accessorio solo se l'ETag inviato e' ancora corrente.</summary>
    public async Task<UpdateEquipmentResult> HandleAsync(
        Guid camperId,
        Guid equipmentId,
        UpdateEquipmentRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await FindOwnedEquipmentAsync(camperId, equipmentId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return new UpdateEquipmentResult(UpdateEquipmentOutcome.NotFound);
        }

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return new UpdateEquipmentResult(UpdateEquipmentOutcome.MissingPrecondition);
        }

        if (!RowVersionEtag.TryParse(ifMatch, out var token)
            || item.RowVersion is not { Length: > 0 } current
            || !current.AsSpan().SequenceEqual(token))
        {
            return new UpdateEquipmentResult(UpdateEquipmentOutcome.PreconditionFailed);
        }

        item.Name = request.Name!.Trim();
        item.Category = request.Category!.Value;
        item.Notes = EquipmentRequestValidation.Normalize(request.Notes);
        item.InstalledOnUtc = request.InstalledOnUtc;
        item.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        db.Entry(item).Property(equipment => equipment.RowVersion).OriginalValue = token;

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UpdateEquipmentResult(UpdateEquipmentOutcome.PreconditionFailed);
        }

        return new UpdateEquipmentResult(
            UpdateEquipmentOutcome.Updated,
            EquipmentResponse.From(item),
            item.RowVersion);
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
