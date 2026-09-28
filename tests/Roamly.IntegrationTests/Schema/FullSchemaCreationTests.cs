using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Schema;

/// <summary>
/// <b>Test B</b> di TESTING.md §5: applica <c>FullSchemaDbContext</c> a un <b>SQL Server vero</b>
/// con <c>EnsureCreatedAsync()</c> e verifica che il motore non sollevi <b>1785</b>, piu' le
/// cinque asserzioni strutturali.
/// <para>
/// Il suo valore proprio non e' trovare l'errore 1785 — quello lo trova il test A a L0 in 10 ms —
/// ma <b>dimostrare che il test A dice il vero</b>. Sono indipendenti per costruzione: se
/// discordano, l'analizzatore e' sbagliato e va corretto prima di proseguire.
/// </para>
/// </summary>
/// <param name="sqlServer">Il container condiviso della run.</param>
[Collection(DatabaseCollection.Name)]
public sealed class FullSchemaCreationTests(SqlServerFixture sqlServer)
{
    private const string OwnerColumn = "OwnerId";
    private const string IdColumn = "Id";
    private const string IdentityUsersTable = "AspNetUsers";

    [Fact]
    public async Task full_schema_is_created_on_real_sql_server_without_error_1785()
    {
        var database = await sqlServer.FullSchemaAsync();

        Assert.True(
            database.Failure is null,
            "EnsureCreated dello schema completo e' FALLITO su SQL Server reale. Se il numero e' 1785, "
            + "il test A (CascadePathAnalyzerTests) e' verde su una topologia che il motore rifiuta: "
            + "l'analizzatore e' sbagliato e va corretto PRIMA di proseguire (ADR-0009 §9, passo 14).\n"
            + "Testo integrale dell'eccezione:\n" + database.Failure);
    }

