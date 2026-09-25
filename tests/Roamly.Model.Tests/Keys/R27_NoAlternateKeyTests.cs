namespace Roamly.Model.Tests.Keys;

/// <summary>
/// <b>R27 (ADR-0008)</b>: nessuna alternate key su alcuna entita' owned. E' la regola che protegge
/// il contenuto di ADR-0008: un <c>HasAlternateKey</c> scritto per abitudine davanti a un errore di
/// FK ricrea l'indice <c>UNIQUE (Id, OwnerId)</c> che la decisione ha eliminato, e il modello si
/// costruisce lo stesso (CONTEXT.md §5.3).
/// </summary>
/// <param name="model">Modello costruito una volta per assembly.</param>
public sealed class R27_NoAlternateKeyTests(ModelFixture model)
{
    [Fact]
    public void No_owned_entity_declares_a_key_other_than_its_primary_key()
    {
        var violations = new List<string>();

        foreach (var entityType in model.OwnedEntityTypes)
        {
            var keys = entityType.GetKeys().ToList();
            var alternates = keys.Where(k => !k.IsPrimaryKey()).ToList();

            if (keys.Count != 1 || alternates.Count != 0)
            {
                var described = alternates.Select(k => "(" + string.Join(", ", k.Properties.Select(p => p.Name)) + ")");
                violations.Add(
                    $"{entityType.ClrType.Name}: {keys.Count} chiavi, alternate = {string.Join(" ", described)}.");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Chiavi alternate rientrate nel modello, R27 (ADR-0008):\n  " + string.Join("\n  ", violations)
            + "\nConseguenza: torna un indice UNIQUE per tabella-padre, con il suo costo di scrittura, "
            + "e il requisito che ADR-0008 ha eliminato rientra senza che nulla fallisca.");
    }
}
