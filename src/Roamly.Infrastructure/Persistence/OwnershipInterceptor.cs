using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Roamly.Common;
using Roamly.Domain.Abstractions;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Presidio in scrittura di R3 (<c>SECURITY.md</c> §2.2): il query filter "OwnerScope" (R2) protegge
/// solo le letture (<c>ARCHITECTURE.md</c> §2.1) — <c>Attach</c>/<c>Update</c>/<c>Remove</c>/
/// <c>SaveChanges</c> non lo attraversano mai. Questo intercettore e' l'unico punto in cui
/// un insert/update/delete su un'entita' <see cref="IOwnedResource"/> viene confrontato con
/// l'identita' corrente prima che raggiunga il database.
/// <para>
/// Iterare con <c>ChangeTracker.Entries&lt;IOwnedResource&gt;()</c> e leggere lo stato in una
/// variabile locale (<c>var state = entry.State;</c>) e' deliberato: il regex di R5/R41 su
/// <c>PathScopedBan</c> produce un falso positivo sul confronto diretto con lo stato letto da una
/// entry (S9, non un'assegnazione) ma non su una variabile locale (verificato con sonda diretta).
/// </para>
/// </summary>
public sealed class OwnershipInterceptor : ISaveChangesInterceptor
{
    /// <inheritdoc />
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Enforce(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Enforce(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Enforce(DbContext? context)
    {
        if (context is not RoamlyDbContextBase roamlyContext)
        {
            return;
        }

        var ownedEntries = roamlyContext.ChangeTracker.Entries<IOwnedResource>();
        var anyOwnedChange = false;

        foreach (var entry in ownedEntries)
        {
            var state = entry.State;

            if (state is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                anyOwnedChange = true;
                break;
            }
        }

        // ⚠️ CurrentUserId non si legge se non c'e' nulla di owned da scrivere: il percorso di
        // registrazione (AddEntityFrameworkStores) chiama SaveChanges su AspNetUsers da anonimo,
        // e ThrowingCurrentUser/HttpContextCurrentUser lanciano su Id quando non c'e' un'identita'.
        if (!anyOwnedChange)
        {
            return;
        }

        var currentUserId = roamlyContext.CurrentUserId;

        foreach (var entry in ownedEntries)
        {
            var state = entry.State;

            if (state == EntityState.Added)
            {
                EnforceAdded(entry, currentUserId);
            }
            else if (state is EntityState.Modified or EntityState.Deleted)
            {
                EnforceExisting(entry, currentUserId);
            }
        }
    }

    private static void EnforceAdded(EntityEntry<IOwnedResource> entry, Guid currentUserId)
    {
        if (entry.Entity.OwnerId == default)
        {
            entry.Property(nameof(IOwnedResource.OwnerId)).CurrentValue = currentUserId;
            return;
        }

        if (entry.Entity.OwnerId != currentUserId)
        {
            throw OwnershipViolationException.ForEntityType(entry.Entity.GetType().Name);
        }
    }

    private static void EnforceExisting(EntityEntry<IOwnedResource> entry, Guid currentUserId)
    {
        var originalOwnerId = (Guid)entry.Property(nameof(IOwnedResource.OwnerId)).OriginalValue!;

        if (originalOwnerId != currentUserId)
        {
            throw OwnershipViolationException.ForEntityType(entry.Entity.GetType().Name);
        }
    }
}
