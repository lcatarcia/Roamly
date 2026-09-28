namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>
/// Esito della creazione dello schema completo su SQL Server reale.
/// <para>
/// L'eccezione e' <b>catturata, non propagata</b>: al Passo 14 il fallimento e' il risultato
/// atteso, e un test che vuole leggere <c>SqlException.Number</c> deve poterlo fare senza che la
/// fixture sia gia' esplosa per conto suo.
/// </para>
/// </summary>
/// <param name="Lease">Il database su cui lo schema e' stato applicato.</param>
/// <param name="Failure">L'eccezione osservata, oppure <c>null</c> se la creazione e' riuscita.</param>
public sealed record FullSchemaDatabase(DatabaseLease Lease, Exception? Failure)
{
    /// <summary>Connection string del database con lo schema completo.</summary>
    public string ConnectionString => Lease.ConnectionString;
}
