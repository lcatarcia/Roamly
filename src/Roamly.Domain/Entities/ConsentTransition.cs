using Roamly.Domain.Abstractions;

namespace Roamly.Domain.Entities;

/// <summary>Prova locale Identity di una transizione; lo stato e' sempre derivato dall'ultima revisione.</summary>
public sealed class ConsentTransition : IOwnedResource
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string TextReference { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
