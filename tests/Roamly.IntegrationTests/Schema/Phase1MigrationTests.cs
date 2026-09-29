using Microsoft.EntityFrameworkCore;
using Roamly.IntegrationTests.Infrastructure;
using Roamly.Infrastructure.Persistence;

namespace Roamly.IntegrationTests.Schema;

/// <summary>
/// <b>Test C</b> di TESTING.md §5: le migration della Phase 1 si applicano davvero a un database
/// vuoto e <b>non hanno deriva</b> rispetto al modello che dichiarano di rappresentare.
/// <para>
/// 🔴 <b>Perche' non basta "MigrateAsync non ha lanciato".</b> Un test C che si ferma li' resta
/// verde anche quando qualcuno aggiunge una proprieta' al modello e dimentica la migration: lo
/// schema applicato sarebbe semplicemente <b>vecchio</b>, e nulla fallirebbe fino alla prima
/// query in produzione. L'asserzione che porta davvero il peso e'
/// <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade"/>.<c>HasPendingModelChanges()</c>,
/// che confronta lo <b>snapshot</b> della migration con il modello corrente: due cose distinte,
/// quindi confrontabili.
/// </para>
/// <para>
/// ⚠️ <c>GetPendingMigrationsAsync()</c> vuoto — la forma letterale di TESTING.md §5 — dice
/// un'altra cosa ancora: che tutte le migration <b>scritte</b> sono state applicate. Non sa nulla
/// del modello. Le due asserzioni convivono perche' coprono due fallimenti diversi.
/// </para>
/// <para>
/// Il soggetto e' <see cref="RoamlyDbContext"/>, cioe' <b>solo la Phase 1</b>
/// (CONTEXT.md §2.6). <c>FullSchemaDbContext</c> non ha migration e non deve averne: lo verifica
/// il test B, per un'altra via.
/// </para>
/// </summary>
/// <param name="sqlServer">Il container condiviso della run.</param>
[Collection(DatabaseCollection.Name)]
public sealed class Phase1MigrationTests(SqlServerFixture sqlServer)
{
    private const string OwnerColumn = "OwnerId";
    private const string IdColumn = "Id";
    private const string MigrationName = "InitialPhase1";

    [Fact]
    public async Task phase1_migrations_apply_to_an_empty_database()
    {
        var database = await sqlServer.Phase1MigratedAsync();

        Assert.True(
            database.Failure is null,
            "L'applicazione delle migration della Phase 1 a un database vuoto e' FALLITA. "
            + "Se il numero dell'eccezione e' 1785 la topologia delle FK e' in conflitto, e va "
            + "letto prima il test A (CascadePathAnalyzerTests), che la nomina.\n"
            + "Testo integrale dell'eccezione:\n" + database.Failure);

        var applied = await AppliedMigrationsAsync(database);

        Assert.True(
            applied.Any(m => m.EndsWith(MigrationName, StringComparison.Ordinal)),
            "__EFMigrationsHistory non contiene " + MigrationName + ". Migration registrate: "
            + (applied.Count == 0 ? "nessuna" : string.Join(", ", applied)));
    }

