using System.Globalization;
using Microsoft.Data.SqlClient;
using Respawn;

namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>
/// Un database effimero sul container condiviso: <c>CREATE DATABASE</c> alla presa,
/// <c>Respawn</c> fra un test e l'altro, <c>DROP DATABASE</c> al rilascio.
/// <para>
/// ⚠️ <b>Nessun retry, nessun <c>Thread.Sleep</c> speculativo</b> (R37, ADR-0009). L'attesa del
/// motore e' gia' stata fatta dalla wait strategy di Testcontainers, che e' un'altra cosa:
/// attende una <b>condizione</b>, non riprova un'operazione fallita.
/// </para>
/// <para>
/// 🔵 Punto di sblocco G1 → G2: oggi ogni chiamante ottiene il proprio database dallo stesso
/// container. Per passare a un database per collection cambia chi possiede la lease, non i test.
/// </para>
/// </summary>
public sealed class DatabaseLease : IAsyncDisposable
{
    private readonly string _masterConnectionString;
    private Respawner? _respawner;

    private DatabaseLease(string masterConnectionString, string databaseName, string connectionString)
    {
        _masterConnectionString = masterConnectionString;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    /// <summary>Nome del database effimero.</summary>
    public string DatabaseName { get; }

    /// <summary>Connection string del database effimero. E' l'unica cosa che un test conosce.</summary>
    public string ConnectionString { get; }

    /// <summary>Crea un database vuoto sul container.</summary>
    /// <param name="masterConnectionString">Connection string verso <c>master</c>.</param>
    /// <param name="cancellationToken">Token di annullamento.</param>
    /// <returns>La lease sul database creato.</returns>
    public static async Task<DatabaseLease> CreateAsync(
        string masterConnectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(masterConnectionString);

        var name = "roamly_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];

        await using (var master = new SqlConnection(masterConnectionString))
        {
            await master.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(master, "CREATE DATABASE [" + name + "];", cancellationToken).ConfigureAwait(false);
        }

        var builder = new SqlConnectionStringBuilder(masterConnectionString) { InitialCatalog = name };

        return new DatabaseLease(masterConnectionString, name, builder.ConnectionString);
    }

    /// <summary>
    /// Svuota il database preservando lo schema. L'ordine dei <c>DELETE</c> e' derivato dalle FK da
    /// Respawn: e' <b>la stessa logica</b> che <c>AccountErasureJob</c> implementa, quindi un
    /// fallimento qui e' un <b>segnale</b> sulla topologia, mai un fastidio da aggirare con
    /// <c>TablesToIgnore</c> (TESTING.md §4.2).
    /// </summary>
    /// <param name="cancellationToken">Token di annullamento.</param>
    /// <returns>Operazione asincrona.</returns>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // ⚠️ ErasureReceipt NON va ignorata: non ha FK ed e' non-owned, quindi se sopravvivesse
        // il test di idempotenza dipenderebbe dall'ordine di esecuzione (ADR-0004).
        _respawner ??= await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude = ["dbo"],
                TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")],
            }).ConfigureAwait(false);

        await _respawner.ResetAsync(connection).ConfigureAwait(false);
    }

    /// <summary>Rilascia il database: <c>DROP DATABASE</c> dopo aver chiuso le connessioni aperte.</summary>
    /// <returns>Operazione asincrona.</returns>
    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var master = new SqlConnection(_masterConnectionString);
        await master.OpenAsync().ConfigureAwait(false);

        await ExecuteAsync(
            master,
            "ALTER DATABASE [" + DatabaseName + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
            + "DROP DATABASE [" + DatabaseName + "];",
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
