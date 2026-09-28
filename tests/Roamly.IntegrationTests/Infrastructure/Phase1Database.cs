namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>
/// Esito dell'applicazione delle <b>migration della Phase 1</b> a un database vuoto.
/// <para>
/// L'eccezione e' <b>catturata, non propagata</b>, per la stessa ragione di
/// <see cref="FullSchemaDatabase"/>: un test che vuole leggere il messaggio del fallimento deve
/// poterlo fare senza che la fixture sia gia' esplosa per conto suo, e gli altri fatti del test C
/// devono poter riportare il proprio esito invece di sparire dietro un errore di setup.
/// </para>
/// </summary>
/// <param name="Lease">Il database su cui le migration sono state applicate.</param>
/// <param name="Failure">L'eccezione osservata, oppure <c>null</c> se l'applicazione e' riuscita.</param>
public sealed record Phase1Database(DatabaseLease Lease, Exception? Failure)
{
    /// <summary>Connection string del database migrato.</summary>
    public string ConnectionString => Lease.ConnectionString;
}
