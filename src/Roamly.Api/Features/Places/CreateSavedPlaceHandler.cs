using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Domain.Entities;
using Roamly.Domain.ValueObjects;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Places;

/// <summary>Crea un luogo personale con identita' client-side e replay idempotente.</summary>
[ProtocolOutcomeHandler("404 uniforme per letture owner-scoped e 409 su collisione d'identita' R50.")]
public sealed class CreateSavedPlaceHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Crea o riproduce il risultato di una richiesta con lo stesso ID.</summary>
    public async Task<CreateSavedPlaceResult> HandleAsync(
        CreateSavedPlaceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var existing = await FindAsync(request.Id!.Value, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Resolve(existing, request);
        }

        return await InsertAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CreateSavedPlaceResult> InsertAsync(
        CreateSavedPlaceRequest request,
        CancellationToken cancellationToken)
    {
        var place = BuildPlace(request);
        db.SavedPlaces.Add(place);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new CreateSavedPlaceResult(CreateSavedPlaceOutcome.Created, SavedPlaceResponse.From(place));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            var existing = await FindAsync(place.Id, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                throw;
            }

            return Resolve(existing, request);
        }
    }

    private SavedPlace BuildPlace(CreateSavedPlaceRequest request)
    {
        Coordinates? position = request.Position is null
            ? null
            : new Coordinates(request.Position.Latitude!.Value, request.Position.Longitude!.Value);
        return new SavedPlace
        {
            Id = request.Id!.Value,
            OwnerId = db.CurrentUserId,
            Name = request.Name!.Trim(),
            Category = request.Category!.Value,
            Position = position,
            Address = NormalizeOptional(request.Address),
            Notes = NormalizeOptional(request.Notes),
            GoogleMapsUrl = NormalizeOptional(request.GoogleMapsUrl),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
    }

    private Task<SavedPlace?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.SavedPlaces.AsNoTracking()
            .SingleOrDefaultAsync(
                place => place.OwnerId == db.CurrentUserId && place.Id == id,
                cancellationToken);
    }

    private static CreateSavedPlaceResult Resolve(SavedPlace existing, CreateSavedPlaceRequest request)
    {
        return HasSameContent(existing, request)
            ? new CreateSavedPlaceResult(CreateSavedPlaceOutcome.Replayed, SavedPlaceResponse.From(existing))
            : new CreateSavedPlaceResult(CreateSavedPlaceOutcome.Conflict, null);
    }

    private static bool HasSameContent(SavedPlace place, CreateSavedPlaceRequest request)
    {
        return place.Name == request.Name!.Trim()
            && place.Category == request.Category!.Value
            && PositionMatches(place.Position, request.Position)
            && place.Address == NormalizeOptional(request.Address)
            && place.Notes == NormalizeOptional(request.Notes)
            && place.GoogleMapsUrl == NormalizeOptional(request.GoogleMapsUrl);
    }

    private static bool PositionMatches(Coordinates? current, SavedPlacePositionRequest? requested)
    {
        return current is null
            ? requested is null
            : requested is not null
                && current.Value.Latitude == requested.Latitude
                && current.Value.Longitude == requested.Longitude;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
    }
}

/// <summary>Esito della creazione idempotente di un luogo.</summary>
public sealed record CreateSavedPlaceResult(CreateSavedPlaceOutcome Outcome, SavedPlaceResponse? Place);

/// <summary>Esiti osservabili della creazione di un luogo.</summary>
public enum CreateSavedPlaceOutcome
{
    /// <summary>Risorsa appena creata.</summary>
    Created,

    /// <summary>Richiesta ripetuta con lo stesso contenuto.</summary>
    Replayed,

    /// <summary>ID riutilizzato con contenuto diverso.</summary>
    Conflict,
}
