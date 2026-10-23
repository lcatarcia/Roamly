namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>Esito dell'applicazione di tutte le migration del modello applicativo corrente.</summary>
/// <param name="Lease">Il database su cui le migration sono state applicate.</param>
/// <param name="Failure">L'eccezione osservata, oppure <c>null</c> se l'applicazione e' riuscita.</param>
public sealed record ApplicationDatabase(DatabaseLease Lease, Exception? Failure)
{
    /// <summary>Connection string del database migrato.</summary>
    public string ConnectionString => Lease.ConnectionString;
}
