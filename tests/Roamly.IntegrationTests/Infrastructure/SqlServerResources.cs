namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>
/// Risorse assegnate al container SQL Server. Parametrizzate <b>oggi</b> perche'
/// <c>Roamly.Benchmarks</c> (R39, ADR-0009) riusera' la stessa fixture con 4 GB e senza Respawn:
/// la parametrizzazione costa dieci righe ora e una riscrittura della fixture dopo.
/// </summary>
/// <param name="ContainerMemoryBytes">Tetto di memoria del container, applicato all'host config.</param>
/// <param name="SqlServerMemoryLimitMb">Tetto interno del motore (<c>MSSQL_MEMORY_LIMIT_MB</c>).</param>
public sealed record SqlServerResources(long ContainerMemoryBytes, int SqlServerMemoryLimitMb)
{
    /// <summary>Profilo dei test: 2 GB, il valore dichiarato in ADR-0009 §*Tradeoff*.</summary>
    public static SqlServerResources ForTests { get; } = new(2L * 1024 * 1024 * 1024, 1536);

    /// <summary>Profilo dei benchmark di @oracle: 4 GB. Non usato finche' il progetto non esiste.</summary>
    public static SqlServerResources ForBenchmarks { get; } = new(4L * 1024 * 1024 * 1024, 3072);
}
