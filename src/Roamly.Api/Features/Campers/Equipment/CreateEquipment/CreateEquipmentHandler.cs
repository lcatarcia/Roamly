using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Features.Campers.Equipment;
using Roamly.Domain.Enums;
using EquipmentEntity = Roamly.Domain.Entities.Equipment;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.Equipment.CreateEquipment;

/// <summary>Crea accessori owner-scoped e applica la semantica d'idempotenza IdentityBased.</summary>
[ProtocolOutcomeHandler("La collisione di idempotenza dell'accessorio si traduce in HTTP 409.")]
public sealed class CreateEquipmentHandler(RoamlyDbContext db, TimeProvider clock)
{
    /// <summary>Crea l'accessorio, riproduce un replay o restituisce il conflitto previsto.</summary>
    public async Task<CreateEquipmentResult> HandleAsync(
        Guid camperId,
        CreateEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await CamperExistsAsync(camperId, cancellationToken).ConfigureAwait(false))
        {
            return new CreateEquipmentResult(CreateEquipmentOutcome.NotFound, null);
        }

        var id = request.Id!.Value;
        var name = request.Name!.Trim();
        var notes = EquipmentRequestValidation.Normalize(request.Notes);
        var existing = await FindAsync(camperId, id, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Resolve(existing, camperId, name, request.Category!.Value, notes, request.InstalledOnUtc);
        }

        return await InsertAsync(
            camperId, id, name, request.Category!.Value, notes, request.InstalledOnUtc, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<CreateEquipmentResult> InsertAsync(
        Guid camperId,
        Guid id,
        string name,
        EquipmentCategory category,
        string? notes,
        DateOnly? installedOnUtc,
        CancellationToken cancellationToken)
    {
        var equipment = new EquipmentEntity
        {
            Id = id,
            CamperId = camperId,
            Name = name,
            Category = category,
            Notes = notes,
            InstalledOnUtc = installedOnUtc,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Equipment.Add(equipment);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new CreateEquipmentResult(CreateEquipmentOutcome.Created, EquipmentResponse.From(equipment));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            var existing = await FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                throw;
            }

            return Resolve(existing, camperId, name, category, notes, installedOnUtc);
        }
    }

    private Task<bool> CamperExistsAsync(Guid camperId, CancellationToken cancellationToken)
    {
        return db.Campers.AnyAsync(
            item => item.OwnerId == db.CurrentUserId && item.Id == camperId,
            cancellationToken);
    }

    private Task<EquipmentEntity?> FindAsync(Guid camperId, Guid id, CancellationToken cancellationToken)
    {
        return db.Equipment.AsNoTracking()
            .Where(item => item.OwnerId == db.CurrentUserId && item.CamperId == camperId && item.Id == id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private Task<EquipmentEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.Equipment.AsNoTracking()
            .Where(item => item.OwnerId == db.CurrentUserId && item.Id == id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static CreateEquipmentResult Resolve(
        EquipmentEntity existing,
        Guid camperId,
        string name,
        EquipmentCategory category,
        string? notes,
        DateOnly? installedOnUtc)
    {
        var sameContent = existing.CamperId == camperId
            && string.Equals(existing.Name.Trim(), name, StringComparison.Ordinal)
            && existing.Category == category
            && string.Equals(EquipmentRequestValidation.Normalize(existing.Notes), notes, StringComparison.Ordinal)
            && existing.InstalledOnUtc == installedOnUtc;
        return sameContent
            ? new CreateEquipmentResult(CreateEquipmentOutcome.Replayed, EquipmentResponse.From(existing))
            : new CreateEquipmentResult(CreateEquipmentOutcome.Conflict, null);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
    }
}
