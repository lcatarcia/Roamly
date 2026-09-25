namespace Roamly.Common;

/// <summary>
/// Identita' dell'utente corrente. Alimenta il query filter nominato "OwnerScope" (R2, ADR-0003):
/// e' deliberatamente ridotta a un solo membro, perche' e' l'unica cosa che il data layer deve sapere.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Identificativo dell'utente autenticato.</summary>
    Guid Id { get; }
}
