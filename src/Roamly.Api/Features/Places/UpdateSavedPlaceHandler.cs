using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Places;

/// <summary>Sostituisce i dati modificabili di un luogo salvato, con controllo If-Match.</summary>
[ProtocolOutcomeHandler("404 uniforme per id non posseduti; If-Match mancante o non corrente produce 428/412.")]
public sealed class UpdateSavedPlaceHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Applica la sostituzione con un solo SaveChanges e il token originale del client.</summary>
    public async Task<UpdateSavedPlaceResult> HandleAsync(
        Guid id,
        UpdateSavedPlaceRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var place = await db.SavedPlaces.SingleOrDefaultAsync(
            item => item.OwnerId == db.CurrentUserId && item.Id == id,
            cancellationToken).ConfigureAwait(false);
        if (place is null)
        {
            return new UpdateSavedPlaceResult(UpdateSavedPlaceOutcome.NotFound, null);
        }

        if (ifMatch is null)
        {
            return new UpdateSavedPlaceResult(UpdateSavedPlaceOutcome.MissingPrecondition, null);
        }

        if (!RowVersionEtag.TryParse(ifMatch, out var originalRowVersion))
        {
            return new UpdateSavedPlaceResult(UpdateSavedPlaceOutcome.PreconditionFailed, null);
        }

        place.Name = request.Name!.Trim();
        place.Category = request.Category!.Value;
        place.Position = SavedPlaceContent.ToPosition(request.Position, request.GoogleMapsUrl);
        place.Address = SavedPlaceContent.NormalizeOptional(request.Address);
        place.Notes = SavedPlaceContent.NormalizeOptional(request.Notes);
        place.GoogleMapsUrl = SavedPlaceContent.NormalizeOptional(request.GoogleMapsUrl);
        place.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        db.Entry(place).Property(candidate => candidate.RowVersion).OriginalValue = originalRowVersion;

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UpdateSavedPlaceResult(UpdateSavedPlaceOutcome.PreconditionFailed, null);
        }

        return new UpdateSavedPlaceResult(
            UpdateSavedPlaceOutcome.Updated,
            SavedPlaceSnapshot.From(place));
    }
}

/// <summary>Esito della sostituzione di un luogo salvato.</summary>
public sealed record UpdateSavedPlaceResult(UpdateSavedPlaceOutcome Outcome, SavedPlaceSnapshot? Place);

/// <summary>Esiti HTTP previsti dalla modifica.</summary>
public enum UpdateSavedPlaceOutcome
{
    /// <summary>Aggiornato.</summary>
    Updated,

    /// <summary>Non trovato o non posseduto.</summary>
    NotFound,

    /// <summary>If-Match mancante.</summary>
    MissingPrecondition,

    /// <summary>ETag malformato, non corrente o conflitto EF.</summary>
    PreconditionFailed,
}
