using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.CreateCamper;

/// <summary>Persiste un camper con lookup sempre limitati all'utente corrente da OwnerScope.</summary>
[ProtocolOutcomeHandler("R50 richiede che una collisione di idempotenza diventi HTTP 409, non un rifiuto di dominio.")]
public sealed class CreateCamperHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Esegue la creazione, oppure risolve un replay o un conflitto sullo stesso id.</summary>
    public async Task<CreateCamperResult> HandleAsync(CreateCamperRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = request.Id!.Value;
        var name = request.Name!.Trim();
        var vehicleKind = request.VehicleKind!.Value;
        var existing = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            return Resolve(existing, name, vehicleKind);
        }

        return await InsertAsync(id, name, vehicleKind, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CreateCamperResult> InsertAsync(
        Guid id,
        string name,
        VehicleKind vehicleKind,
        CancellationToken cancellationToken)
    {
        var camper = new Camper
        {
            Id = id,
            Name = name,
            VehicleKind = vehicleKind,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };

        db.Campers.Add(camper);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new CreateCamperResult(CreateCamperOutcome.Created, CreateCamperResponse.From(camper));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            var existing = await FindAsync(id, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                throw;
            }

            return Resolve(existing, name, vehicleKind);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
    }

    private async Task<Camper?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Campers
            .AsNoTracking()
            .SingleOrDefaultAsync(camper => camper.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    private static CreateCamperResult Resolve(Camper existing, string name, VehicleKind vehicleKind)
    {
        var sameContent = string.Equals(existing.Name.Trim(), name, StringComparison.Ordinal)
            && existing.VehicleKind == vehicleKind;

        return sameContent
            ? new CreateCamperResult(CreateCamperOutcome.Replayed, CreateCamperResponse.From(existing))
            : new CreateCamperResult(CreateCamperOutcome.Conflict, null);
    }
}
