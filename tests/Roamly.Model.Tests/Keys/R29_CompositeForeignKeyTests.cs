namespace Roamly.Model.Tests.Keys;

/// <summary>
/// <b>R29 (ADR-0008)</b>: ogni FK fra due entita' owned e' composita, ha <c>OwnerId</c> come prima
/// colonna, e il suo <c>PrincipalKey</c> e' la <b>PK</b> del principal — mai una alternate key.
/// E' la forma che rende il "morso" di R4 (coerenza gerarchica) un vincolo di database e non una
/// convenzione applicativa.
/// </summary>
/// <param name="model">Modello costruito una volta per assembly.</param>
public sealed class R29_CompositeForeignKeyTests(ModelFixture model)
{
    [Fact]
    public void Every_foreign_key_between_owned_entities_is_composite_and_targets_the_primary_key()
    {
        var violations = new List<string>();

        foreach (var entityType in model.OwnedEntityTypes)
        {
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                if (!ModelFixture.IsOwned(foreignKey.PrincipalEntityType))
                {
                    // Le FK OwnerId -> AspNetUsers hanno un principal non owned: sono coperte
                    // dalla convenzione e verificate altrove (NoAction, R40 e test B).
                    continue;
                }

                var columns = foreignKey.Properties.Select(p => p.Name).ToArray();
                var label = $"{entityType.ClrType.Name} -> {foreignKey.PrincipalEntityType.ClrType.Name} ({string.Join(", ", columns)})";

                if (columns.Length < 2)
                {
                    violations.Add($"{label}: FK non composita.");
                }

                if (columns.Length == 0 || columns[0] != "OwnerId")
                {
                    violations.Add($"{label}: la prima colonna non e' OwnerId.");
                }

                if (!foreignKey.PrincipalKey.IsPrimaryKey())
                {
                    violations.Add(
                        $"{label}: punta a una chiave alternata ({string.Join(", ", foreignKey.PrincipalKey.Properties.Select(p => p.Name))}), non alla PK.");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "FK non conformi a R29 (ADR-0008):\n  " + string.Join("\n  ", violations)
            + "\nConseguenza: un figlio puo' agganciarsi al padre di un altro utente senza che il "
            + "database lo impedisca — la coerenza gerarchica torna a dipendere dal codice applicativo.");
    }
}
