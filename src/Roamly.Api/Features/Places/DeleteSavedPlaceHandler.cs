using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Places;

/// <summary>
/// Elimina un luogo salvato con controllo If-Match. Un luogo usato da una tappa di viaggio
/// non viene eliminato e non viene modificato nulla: la risposta e' un conflitto esplicito.
/// </summary>
[ProtocolOutcomeHandler("404 uniforme per id non posseduti; 428/412 per If-Match; 409 se referenziato da una tappa.")]
public sealed class DeleteSavedPlaceHandler(RoamlyDbContext db)
{
    private const int ForeignKeyViolation = 547;

    /// <summary>Elimina il luogo solo se il token corrisponde e nessuna tappa del proprietario lo referenzia.</summary>
    public async Task<DeleteSavedPlaceOutcome> HandleAsync(
        Guid id,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        var place = await db.SavedPlaces.SingleOrDefaultAsync(
            item => item.OwnerId == db.CurrentUserId && item.Id == id,
            cancellationToken).ConfigureAwait(false);
        if (place is null)
        {
            return DeleteSavedPlaceOutcome.NotFound;
        }

        if (ifMatch is null)
        {
            return DeleteSavedPlaceOutcome.MissingPrecondition;
        }

        if (!RowVersionEtag.TryParse(ifMatch, out var originalRowVersion))
        {
            return DeleteSavedPlaceOutcome.PreconditionFailed;
        }

        if (await IsReferencedByTripStopAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return DeleteSavedPlaceOutcome.Referenced;
        }

        db.Entry(place).Property(candidate => candidate.RowVersion).OriginalValue = originalRowVersion;
        db.SavedPlaces.Remove(place);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return DeleteSavedPlaceOutcome.Deleted;
        }
        catch (DbUpdateConcurrencyException)
        {
            return DeleteSavedPlaceOutcome.PreconditionFailed;
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            // Una tappa creata in parallelo dopo il controllo: la FK NoAction blocca la cancellazione.
            return DeleteSavedPlaceOutcome.Referenced;
        }
    }

    private Task<bool> IsReferencedByTripStopAsync(Guid savedPlaceId, CancellationToken cancellationToken)
    {
        return db.TripStops.AnyAsync(
            stop => stop.OwnerId == db.CurrentUserId && stop.SavedPlaceId == savedPlaceId,
            cancellationToken);
    }

    private static bool IsForeignKeyViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: ForeignKeyViolation };
    }
}

/// <summary>Esiti HTTP previsti dalla cancellazione.</summary>
public enum DeleteSavedPlaceOutcome
{
    /// <summary>Eliminato.</summary>
    Deleted,

    /// <summary>Non trovato o non posseduto.</summary>
    NotFound,

    /// <summary>If-Match mancante.</summary>
    MissingPrecondition,

    /// <summary>ETag malformato, non corrente o conflitto EF.</summary>
    PreconditionFailed,

    /// <summary>Referenziato da una tappa di viaggio.</summary>
    Referenced,
}
