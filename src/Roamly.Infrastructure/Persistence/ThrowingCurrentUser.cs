using Roamly.Common;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Utente corrente inesistente, per i contesti costruiti <b>fuori da una richiesta HTTP</b>:
/// costruzione del modello nei test, tooling di design-time, operazioni sullo schema (migration,
/// <c>EnsureCreated</c>) nei test di integrazione.
/// <para>
/// Lancia su <see cref="Id"/> invece di restituire un valore di comodo. E' sicuro perche' verificato
/// (R38): la costruzione del modello EF non legge mai <see cref="Id"/> — il filtro "OwnerScope" e'
/// un'espressione non valutata finche' non si esegue davvero una query — e le operazioni di schema
/// (migration, applied-migrations, <c>EnsureCreated</c>) agiscono sul catalogo, non sui dati, quindi
/// non valutano query filter. Se un giorno un percorso arrivasse davvero a interrogare un DbSet con
/// questa istanza nel contenitore, il fallimento immediato e' la degradazione giusta (R6): il
/// predecessore, <c>NoCurrentUser</c>, restituiva <c>Guid.Empty</c> — lo stesso valore di
/// <c>default(Guid)</c> di <c>OwnerId</c> — e avrebbe reso silenziosamente vuoto anche l'insieme
/// filtrato da un dato scritto senza owner.
/// </para>
/// </summary>
public sealed class ThrowingCurrentUser : ICurrentUser
{
    /// <summary>Istanza condivisa.</summary>
    public static ThrowingCurrentUser Instance { get; } = new();

    /// <inheritdoc />
    public bool IsAuthenticated => false;

    /// <inheritdoc />
    public Guid Id => throw new InvalidOperationException(
        "Nessun utente corrente in questo contesto: e' stato costruito fuori da una richiesta HTTP " +
        "(design-time, costruzione del modello, o operazione sullo schema) e non doveva mai valutare " +
        "il filtro \"OwnerScope\". Se questo lancia da un percorso che esegue davvero una query su " +
        "dati owned, quel percorso ha bisogno di un ICurrentUser vero, non di questa istanza.");
}
