namespace Roamly.TestSupport;

/// <summary>
/// Costruttore di una singola entita' owned (R33, ADR-0009; TESTING.md §9).
/// <para>
/// <b>Build non tocca alcun <c>DbContext</c>.</b> Con <c>Id</c> e <c>OwnerId</c>
/// <c>ValueGeneratedNever</c> (R28, ADR-0008) e le FK figlie composite <c>(OwnerId, ParentId)</c>,
/// agganciare un figlio richiede due <see cref="Guid"/> gia' noti, non un round-trip sul database.
/// La persistenza — <c>AddRange</c> e <c>SaveChangesAsync</c> — e' responsabilita' del chiamante L1.
/// </para>
/// </summary>
public interface IEntityBuilder
{
    /// <summary>Tipo CLR dell'entita' prodotta. E' la chiave con cui R33 confronta modello e registro.</summary>
    Type EntityType { get; }

    /// <summary>Produce un'istanza valida e minimale, agganciata al padre gia' costruito.</summary>
    /// <param name="context">Contesto di costruzione: proprietario, id, tempo fissato e modello.</param>
    /// <returns>L'istanza POCO, non tracciata da alcun contesto EF.</returns>
    object Build(BuilderContext context);
}
