using Roamly.Domain.Abstractions;
using Roamly.Domain.Enums;

namespace Roamly.Domain.Entities;

/// <summary>
/// Un viaggio, dalla pianificazione al consuntivo (CONTEXT.md §2.3). Figlio obbligatorio di <see cref="Camper"/>.
/// </summary>
public class Trip : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Camper di appartenenza.</summary>
    public Guid CamperId { get; set; }

    /// <summary>Nome del viaggio.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Stato del viaggio. Si cambia solo tramite <see cref="TransitionTo"/>: uno stato derivato
    /// dalle date cambierebbe da solo mentre l'utente non guarda (CONTEXT.md §2.3).
    /// </summary>
    public TripStatus Status { get; private set; } = TripStatus.Planned;

    /// <summary>Inizio pianificato.</summary>
    public DateOnly? PlannedStartOnUtc { get; set; }

    /// <summary>Fine pianificata.</summary>
    public DateOnly? PlannedEndOnUtc { get; set; }

    /// <summary>Inizio effettivo.</summary>
    public DateOnly? ActualStartOnUtc { get; set; }

    /// <summary>Fine effettiva.</summary>
    public DateOnly? ActualEndOnUtc { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Token di concorrenza ottimistica.</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>Navigazione verso il camper.</summary>
    public Camper? Camper { get; set; }

    /// <summary>Tappe del viaggio.</summary>
    public ICollection<TripStop> Stops { get; } = [];

    /// <summary>Checklist del viaggio.</summary>
    public ICollection<Checklist> Checklists { get; } = [];

    /// <summary>Note di viaggio.</summary>
    public ICollection<JournalEntry> JournalEntries { get; } = [];

    /// <summary>
    /// Applica una transizione di stato. Cinque archi legali, nessuna libreria di state machine:
    /// per un grafo cosi' piccolo aggiungerebbe vocabolario e dipendenze senza restituire nulla.
    /// </summary>
    /// <param name="target">Stato di destinazione.</param>
    /// <exception cref="InvalidOperationException">La transizione non e' nella matrice di CONTEXT.md §2.3.</exception>
    public void TransitionTo(TripStatus target)
    {
        if (!IsLegalTransition(Status, target))
        {
            throw new InvalidOperationException(
                "Transizione di stato non ammessa: " + Status.ToString() + " -> " + target.ToString() + ".");
        }

        Status = target;
    }

    /// <summary>Espone la matrice 4x4 di CONTEXT.md §2.3 senza mutare l'entita'.</summary>
    /// <param name="from">Stato di partenza.</param>
    /// <param name="to">Stato di destinazione.</param>
    /// <returns><see langword="true"/> se l'arco esiste.</returns>
    public static bool IsLegalTransition(TripStatus from, TripStatus to) => (from, to) switch
    {
        (TripStatus.Planned, TripStatus.Active) => true,
        (TripStatus.Planned, TripStatus.Cancelled) => true,
        (TripStatus.Active, TripStatus.Completed) => true,
        (TripStatus.Active, TripStatus.Cancelled) => true,
        (TripStatus.Completed, TripStatus.Active) => true,
        _ => false,
    };
}
