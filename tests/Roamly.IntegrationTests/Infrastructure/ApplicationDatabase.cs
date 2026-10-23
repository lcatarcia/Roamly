namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>Esito dell'applicazione di tutte le migration del modello corrente.</summary>
public sealed record ApplicationDatabase(DatabaseLease Lease, Exception? Failure)
{
    /// <summary>Connection string del database migrato.</summary>
    public string ConnectionString => Lease.ConnectionString;
}
