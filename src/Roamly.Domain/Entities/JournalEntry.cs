using Roamly.Domain.Abstractions;

namespace Roamly.Domain.Entities;

/// <summary>
/// Nota o ricordo di viaggio, opzionalmente ancorata a una tappa (CONTEXT.md §2.3).
/// </summary>
public class JournalEntry : IOwnedResource
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Guid OwnerId { get; set; }

    /// <summary>Viaggio di appartenenza.</summary>
    public Guid TripId { get; set; }

    /// <summary>Tappa a cui la nota e' ancorata, se esiste.</summary>
    public Guid? TripStopId { get; set; }

    /// <summary>Data della nota.</summary>
    public DateOnly EntryOnUtc { get; set; }

    /// <summary>Titolo.</summary>
    public string? Title { get; set; }

    /// <summary>Corpo della nota.</summary>
    public string Body { get; set; } = string.Empty;

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Navigazione verso il viaggio.</summary>
    public Trip? Trip { get; set; }

    /// <summary>Navigazione verso la tappa.</summary>
    public TripStop? TripStop { get; set; }
}
