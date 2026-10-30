using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;
using Roamly.Infrastructure.Persistence;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(Roamly.IntegrationTests.Infrastructure.SqlServerFixture))]

namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>
/// <b>Un solo container per run di assembly</b> (ADR-0009, decisione 2). E' l'unico progetto che
/// possiede il container: con piu' assembly che ne avviano uno ciascuno si moltiplicherebbero i
/// SQL Server da 2 GB, seconda sorgente di flakiness prevista da TESTING.md §4.3.
/// <para>
/// ⚠️ <b>Nessun test conosce il container.</b> La fixture espone una connection string e delle
/// lease: e' cio' che rende basso il costo di passare a <c>services:</c> o ad altro provisioning
/// (ADR-0009 §*Costo di inversione*).
/// </para>
/// </summary>
public class SqlServerFixture : IAsyncLifetime
{
    public const string Phase1MigrationTarget = "20261006160121_AddLoginCounterKinds";

    private readonly MsSqlContainer _container;
    private readonly IContainer _azuriteContainer;
    private readonly string _azuriteAccountName;
    private readonly string _azuriteAccountKey;
    private readonly List<(string Label, double Milliseconds)> _timings = [];
    private readonly Lazy<Task<FullSchemaDatabase>> _fullSchema;
    private readonly Lazy<Task<Phase1Database>> _phase1;
    private readonly Lazy<Task<ApplicationDatabase>> _application;

    /// <summary>Costruisce la fixture con il profilo di risorse dei test.</summary>
    public SqlServerFixture()
        : this(SqlServerResources.ForTests)
    {
    }

    /// <summary>
    /// Costruisce la fixture con un profilo di risorse esplicito (R39: i benchmark useranno 4 GB).
    /// <para>
    /// ⚠️ <b>protected, non public, di proposito</b>: xUnit v3 pretende che una assembly fixture
    /// abbia un solo costruttore pubblico con firma nota (xUnit3005). Il riuso da parte di
    /// <c>Roamly.Benchmarks</c> avviene per <b>derivazione</b>, non per parametro.
    /// </para>
    /// </summary>
    /// <param name="resources">Profilo di risorse del container.</param>
    protected SqlServerFixture(SqlServerResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        Resources = resources;
        _azuriteAccountName = "roamlytest";
        _azuriteAccountKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _fullSchema = new Lazy<Task<FullSchemaDatabase>>(CreateFullSchemaAsync);
        _phase1 = new Lazy<Task<Phase1Database>>(MigratePhase1Async);
        _application = new Lazy<Task<ApplicationDatabase>>(MigrateApplicationAsync);

        _container = new MsSqlBuilder(new DockerImage(SqlServerImage.Tag))
            .WithEnvironment(
                "MSSQL_MEMORY_LIMIT_MB",
                resources.SqlServerMemoryLimitMb.ToString(CultureInfo.InvariantCulture))
            .WithCreateParameterModifier(parameters =>
            {
                parameters.HostConfig ??= new Docker.DotNet.Models.HostConfig();
                parameters.HostConfig.Memory = resources.ContainerMemoryBytes;
            })

            // La wait strategy NON e' un retry (R37): attende una condizione di prontezza del
            // motore, non riprova un'operazione fallita. Quella di MsSqlBuilder e' gia' corretta.
            .Build();
        _azuriteContainer = new ContainerBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0")
            .WithEnvironment("AZURITE_ACCOUNTS", $"{_azuriteAccountName}:{_azuriteAccountKey}")
            .WithCommand(
                "azurite",
                "--blobHost",
                "0.0.0.0",
                "--location",
                "/data",
                "--skipApiVersionCheck")
            .WithPortBinding(10000, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(10000))
            .Build();
    }

    /// <summary>Profilo di risorse in uso.</summary>
    public SqlServerResources Resources { get; }

    /// <summary>Connection string verso <c>master</c>. Da qui nascono le lease.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>Connection string isolata verso il servizio Blob Azurite del run corrente.</summary>
    public string JournalImageStorageConnectionString =>
        $"DefaultEndpointsProtocol=http;AccountName={_azuriteAccountName};AccountKey={_azuriteAccountKey};"
        + $"BlobEndpoint=http://{_azuriteContainer.Hostname}:{_azuriteContainer.GetMappedPublicPort(10000)}/"
        + $"{_azuriteAccountName};";

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        await _container.StartAsync().ConfigureAwait(false);
        Measure("Avvio del container fino alla prontezza (immagine gia' in locale)", stopwatch.Elapsed);

