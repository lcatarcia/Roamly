using Microsoft.EntityFrameworkCore;
using Roamly.Infrastructure.Persistence.Configurations;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Punto unico in cui le <c>IEntityTypeConfiguration</c> vengono applicate. I due contesti
/// condividono le <b>stesse</b> configurazioni e differiscono solo per l'elenco applicato:
/// <c>ApplyConfigurationsFromAssembly</c> trascinerebbe le entita' Phase 2+ dentro
/// <see cref="RoamlyDbContext"/>, cioe' tabelle vuote in produzione per sei mesi (CONTEXT.md §2.6).
/// </summary>
public static class ModelConfigurationRegistry
{
    /// <summary>Applica le configurazioni delle sei entita' della prima migration.</summary>
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
    }

    /// <summary>Applica le configurazioni di tutte e 14 le entita'.</summary>
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
        modelBuilder.ApplyConfiguration(new ExpenseConfiguration(context));
        modelBuilder.ApplyConfiguration(new DocumentConfiguration(context));
    }
}
