namespace Roamly.Common;

/// <summary>
/// Lanciata da <c>SaveChangesInterceptor</c> (R3) quando un insert/update/delete su un'entita'
/// <c>IOwnedResource</c> tenta di scrivere fuori dallo scope dell'identita' corrente.
/// <para>
/// E' un bug applicativo, non un errore utente: se il codice sopra l'interceptor si comporta
/// correttamente, questa eccezione non si verifica mai — un endpoint owner-scoped non dovrebbe
/// mai riuscire a costruire un'entita' con l'<c>OwnerId</c> di qualcun altro. Il messaggio non
/// include mai l'identificativo dell'utente proprietario reale (<c>actualOwnerId</c>): quel dato
/// vive solo nel log strutturato (<c>ARCHITECTURE.md</c> §6), mai in un'eccezione che potrebbe
/// finire in una risposta HTTP.
/// </para>
/// </summary>
public sealed class OwnershipViolationException : InvalidOperationException
{
    /// <summary>Costruttore senza parametri, richiesto dalle convenzioni di serializzazione .NET.</summary>
    public OwnershipViolationException()
        : this("Tentativo di scrittura su una risorsa owned fuori dallo scope dell'identita' corrente.")
    {
    }

    /// <summary>Costruttore con messaggio, richiesto dalle convenzioni di serializzazione .NET.</summary>
    /// <param name="message">Messaggio dell'eccezione.</param>
    public OwnershipViolationException(string message)
        : base(message)
    {
        EntityType = "Sconosciuto";
    }

    /// <summary>Costruttore con messaggio ed eccezione interna, richiesto dalle convenzioni .NET.</summary>
    /// <param name="message">Messaggio dell'eccezione.</param>
    /// <param name="innerException">Eccezione interna.</param>
    public OwnershipViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
        EntityType = "Sconosciuto";
    }

    private OwnershipViolationException(string entityType, bool isTyped)
        : base(
            "Tentativo di scrittura su una risorsa owned fuori dallo scope dell'identita' corrente. " +
            "Vedi il log strutturato per i dettagli (evento ownership_violation).")
    {
        _ = isTyped;
        EntityType = entityType;
    }

    /// <summary>Nome del tipo CLR dell'entita' su cui si e' tentata la scrittura fuori scope.</summary>
    public string EntityType { get; }

    /// <summary>Costruisce l'eccezione per un tipo di entita' specifico, cosi' come la lancia l'interceptor.</summary>
    /// <param name="entityType">Nome del tipo CLR dell'entita' coinvolta.</param>
    /// <returns>L'eccezione pronta al lancio.</returns>
    public static OwnershipViolationException ForEntityType(string entityType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        return new OwnershipViolationException(entityType, isTyped: true);
    }
}
