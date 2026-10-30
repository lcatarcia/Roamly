using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Checklists.PreviewChecklistSeed;

/// <summary>Costruisce una preview owner-scoped senza creare o modificare dati.</summary>
[ProtocolOutcomeHandler("Found e NotFound sono esiti della preview, tradotti dall'endpoint in HTTP 200 o 404.")]
public sealed class PreviewChecklistSeedHandler(RoamlyDbContext db)
{
    public async Task<PreviewChecklistSeedResult> HandleAsync(
        Guid tripId,
        PreviewChecklistSeedRequest request,
        CancellationToken cancellationToken)
    {
        var trip = await db.Trips.AsNoTracking().SingleOrDefaultAsync(
            item => item.OwnerId == db.CurrentUserId && item.Id == tripId,
            cancellationToken).ConfigureAwait(false);
        if (trip is null)
        {
            return new PreviewChecklistSeedResult(PreviewChecklistSeedOutcome.NotFound);
        }

        var ownsCamper = await db.Campers.AsNoTracking().AnyAsync(
            camper => camper.OwnerId == db.CurrentUserId && camper.Id == trip.CamperId,
            cancellationToken).ConfigureAwait(false);
        if (!ownsCamper)
        {
            return new PreviewChecklistSeedResult(PreviewChecklistSeedOutcome.NotFound);
        }

        var equipment = await db.Equipment.AsNoTracking()
            .Where(item => item.OwnerId == db.CurrentUserId && item.CamperId == trip.CamperId)
            .Select(item => item.Category)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);
        var kind = request.Kind!.Value;
        var items = ChecklistSeedCatalog.Build(
            kind,
            trip.PlannedStartOnUtc,
            trip.PlannedEndOnUtc,
            equipment);
        return new PreviewChecklistSeedResult(
            PreviewChecklistSeedOutcome.Found,
            new ChecklistSeedPreviewResponse(
                ChecklistSeedCatalog.NameFor(kind),
                kind,
                ChecklistSeedCatalog.Version,
                items),
            trip.RowVersion);
    }
}

public sealed record PreviewChecklistSeedResult(
    PreviewChecklistSeedOutcome Outcome,
    ChecklistSeedPreviewResponse? Preview = null,
    byte[]? RowVersion = null);

public enum PreviewChecklistSeedOutcome
{
    Found,
    NotFound,
}
