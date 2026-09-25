using Microsoft.EntityFrameworkCore;
using Roamly.Common;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Contesto applicativo: <b>solo le sei entita' della Phase 1</b> (CONTEXT.md §2.6).
/// E' questo il contesto su cui si generano le migration.
/// </summary>
public class RoamlyDbContext : RoamlyDbContextBase
{
    /// <summary>Costruisce il contesto applicativo.</summary>
    /// <param name="options">Opzioni del contesto.</param>
    /// <param name="currentUser">Utente corrente, sorgente del filtro "OwnerScope".</param>
    public RoamlyDbContext(DbContextOptions<RoamlyDbContext> options, ICurrentUser currentUser)
        : base(options, currentUser)
    {
    }

    /// <summary>Camper dell'utente.</summary>
    public DbSet<Camper> Campers => Set<Camper>();

    /// <summary>Accessori di bordo.</summary>
    public DbSet<Equipment> Equipment => Set<Equipment>();

    /// <summary>Manutenzioni ricorrenti.</summary>
    public DbSet<MaintenanceItem> MaintenanceItems => Set<MaintenanceItem>();

    /// <summary>Storico degli interventi.</summary>
    public DbSet<MaintenanceLog> MaintenanceLogs => Set<MaintenanceLog>();

    /// <summary>Letture del contachilometri.</summary>
    public DbSet<OdometerReading> OdometerReadings => Set<OdometerReading>();

    /// <summary>Ricevute di cancellazione, non owned.</summary>
    public DbSet<ErasureReceipt> ErasureReceipts => Set<ErasureReceipt>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        ModelConfigurationRegistry.ApplyPhase1(builder, this);
    }
}
