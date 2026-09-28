using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Schema;

/// <summary>
/// <b>R43 (ADR-0008)</b>: ogni clausola <c>REFERENCES</c> elenca le colonne <b>nell'ordine della
/// PK <c>(OwnerId, Id)</c></b>.
/// <para>
/// 🔴 E' il <b>solo punto empirico mai verificato</b> dell'intero ADR-0008: fin qui era una
/// assunzione su come EF Core emette il DDL delle FK composite. Se l'ordine divergesse, il motore
/// aggancerebbe <c>OwnerId</c> a <c>Id</c> — e i test di ownership potrebbero restare verdi su un
/// database che collega righe sbagliate.
/// </para>
/// <para>
/// Si interroga <c>sys.foreign_key_columns</c> confrontando la sequenza di
/// <c>referenced_column_id</c> con l'ordine reale della chiave primaria referenziata.
/// </para>
/// </summary>
/// <param name="sqlServer">Il container condiviso della run.</param>
[Collection(DatabaseCollection.Name)]
public sealed class R43_ForeignKeyColumnOrderTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task every_references_clause_lists_columns_in_primary_key_order()
    {
        var database = await sqlServer.FullSchemaAsync();

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
            "Nessuna FK composita fra tabelle di Roamly: R43 sarebbe verde a vuoto. "
            + "Lo schema completo ne prevede diverse (Camper->MaintenanceItem, Trip->TripStop, ...).");

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
            "R43 violata: l'ordine delle colonne in REFERENCES non segue l'ordine della PK.\n"
            + "🔴 NON improvvisare una correzione: ADR-0008 §*Piano B* descrive che cosa fare.\n - "
            + string.Join("\n - ", offenders));
    }
}
