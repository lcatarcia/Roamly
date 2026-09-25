using Roamly.Common;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Utente corrente inesistente, per i contesti costruiti <b>fuori da una richiesta HTTP</b>:
/// costruzione del modello nei test, tooling di design-time. Il query filter resta attivo e
/// non seleziona nulla: e' la degradazione giusta, perche' un filtro spento sarebbe invisibile.
/// </summary>
public sealed class NoCurrentUser : ICurrentUser
{
    /// <summary>Istanza condivisa.</summary>
    public static NoCurrentUser Instance { get; } = new();

    /// <inheritdoc />
    public Guid Id => Guid.Empty;
}
