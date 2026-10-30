using Microsoft.EntityFrameworkCore;
using Roamly.Infrastructure.Persistence.Configurations;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Punto unico in cui le <c>IEntityTypeConfiguration</c> vengono applicate. I due contesti
/// condividono le <b>stesse</b> configurazioni e differiscono solo per l'elenco applicato:
/// <c>ApplyConfigurationsFromAssembly</c> trascinerebbe le entita' Phase 3+ dentro
/// <see cref="RoamlyDbContext"/>, cioe' tabelle vuote prima che la relativa fase sia attiva.
/// </summary>
public static class ModelConfigurationRegistry
{
    /// <summary>Applica le configurazioni delle entita' di dominio Phase 1 e delle tabelle infrastrutturali.</summary>
    /// <param name="modelBuilder">Model builder del contesto.</param>
    /// <param name="context">Contesto in costruzione, sorgente del query filter.</param>
    public static void ApplyPhase1(ModelBuilder modelBuilder, RoamlyDbContextBase context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        modelBuilder.ApplyConfiguration(new CamperConfiguration(context));
        modelBuilder.ApplyConfiguration(new EquipmentConfiguration(context));
        modelBuilder.ApplyConfiguration(new MaintenanceItemConfiguration(context));
        modelBuilder.ApplyConfiguration(new MaintenanceLogConfiguration(context));
        modelBuilder.ApplyConfiguration(new OdometerReadingConfiguration(context));
        modelBuilder.ApplyConfiguration(new ErasureReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new LoginAttemptCounterConfiguration());
    }

    /// <summary>Applica le configurazioni applicative attive e delle entita' strutturali necessarie.</summary>
    /// <param name="modelBuilder">Model builder del contesto.</param>
    /// <param name="context">Contesto in costruzione, sorgente del query filter.</param>
    public static void ApplyApplication(ModelBuilder modelBuilder, RoamlyDbContextBase context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ApplyPhase1(modelBuilder, context);
        modelBuilder.ApplyConfiguration(new TripConfiguration(
            context,
            includeChecklistNavigations: true,
            includeJournalEntryNavigations: true));
        modelBuilder.ApplyConfiguration(new TripStopConfiguration(context));
        modelBuilder.ApplyConfiguration(new ChecklistConfiguration(context));
        modelBuilder.ApplyConfiguration(new ChecklistItemConfiguration(context));
        modelBuilder.ApplyConfiguration(new JournalEntryConfiguration(context));
        modelBuilder.ApplyConfiguration(new JournalEntryAttachmentConfiguration(context));
        modelBuilder.ApplyConfiguration(new JournalAttachmentDeletionOutboxMessageConfiguration(context));
        modelBuilder.ApplyConfiguration(new SavedPlaceConfiguration(context));
        modelBuilder.ApplyConfiguration(new ExpenseConfiguration(context));
    }

    /// <summary>Applica le configurazioni di tutte le entita' del dominio.</summary>
    /// <param name="modelBuilder">Model builder del contesto.</param>
    /// <param name="context">Contesto in costruzione, sorgente del query filter.</param>
    public static void ApplyAll(ModelBuilder modelBuilder, RoamlyDbContextBase context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ApplyPhase1(modelBuilder, context);
        modelBuilder.ApplyConfiguration(new TripConfiguration(context));
        modelBuilder.ApplyConfiguration(new SavedPlaceConfiguration(context));
        modelBuilder.ApplyConfiguration(new TripStopConfiguration(context));
        modelBuilder.ApplyConfiguration(new ChecklistConfiguration(context));
        modelBuilder.ApplyConfiguration(new ChecklistItemConfiguration(context));
        modelBuilder.ApplyConfiguration(new JournalEntryConfiguration(context));
        modelBuilder.ApplyConfiguration(new JournalEntryAttachmentConfiguration(context));
        modelBuilder.ApplyConfiguration(new JournalAttachmentDeletionOutboxMessageConfiguration(context));
        modelBuilder.ApplyConfiguration(new ExpenseConfiguration(context));
        modelBuilder.ApplyConfiguration(new DocumentConfiguration(context));
    }
}