    [Fact]
    public async Task no_migration_is_left_pending_after_migrate()
    {
        var database = await sqlServer.Phase1MigratedAsync();
        await using var context = ContextOn(database);

        var pending = (await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ToList();

        Assert.True(
            pending.Count == 0,
            "Migration scritte ma non applicate:\n - " + string.Join("\n - ", pending));
    }

    [Fact]
    public async Task the_migrations_carry_no_pending_model_changes()
    {
        // 🔴 L'asserzione centrale del test C. Confronta lo snapshot della migration
        // (RoamlyDbContextModelSnapshot) con il modello che RoamlyDbContext costruisce oggi.
        // Diventa rossa esattamente nel caso che il test C esiste per intercettare: modello
        // cambiato, migration non rigenerata.
        var database = await sqlServer.Phase1MigratedAsync();
        await using var context = ContextOn(database);

        var hasChanges = context.Database.HasPendingModelChanges();

        Assert.False(
            hasChanges,
            "Il modello di RoamlyDbContext e' cambiato senza che sia stata generata la migration "
            + "corrispondente: lo schema applicato e' piu' vecchio del modello.\n"
            + "Rigenerare con:\n"
            + "  dotnet ef migrations add <Nome> --project src/Roamly.Infrastructure "
            + "--startup-project src/Roamly.Infrastructure --context RoamlyDbContext "
            + "--output-dir Persistence/Migrations");
    }

    [Fact]
    public async Task migrated_primary_keys_are_owner_id_then_id_and_clustered()
    {
        // La migration e' uno scaffolding: puo' divergere da cio' che il modello dichiara.
        // Qui lo si chiede al motore, sullo schema che le migration hanno davvero creato — non
        // su quello di EnsureCreated, che e' il soggetto del test B.
        var database = await sqlServer.Phase1MigratedAsync();
        var columns = await SchemaCatalog.PrimaryKeyColumnsAsync(database.ConnectionString);
        var owned = await OwnedTablesAsync(database.ConnectionString);

        var byTable = columns
            .Where(c => owned.Contains(c.TableName))
            .GroupBy(c => c.TableName, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            byTable.Count > 0,
            "Nessuna tabella owned trovata sul database migrato: l'asserzione sarebbe verde a vuoto.");

        var offenders = new List<string>();
        foreach (var table in byTable)
        {
            var shape = table.OrderBy(c => c.KeyOrdinal).Select(c => c.ColumnName).ToList();

            if (shape.Count != 2 || shape[0] != OwnerColumn || shape[1] != IdColumn)
            {
                offenders.Add(table.Key + ": PK (" + string.Join(", ", shape) + ") invece di (OwnerId, Id)");
            }

            if (!table.All(c => c.IsClustered))
            {
                offenders.Add(table.Key + ": la PK creata dalla migration non e' CLUSTERED");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Lo schema prodotto dalle migration non rispetta ADR-0008 (R26):\n - "
            + string.Join("\n - ", offenders));
    }

    [Fact]
    public async Task migrated_foreign_keys_reference_columns_in_primary_key_order()
    {
        // R43 (ADR-0008) sul percorso delle migration. Il Blocco 4 lo ha verificato su
        // EnsureCreated; che lo scaffolding emetta lo stesso ordine e' un fatto distinto.
        var database = await sqlServer.Phase1MigratedAsync();

        var foreignKeys = await SchemaCatalog.ForeignKeyColumnsAsync(database.ConnectionString);
        var primaryKeys = await SchemaCatalog.PrimaryKeyColumnsAsync(database.ConnectionString);

        var pkOrder = primaryKeys
            .GroupBy(p => p.TableName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(p => p.KeyOrdinal).Select(p => p.ColumnName).ToList(),
                StringComparer.Ordinal);

        var composite = foreignKeys
            .Where(c => SchemaCatalog.IsRoamlyTable(c.ParentTable) && SchemaCatalog.IsRoamlyTable(c.ReferencedTable))
            .GroupBy(c => c.ConstraintName, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.True(
            composite.Count > 0,
            "Nessuna FK composita fra tabelle di Roamly sul database migrato: R43 sarebbe verde a "
            + "vuoto. La Phase 1 ne prevede quattro (Camper -> Equipment, MaintenanceItem, "
            + "OdometerReading; MaintenanceItem -> MaintenanceLog).");

        var offenders = new List<string>();
        foreach (var constraint in composite)
        {
            var referencedTable = constraint.First().ReferencedTable;
            var declared = constraint.OrderBy(c => c.Ordinal).Select(c => c.ReferencedColumn).ToList();
            var expected = pkOrder.TryGetValue(referencedTable, out var pk) ? pk : [];

            if (!declared.SequenceEqual(expected, StringComparer.Ordinal))
            {
                offenders.Add(
                    constraint.Key + ": REFERENCES " + referencedTable
                    + " (" + string.Join(", ", declared) + ") ma la PK e' ("
                    + string.Join(", ", expected) + ")");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "R43 violata dallo schema prodotto dalle migration.\n"
            + "🔴 NON improvvisare una correzione: ADR-0008 §*Piano B* descrive che cosa fare.\n - "
            + string.Join("\n - ", offenders));
    }

    /// <summary>
    /// Contesto sul database migrato. Serve <see cref="ThrowingCurrentUser"/> per la stessa ragione della
    /// factory di design-time: fuori da una richiesta HTTP non esiste un utente corrente, e qui si
    /// interroga il catalogo delle migration, non i dati.
    /// </summary>
    private static RoamlyDbContext ContextOn(Phase1Database database)
    {
        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options;

        return new RoamlyDbContext(options, ThrowingCurrentUser.Instance);
    }

    private static async Task<IReadOnlyList<string>> AppliedMigrationsAsync(Phase1Database database)
    {
        await using var context = ContextOn(database);
        return [.. await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)];
    }

    /// <summary>
    /// Le tabelle owned dedotte dal motore: quelle che hanno una colonna <c>OwnerId</c>.
    /// <c>ErasureReceipts</c> non lo e' per costruzione (R10, ADR-0004) e resta giustamente fuori.
    /// </summary>
    private static async Task<HashSet<string>> OwnedTablesAsync(string connectionString)
    {
        var columns = await SchemaCatalog.ColumnTypesAsync(connectionString);

        return columns
            .Where(c => SchemaCatalog.IsRoamlyTable(c.TableName) && c.ColumnName == OwnerColumn)
            .Select(c => c.TableName)
            .ToHashSet(StringComparer.Ordinal);
    }
}
