namespace Roamly.Common;

/// <summary>
/// Identita' esplicita per un'operazione di sistema o per conto di un singolo utente.
/// L'implementazione e' scoped e non usa stato statico/ambientale.
/// </summary>
public interface ICurrentUserScope
{
    /// <summary>Indica che l'operazione corrente e' esplicitamente in modalita' sistema.</summary>
    bool IsRunningAsSystem { get; }

    /// <summary>Indica che l'operazione corrente e' esplicitamente eseguita per un utente.</summary>
    bool IsRunningAsUser { get; }

    /// <summary>Identificativo utente attivo; lancia se la modalita' non e' RunAsUser.</summary>
    Guid UserId { get; }

    /// <summary>Esegue una callback senza identita' utente.</summary>
    Task<TResult> RunAsSystemAsync<TResult>(string reason, Func<Task<TResult>> operation);

    /// <summary>Esegue una callback con il filtro OwnerScope attivo per l'utente indicato.</summary>
    Task<TResult> RunAsUserAsync<TResult>(Guid userId, string reason, Func<Task<TResult>> operation);
}
