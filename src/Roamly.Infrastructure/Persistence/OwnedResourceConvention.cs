using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Abstractions;
using Roamly.Infrastructure.Identity;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Convenzione applicata a <b>ogni</b> entita' owned (ADR-0008 §2). E' l'unico punto in cui la
/// forma della chiave e' decisa: replicarla a mano su 13 entita' sarebbe 13 occasioni di sbagliarla.
/// </summary>
public static class OwnedResourceConvention
{
    /// <summary>Nome del query filter, l'unico del sistema (R2, ADR-0003).</summary>
    public const string OwnerScopeFilter = "OwnerScope";

    /// <summary>Applica PK composita, value generation, audit field, FK verso l'utente e query filter.</summary>
    /// <typeparam name="T">Entita' owned.</typeparam>
    /// <param name="builder">Builder dell'entity type.</param>
    /// <param name="context">Contesto da cui il filtro legge l'utente corrente.</param>
    public static void ConfigureOwnedResource<T>(EntityTypeBuilder<T> builder, RoamlyDbContextBase context)
        where T : class, IOwnedResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(context);

        // PK COMPOSITA, OwnerId per primo, CLUSTERED.
        // E' questa riga che elimina le 6 alternate key: le FK composite
        // referenziano direttamente la PK.
        // IsClustered(true) e' il default di SQL Server, ma va reso ESPLICITO:
        // qui e' una decisione, non un default subito.
        builder.HasKey(e => new { e.OwnerId, e.Id }).IsClustered(true);

        // EF NON applica value generation per convenzione a una proprieta' che fa
        // parte di una chiave composita. L'id e' assegnato dall'aggregato tramite
        // IIdGenerator: EF lo persiste e basta.
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.OwnerId).ValueGeneratedNever();

        builder.Property(e => e.CreatedAtUtc).HasUtcTimestamp().IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasUtcTimestamp();

        // OwnerId -> AspNetUsers: NO ACTION senza eccezioni (ADR-0004).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.OwnerId)
            .OnDelete(DeleteBehavior.NoAction);

        // Query filter nominato, l'unico del sistema (R2, ADR-0003).
        builder.HasQueryFilter(OwnerScopeFilter, e => e.OwnerId == context.CurrentUserId);
    }
}
