using Microsoft.EntityFrameworkCore;
using Roamly.Common;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Contesto di sola verifica: costruisce <b>tutte le entita'</b> di dominio, comprese quelle
/// a tabella rimandata, oltre ai tipi infrastrutturali. Esiste perche' entrambi i casi 1785 noti
/// coinvolgono entita' future: senza questo modello la topologia di CONTEXT.md §5.2 resterebbe una
/// dimostrazione analitica (DATA.md §2.4). <b>Non genera migration.</b>
/// </summary>
public class FullSchemaDbContext : RoamlyDbContextBase
{
    /// <summary>Costruisce il contesto dello schema completo.</summary>
    /// <param name="options">Opzioni del contesto.</param>
    /// <param name="currentUser">Utente corrente, sorgente del filtro "OwnerScope".</param>
    public FullSchemaDbContext(DbContextOptions<FullSchemaDbContext> options, ICurrentUser currentUser)
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

    /// <summary>Contatori infrastrutturali condivisi per il rate limit login, non owned.</summary>
    public DbSet<LoginAttemptCounter> LoginAttemptCounters => Set<LoginAttemptCounter>();

    /// <summary>Viaggi.</summary>
    public DbSet<Trip> Trips => Set<Trip>();

    /// <summary>Tappe di viaggio.</summary>
    public DbSet<TripStop> TripStops => Set<TripStop>();

    /// <summary>Checklist.</summary>
    public DbSet<Checklist> Checklists => Set<Checklist>();

    /// <summary>Voci di checklist.</summary>
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();

    /// <summary>Note di viaggio.</summary>
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    /// <summary>Immagini del Journal.</summary>
    public DbSet<JournalEntryAttachment> JournalEntryAttachments => Set<JournalEntryAttachment>();

    /// <summary>Intenti persistenti di cancellazione dei blob Journal.</summary>
    public DbSet<JournalAttachmentDeletionOutboxMessage> JournalAttachmentDeletionOutbox =>
        Set<JournalAttachmentDeletionOutboxMessage>();

    /// <summary>Spese.</summary>
    public DbSet<Expense> Expenses => Set<Expense>();

    /// <summary>Luoghi salvati.</summary>
    public DbSet<SavedPlace> SavedPlaces => Set<SavedPlace>();

    /// <summary>Documenti.</summary>
    public DbSet<Document> Documents => Set<Document>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        ModelConfigurationRegistry.ApplyAll(builder, this);
    }
}
