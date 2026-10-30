using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Roamly.Common;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Contesto applicativo: le entita' attive fino alla Phase 2, SavedPlace strutturale per la FK
/// opzionale delle tappe, Data Protection e il contatore infrastrutturale non-owned del rate limiter.
/// E' questo il contesto su cui si generano le migration; FullSchema verifica anche le entita' future.
/// </summary>
public class RoamlyDbContext : RoamlyDbContextBase, IDataProtectionKeyContext
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

    /// <summary>Viaggi pianificati e consuntivi.</summary>
    public DbSet<Trip> Trips => Set<Trip>();

    /// <summary>Tappe dei viaggi.</summary>
    public DbSet<TripStop> TripStops => Set<TripStop>();

    /// <summary>Checklist manuali dei viaggi.</summary>
    public DbSet<Checklist> Checklists => Set<Checklist>();

    /// <summary>Voci delle checklist.</summary>
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();

    /// <summary>Voci del Journal di viaggio.</summary>
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    /// <summary>Immagini originali del Journal.</summary>
    public DbSet<JournalEntryAttachment> JournalEntryAttachments => Set<JournalEntryAttachment>();

    /// <summary>Intenti persistenti di cancellazione dei blob Journal.</summary>
    public DbSet<JournalAttachmentDeletionOutboxMessage> JournalAttachmentDeletionOutbox =>
        Set<JournalAttachmentDeletionOutboxMessage>();

    /// <summary>Spese associate ai camper e, facoltativamente, ai viaggi.</summary>
    public DbSet<Expense> Expenses => Set<Expense>();

    /// <summary>Luoghi salvati, presenti solo per l'integrita' referenziale in questa fase.</summary>
    public DbSet<SavedPlace> SavedPlaces => Set<SavedPlace>();

    /// <summary>Ricevute di cancellazione, non owned.</summary>
    public DbSet<ErasureReceipt> ErasureReceipts => Set<ErasureReceipt>();

    /// <summary>Chiavi di Data Protection condivise fra istanze dell'applicazione.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>Contatori infrastrutturali condivisi per il rate limit login.</summary>
    public DbSet<LoginAttemptCounter> LoginAttemptCounters => Set<LoginAttemptCounter>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        ModelConfigurationRegistry.ApplyApplication(builder, this);
    }
}
