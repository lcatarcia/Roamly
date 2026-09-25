using Microsoft.EntityFrameworkCore;

namespace Roamly.Model.Tests.Keys;

/// <summary>
/// <b>R40 (ADR-0008)</b>: nessuna FK del modello usa <c>DeleteBehavior.SetNull</c> o
/// <c>SetDefault</c>. Sono azioni referenziali a tutti gli effetti e <b>contano</b> ai fini
/// dell'errore 1785 esattamente come <c>Cascade</c>: usarle sulle FK opzionali
/// (<c>Trip -> Expense</c>, <c>SavedPlace -> TripStop</c>, <c>TripStop -> JournalEntry</c>)
/// riaprirebbe i casi (b) e (c) di CONTEXT.md §5.2.
/// <para>
/// Il controllo copre <b>tutte</b> le FK del modello, Identity compresa: l'errore 1785 non
/// distingue fra tabelle nostre e tabelle del framework.
/// </para>
/// <para>
/// ⚠️ <c>DeleteBehavior</c> di EF Core <b>non ha</b> un valore <c>SetDefault</c>: la meta' della
/// regola che lo riguarda non e' esprimibile su <c>IModel</c> e resta verificabile solo sul DB reale.
/// Al suo posto si controlla <c>ClientSetNull</c>, che e' il <b>default di EF per una relazione
/// opzionale non configurata</b> — cioe' esattamente il modo in cui la violazione entrerebbe per
/// distrazione. E' anche la forma chiesta da TESTING.md §6.2.
/// </para>
/// </summary>
/// <param name="model">Modello costruito una volta per assembly.</param>
public sealed class R40_NoSetNullDeleteBehaviorTests(ModelFixture model)
{
    [Fact]
    public void No_foreign_key_uses_set_null_or_set_default()
    {
        var violations = model.FullSchemaModel.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Distinct()
            .Where(fk => fk.DeleteBehavior is DeleteBehavior.SetNull or DeleteBehavior.ClientSetNull)
            .Select(fk =>
                $"{fk.DeclaringEntityType.ClrType.Name} -> {fk.PrincipalEntityType.ClrType.Name} "
                + $"({string.Join(", ", fk.Properties.Select(p => p.Name))}): {fk.DeleteBehavior}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "FK con SetNull, R40 (ADR-0008):\n  " + string.Join("\n  ", violations)
            + "\nConseguenza: la FK diventa un cammino di azione referenziale, l'analizzatore 1785 "
            + "puo' trovare una seconda strada verso la stessa tabella, e la dimostrazione di "
            + "CONTEXT.md §5.2 salta. L'unica uscita e' NoAction piu' sganciamento nell'handler.");
    }
}
