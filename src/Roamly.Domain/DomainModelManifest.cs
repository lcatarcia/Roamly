using Roamly.Domain.Entities;

namespace Roamly.Domain;

/// <summary>
/// Elenco esplicito delle entita' del dominio. Alimenta il verificatore R32 dello schema completo:
/// una lista dichiarata rende visibile l'entita' dimenticata, una scansione dell'assembly no.
/// </summary>
public static class DomainModelManifest
{
    /// <summary>Le sei entita' della prima migration (CONTEXT.md §2.6).</summary>
    public static IReadOnlyList<Type> Phase1EntityTypes { get; } =
    [
        typeof(Camper),
        typeof(Equipment),
        typeof(MaintenanceItem),
        typeof(MaintenanceLog),
        typeof(OdometerReading),
        typeof(ErasureReceipt),
    ];

    /// <summary>Le otto entita' a topologia decisa e tabella rimandata (CONTEXT.md §2.3).</summary>
    public static IReadOnlyList<Type> LaterPhaseEntityTypes { get; } =
    [
        typeof(Trip),
        typeof(TripStop),
        typeof(Checklist),
        typeof(ChecklistItem),
        typeof(JournalEntry),
        typeof(Expense),
        typeof(SavedPlace),
        typeof(Document),
    ];

    /// <summary>Tutte e 14 le entita' del modello, Phase 1 comprese.</summary>
    public static IReadOnlyList<Type> AllEntityTypes { get; } =
        [.. Phase1EntityTypes, .. LaterPhaseEntityTypes];
}
