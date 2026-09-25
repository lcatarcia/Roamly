namespace Roamly.Domain.Abstractions;

/// <summary>
/// Contratto di ogni entita' owned: chiave primaria composita <c>(OwnerId, Id)</c> (ADR-0008)
/// e audit field minimi richiesti da ADR-0004.
/// </summary>
public interface IOwnedResource
{
    /// <summary>Seconda colonna della PK composita, generata dal dominio via <c>IIdGenerator</c>.</summary>
    Guid Id { get; }

    /// <summary>Prima colonna della PK composita e della clustering key.</summary>
    Guid OwnerId { get; }

    /// <summary>Istante di creazione, sempre UTC.</summary>
    DateTime CreatedAtUtc { get; }

    /// <summary><c>null</c> significa "mai modificata dopo la creazione" (CONTEXT.md §2.0).</summary>
    DateTime? UpdatedAtUtc { get; }
}
