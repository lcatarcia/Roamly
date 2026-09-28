using System.Diagnostics;

namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>
/// Verifica che il meccanismo di isolamento G1 <b>funzioni davvero</b>, invece di essere
/// codice consegnato e mai eseguito.
/// <para>
/// 🔴 Il valore non e' "Respawn cancella le righe": e' che Respawn <b>riesce a calcolare
/// l'ordine di cancellazione</b> sul grafo reale delle FK. E' lo stesso ordine che
/// <c>AccountErasureJob</c> dovra' percorrere (R9, ADR-0004): se qui fallisse, il segnale non
/// sarebbe "Respawn e' fragile" ma "la topologia ha un ciclo".
/// </para>
/// </summary>
/// <param name="sqlServer">Il container condiviso della run.</param>
[Collection(DatabaseCollection.Name)]
public sealed class DatabaseLeaseTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task respawn_can_reset_the_full_schema_database()
    {
        var database = await sqlServer.FullSchemaAsync();

        var stopwatch = Stopwatch.StartNew();
        await database.Lease.ResetAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        sqlServer.Measure("Respawn.ResetAsync sullo schema completo (database vuoto)", stopwatch.Elapsed);

        Assert.True(true, "Respawn ha calcolato l'ordine di cancellazione sul grafo reale delle FK.");
    }

    [Fact]
    public async Task a_lease_creates_and_releases_an_isolated_database()
    {
        var lease = await sqlServer.CreateEmptyDatabaseAsync("CREATE DATABASE vuoto (container caldo)");

        Assert.StartsWith("roamly_", lease.DatabaseName, StringComparison.Ordinal);
        Assert.Contains(lease.DatabaseName, lease.ConnectionString, StringComparison.Ordinal);

        await lease.DisposeAsync();
    }
}
