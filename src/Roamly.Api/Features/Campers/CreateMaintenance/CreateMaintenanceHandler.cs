using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.CreateMaintenance;

/// <summary>Crea regole ricorrenti owner-scoped senza inventare uno storico di servizio.</summary>
[ProtocolOutcomeHandler("R50: replay e conflitto d'identificativo hanno esiti HTTP distinti.")]
public sealed class CreateMaintenanceHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Crea la regola oppure restituisce il replay o il conflitto sul suo identificativo.</summary>
    public async Task<CreateMaintenanceResult> HandleAsync(
        Guid camperId,
        CreateMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var camperExists = await db.Campers
            .AnyAsync(camper => camper.Id == camperId, cancellationToken)
            .ConfigureAwait(false);
        if (!camperExists)
        {
            return new CreateMaintenanceResult(CreateMaintenanceOutcome.NotFound, null);
        }

        var name = request.Name!.Trim();
        var category = request.Category!.Value;
        var existing = await FindAsync(request.Id!.Value, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Resolve(existing, camperId, name, category, request.IntervalKm, request.IntervalMonths);
        }

        return await InsertAsync(camperId, request, name, category, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CreateMaintenanceResult> InsertAsync(
        Guid camperId,
        CreateMaintenanceRequest request,
        string name,
        MaintenanceCategory category,
        CancellationToken cancellationToken)
    {
        var item = new MaintenanceItem
        {
            Id = request.Id!.Value,
            CamperId = camperId,
            Name = name,
            Category = category,
            IntervalKm = request.IntervalKm,
            IntervalMonths = request.IntervalMonths,
            Origin = MaintenanceOrigin.User,
            IsActive = true,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.MaintenanceItems.Add(item);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new CreateMaintenanceResult(CreateMaintenanceOutcome.Created, CreateMaintenanceResponse.From(item));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            var existing = await FindAsync(item.Id, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                throw;
            }

            return Resolve(existing, camperId, name, category, request.IntervalKm, request.IntervalMonths);
        }
    }

    private Task<MaintenanceItem?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.MaintenanceItems.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    private static CreateMaintenanceResult Resolve(
        MaintenanceItem existing,
        Guid camperId,
        string name,
        MaintenanceCategory category,
        int? intervalKm,
        short? intervalMonths)
    {
        var sameContent = existing.CamperId == camperId
            && string.Equals(existing.Name.Trim(), name, StringComparison.Ordinal)
            && existing.Category == category
            && existing.IntervalKm == intervalKm
            && existing.IntervalMonths == intervalMonths;
        return sameContent
            ? new CreateMaintenanceResult(CreateMaintenanceOutcome.Replayed, CreateMaintenanceResponse.From(existing))
            : new CreateMaintenanceResult(CreateMaintenanceOutcome.Conflict, null);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
    }
}