    [Fact]
    public async Task every_owner_foreign_key_to_identity_is_no_action()
    {
        // Asserzione 1 (DATA.md §6): la cancellazione dell'account e' un job esplicito e ordinato,
        // non un cascade del motore. Un solo ON DELETE CASCADE qui riaprirebbe R10.
        var database = await sqlServer.FullSchemaAsync();
        var columns = await SchemaCatalog.ForeignKeyColumnsAsync(database.ConnectionString);

        var offenders = columns
            .Where(c => c.ReferencedTable == IdentityUsersTable && SchemaCatalog.IsRoamlyTable(c.ParentTable))
            .Where(c => c.DeleteAction != "NO_ACTION")
            .Select(c => c.ParentTable + "." + c.ConstraintName + " = " + c.DeleteAction)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "FK verso AspNetUsers con azione diversa da NO ACTION:\n - " + string.Join("\n - ", offenders));
    }

    [Fact]
    public async Task every_primary_key_is_owner_id_then_id_and_clustered()
    {
        // Asserzione 2 (ADR-0008, R26 e R42): PK (OwnerId, Id) CLUSTERED, e CreatedAtUtc FUORI
        // dalla clustering key. Il Blocco 3 lo ha verificato a L0 sui metadati; qui lo conferma
        // il motore, che e' l'unica autorita' su che cosa e' stato davvero creato.
        var database = await sqlServer.FullSchemaAsync();
        var columns = await SchemaCatalog.PrimaryKeyColumnsAsync(database.ConnectionString);
        var owned = await OwnedTablesAsync(database.ConnectionString);

        var byTable = columns
            .Where(c => owned.Contains(c.TableName))
            .GroupBy(c => c.TableName)
            .ToList();

        Assert.True(byTable.Count > 0, "Nessuna tabella di Roamly trovata: lo schema non e' stato creato.");

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
                offenders.Add(table.Key + ": la PK non e' CLUSTERED");
            }
        }

        Assert.True(offenders.Count == 0, "Chiavi primarie fuori forma sul database reale:\n - " + string.Join("\n - ", offenders));
    }

    [Fact]
    public async Task no_alternate_key_exists()
    {
        // Asserzione 3 (ADR-0008): le chiavi alternate sono state eliminate tutte. Una UNIQUE
        // superstite e' un secondo bersaglio per le FK, e riaprirebbe R27.
        var database = await sqlServer.FullSchemaAsync();
        var constraints = await SchemaCatalog.UniqueConstraintsAsync(database.ConnectionString);

        var offenders = constraints
            .Where(c => SchemaCatalog.IsRoamlyTable(c.Table))
            .Select(c => c.Table + "." + c.Constraint)
            .ToList();

        Assert.True(offenders.Count == 0, "Chiavi alternate presenti sul database reale:\n - " + string.Join("\n - ", offenders));
    }

    [Fact]
    public async Task every_child_foreign_key_is_composite_and_carries_owner_id()
    {
        // Asserzione 4 (ADR-0003, il "morso" di R4 pagato una volta sola): una FK figlia che non
        // porta OwnerId consentirebbe a una riga di appendersi a un padre di un altro utente.
        var database = await sqlServer.FullSchemaAsync();
        var columns = await SchemaCatalog.ForeignKeyColumnsAsync(database.ConnectionString);

        var offenders = columns
            .Where(c => SchemaCatalog.IsRoamlyTable(c.ParentTable) && SchemaCatalog.IsRoamlyTable(c.ReferencedTable))
            .GroupBy(c => c.ConstraintName)
            .Where(g => g.Count() != 2 || g.All(c => c.ParentColumn != OwnerColumn))
            .Select(g => g.Key + " su " + g.First().ParentTable
                + " (" + string.Join(", ", g.OrderBy(c => c.Ordinal).Select(c => c.ParentColumn)) + ")")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "FK figlie non composite o senza OwnerId:\n - " + string.Join("\n - ", offenders));
    }

    [Fact]
    public async Task money_and_coordinates_have_the_declared_decimal_shape()
    {
        // Asserzione 5 (CONTEXT.md §2.1): i complex type non sono decimal nudi, e la scala e'
        // quella dichiarata. Una scala sbagliata sui soldi si nota in produzione, non qui.
        var database = await sqlServer.FullSchemaAsync();
        var columns = await SchemaCatalog.ColumnTypesAsync(database.ConnectionString);

        var expected = new Dictionary<string, (int Precision, int Scale)>(StringComparer.Ordinal)
        {
            ["Amount"] = (19, 4),
            ["CostAmount"] = (19, 4),
            ["Latitude"] = (8, 6),
            ["Longitude"] = (9, 6),
        };

        var interesting = columns
            .Where(c => SchemaCatalog.IsRoamlyTable(c.TableName) && expected.ContainsKey(c.ColumnName))
            .ToList();

        Assert.True(
            interesting.Count > 0,
            "Nessuna colonna di Money o Coordinates trovata: il test sarebbe verde a vuoto.");

        var offenders = interesting
            .Where(c => c.TypeName != "decimal"
                || c.Precision != expected[c.ColumnName].Precision
                || c.Scale != expected[c.ColumnName].Scale)
            .Select(c => c.TableName + "." + c.ColumnName + " = " + c.TypeName
                + "(" + c.Precision + "," + c.Scale + ") invece di decimal("
                + expected[c.ColumnName].Precision + "," + expected[c.ColumnName].Scale + ")")
            .ToList();

        Assert.True(offenders.Count == 0, "Tipi decimali fuori specifica:\n - " + string.Join("\n - ", offenders));
    }

    /// <summary>
    /// Le tabelle <b>owned</b>, dedotte dal database reale: quelle che hanno una colonna
    /// <c>OwnerId</c>.
    /// <para>
    /// ⚠️ <c>ErasureReceipts</c> non e' owned per costruzione (R10, ADR-0004: la ricevuta di
    /// cancellazione non puo' avere una FK verso l'utente che si sta cancellando), quindi la sua
    /// PK e' <c>(Id)</c> e non deve far fallire un'asserzione di ADR-0008. Dedurre l'elenco dal
    /// motore invece di scriverlo a mano evita sia il falso rosso sia la lista che marcisce.
    /// </para>
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