        stopwatch.Restart();
        await _azuriteContainer.StartAsync().ConfigureAwait(false);
        var blobService = new BlobServiceClient(JournalImageStorageConnectionString);
        await blobService.GetBlobContainerClient("roamly-journal-images")
            .CreateIfNotExistsAsync(PublicAccessType.None).ConfigureAwait(false);
        Measure("Avvio Azurite e creazione container privato", stopwatch.Elapsed);
    }

    /// <summary>Crea un database vuoto sul container e ne misura il costo.</summary>
    /// <param name="label">Etichetta con cui annotare la misura.</param>
    /// <returns>La lease sul database creato.</returns>
    public async Task<DatabaseLease> CreateEmptyDatabaseAsync(string label = "CREATE DATABASE vuoto (container caldo)")
    {
        var stopwatch = Stopwatch.StartNew();
        var lease = await DatabaseLease.CreateAsync(ConnectionString).ConfigureAwait(false);
        Measure(label, stopwatch.Elapsed);

        return lease;
    }

    /// <summary>
    /// Database con lo <b>schema completo</b> creato una volta per run: le asserzioni strutturali
    /// del test B e di R43 leggono tutte le stesse tabelle, e ricrearle per ogni <c>[Fact]</c>
    /// pagherebbe tre secondi a testa senza aggiungere garanzia.
    /// </summary>
    /// <returns>Lo stato della creazione, riuscita o fallita.</returns>
    public Task<FullSchemaDatabase> FullSchemaAsync() => _fullSchema.Value;

    /// <summary>
    /// Database su cui sono state applicate le <b>migration della Phase 1</b>, una volta per run.
    /// E' il soggetto del <b>test C</b> (TESTING.md §5): schema prodotto dalle migration, non da
    /// <c>EnsureCreated</c>. Sono due percorsi diversi e devono restare due database diversi,
    /// altrimenti il confronto fra i due non significherebbe piu' nulla.
    /// </summary>
    /// <returns>Lo stato dell'applicazione, riuscita o fallita.</returns>
    public Task<Phase1Database> Phase1MigratedAsync() => _phase1.Value;

    /// <summary>Database su cui sono state applicate tutte le migration del modello applicativo corrente.</summary>
    public Task<ApplicationDatabase> ApplicationMigratedAsync() => _application.Value;

    /// <summary>Annota una misura di tempo, riversata su file a fine run (Passo 15 di ADR-0009 §9).</summary>
    /// <param name="label">Che cosa e' stato misurato.</param>
    /// <param name="elapsed">Durata osservata.</param>
    public void Measure(string label, TimeSpan elapsed)
    {
        lock (_timings)
        {
            _timings.Add((label, elapsed.TotalMilliseconds));
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_fullSchema.IsValueCreated)
        {
            var schema = await _fullSchema.Value.ConfigureAwait(false);
            await schema.Lease.DisposeAsync().ConfigureAwait(false);
        }

        if (_phase1.IsValueCreated)
        {
            var migrated = await _phase1.Value.ConfigureAwait(false);
            await migrated.Lease.DisposeAsync().ConfigureAwait(false);
        }

        if (_application.IsValueCreated)
        {
            var migrated = await _application.Value.ConfigureAwait(false);
            await migrated.Lease.DisposeAsync().ConfigureAwait(false);
        }

        await _container.DisposeAsync().ConfigureAwait(false);
        await _azuriteContainer.DisposeAsync().ConfigureAwait(false);

        WriteTimings();

        GC.SuppressFinalize(this);
    }

    private void WriteTimings()
    {
        // ⚠️ Console.WriteLine non e' visibile con Microsoft.Testing.Platform, nemmeno con
        // --output Detailed: le misure del Passo 15 si leggono da questo file.
        var directory = Path.Combine(RepositoryRoot(), "TestResults");
        Directory.CreateDirectory(directory);

        var lines = _timings
            .Select(t => t.Label + " = "
                + t.Milliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms")
            .ToList();

        File.WriteAllLines(Path.Combine(directory, "blocco4-tempi.txt"), lines);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Roamly.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }

    private async Task<Phase1Database> MigratePhase1Async()
    {
        var lease = await CreateEmptyDatabaseAsync("CREATE DATABASE per le migration della Phase 1").ConfigureAwait(false);

        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(lease.ConnectionString)
            .Options;

        await using var context = new RoamlyDbContext(options, ThrowingCurrentUser.Instance);

        var stopwatch = Stopwatch.StartNew();
        var failure = await Record.ExceptionAsync(
            () => context.Database.MigrateAsync(Phase1MigrationTarget)).ConfigureAwait(false);
        stopwatch.Stop();

        Measure(
            failure is null
                ? "Migration Phase 1 su database vuoto"
                : "Migration Phase 1 su database vuoto, FALLITA",
            stopwatch.Elapsed);

        return new Phase1Database(lease, failure);
    }

    private async Task<ApplicationDatabase> MigrateApplicationAsync()
    {
        var lease = await CreateEmptyDatabaseAsync("CREATE DATABASE per le migration applicative").ConfigureAwait(false);

        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(lease.ConnectionString)
            .Options;

        await using var context = new RoamlyDbContext(options, ThrowingCurrentUser.Instance);

        var stopwatch = Stopwatch.StartNew();
        var failure = await Record.ExceptionAsync(() => context.Database.MigrateAsync()).ConfigureAwait(false);
        stopwatch.Stop();

        Measure(
            failure is null
                ? "Migration applicative da database vuoto"
                : "Migration applicative da database vuoto, FALLITA",
            stopwatch.Elapsed);

        return new ApplicationDatabase(lease, failure);
    }

    private async Task<FullSchemaDatabase> CreateFullSchemaAsync()
    {
        var lease = await CreateEmptyDatabaseAsync("CREATE DATABASE per lo schema completo").ConfigureAwait(false);

        var options = new DbContextOptionsBuilder<FullSchemaDbContext>()
            .UseSqlServer(lease.ConnectionString)
            .Options;

        await using var context = new FullSchemaDbContext(options, ThrowingCurrentUser.Instance);

        var stopwatch = Stopwatch.StartNew();
        var failure = await Record.ExceptionAsync(() => context.Database.EnsureCreatedAsync()).ConfigureAwait(false);
        stopwatch.Stop();

        Measure(
            failure is null
                ? "EnsureCreated dello schema completo (15 entita' + Identity)"
                : "EnsureCreated dello schema completo, FALLITO",
            stopwatch.Elapsed);

        return new FullSchemaDatabase(lease, failure);
    }
}
