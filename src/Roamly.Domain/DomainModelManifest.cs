using Roamly.Domain.Entities;

namespace Roamly.Domain;

/// <summary>
/// Elenco esplicito delle entita' del dominio. Alimenta il verificatore R32 (ADR-0009) dello schema completo:
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

    /// <summary>Le nove entita' a topologia decisa e tabella rimandata (CONTEXT.md §2.3).</summary>
    public static IReadOnlyList<Type> LaterPhaseEntityTypes { get; } =
    [
        typeof(Trip),
        typeof(TripStop),
        typeof(Checklist),
        typeof(ChecklistItem),
        typeof(JournalEntry),
        typeof(JournalEntryAttachment),
        typeof(JournalAttachmentDeletionOutboxMessage),
        typeof(Expense),
        typeof(SavedPlace),
        typeof(Document),
    ];

    /// <summary>Entita' applicative attive nella Phase 2 del Journal.</summary>
    public static IReadOnlyList<Type> ApplicationEntityTypes { get; } =
    [
        .. Phase1EntityTypes,
        typeof(Trip),
        typeof(TripStop),
        typeof(Checklist),
        typeof(ChecklistItem),
        typeof(JournalEntry),
        typeof(JournalEntryAttachment),
        typeof(JournalAttachmentDeletionOutboxMessage),
        typeof(SavedPlace),
        typeof(Expense),
    ];

    /// <summary>Tutte le entita' di dominio e dell'outbox nel modello completo.</summary>
    public static IReadOnlyList<Type> AllEntityTypes { get; } =
        [.. Phase1EntityTypes, .. LaterPhaseEntityTypes, typeof(JournalAttachmentDeletionOutboxMessage)];
}
