using Microsoft.EntityFrameworkCore;

namespace Roamly.Model.Tests.Keys;

/// <summary>
/// <b>R26 (ADR-0008)</b>: ogni entita' owned ha una PK di <b>esattamente due</b> proprieta',
/// nell'ordine <c>(OwnerId, Id)</c>, dichiarata <c>IsClustered(true)</c>.
/// <para>
/// 🔴 <c>IsClustered()</c> e' leggibile solo su <see cref="Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel"/>:
/// su <c>DbContext.Model</c> lancia <c>InvalidOperationException</c> (TESTING.md §8.2). La fixture
/// legge il modello giusto; se qualcuno la cambiasse, questo verificatore esploderebbe rumorosamente
/// invece di diventare verde in silenzio.
/// </para>
/// <para>R42 e' coperta da qui: la clustering key e' <c>(OwnerId, Id)</c> e nient'altro.</para>
/// </summary>
/// <param name="model">Modello costruito una volta per assembly.</param>
public sealed class R26_PrimaryKeyShapeTests(ModelFixture model)
{
    [Fact]
    public void Every_owned_entity_has_a_two_column_primary_key_ordered_owner_then_id()
    {
        var violations = new List<string>();

        foreach (var entityType in model.OwnedEntityTypes)
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey is null)
            {
                violations.Add($"{entityType.ClrType.Name}: nessuna chiave primaria.");
                continue;
            }

            var columns = primaryKey.Properties.Select(p => p.Name).ToArray();
            if (columns.Length != 2 || columns[0] != "OwnerId" || columns[1] != "Id")
            {
                violations.Add(
                    $"{entityType.ClrType.Name}: PK = ({string.Join(", ", columns)}), attesa (OwnerId, Id).");
            }
        }

        Assert.True(
            violations.Count == 0,
            "PK non conformi a R26 (ADR-0008):\n  " + string.Join("\n  ", violations)
            + "\nConseguenza: le FK composite non possono referenziare la PK e tornerebbero le "
            + "chiavi alternate che ADR-0008 ha eliminato.");
    }

    [Fact]
    public void Every_owned_entity_has_a_clustered_primary_key()
    {
        var violations = new List<string>();

        foreach (var entityType in model.OwnedEntityTypes)
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey is null)
            {
                violations.Add($"{entityType.ClrType.Name}: nessuna chiave primaria.");
                continue;
            }

            // Non si usa FindAnnotation(...) is not null come guardia permissiva: sarebbe verde
            // su tutto se il modello letto fosse quello read-optimized (TESTING.md §8.2).
            if (primaryKey.IsClustered() != true)
            {
                violations.Add(
                    $"{entityType.ClrType.Name}: PK non CLUSTERED (SqlServer:Clustered = {primaryKey.IsClustered()?.ToString() ?? "null"}).");
            }
        }

        Assert.True(
            violations.Count == 0,
            "PK non clusterizzate, R26 (ADR-0008):\n  " + string.Join("\n  ", violations)
            + "\nConseguenza: l'ordinamento fisico delle righe non segue (OwnerId, Id) e la ragione "
            + "per cui l'Id e' un COMB decade.");
    }
}
