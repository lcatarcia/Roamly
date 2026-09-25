using Microsoft.EntityFrameworkCore;

namespace Roamly.Model.Tests.Keys;

/// <summary>
/// <b>R28 (ADR-0008)</b>: l'<c>Id</c> e' generato dal dominio. <c>Id</c> e <c>OwnerId</c> hanno
/// <c>ValueGenerated == Never</c>, nessun default SQL (<c>NEWID()</c>, <c>NEWSEQUENTIALID()</c>) e
/// nessun <c>ValueGenerator</c> registrato: l'unica sorgente e' <c>IIdGenerator</c>.
/// <para>
/// Non e' ridondante con la convenzione: EF <b>non</b> applica value generation per convenzione a
/// una proprieta' di chiave composita, quindi un default aggiunto a mano su una singola entita'
/// non verrebbe contraddetto da nulla.
/// </para>
/// </summary>
/// <param name="model">Modello costruito una volta per assembly.</param>
public sealed class R28_KeyValueGenerationTests(ModelFixture model)
{
    [Fact]
    public void Key_properties_of_owned_entities_are_never_generated_by_the_store()
    {
        var violations = new List<string>();

        foreach (var entityType in model.OwnedEntityTypes)
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey is null)
            {
                continue;
            }

            foreach (var property in primaryKey.Properties)
            {
                var label = $"{entityType.ClrType.Name}.{property.Name}";

                if (property.ValueGenerated != Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never)
                {
                    violations.Add($"{label}: ValueGenerated = {property.ValueGenerated}, atteso Never.");
                }

                var defaultValueSql = property.GetDefaultValueSql();
                if (defaultValueSql is not null)
                {
                    violations.Add($"{label}: default SQL '{defaultValueSql}' sulla chiave.");
                }

                if (property.GetValueGeneratorFactory() is not null)
                {
                    violations.Add($"{label}: ValueGenerator registrato sulla chiave.");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Chiavi generate dal database, R28 (ADR-0008):\n  " + string.Join("\n  ", violations)
            + "\nConseguenza: l'Id non e' piu' noto prima dell'INSERT, i builder dei test non possono "
            + "agganciare i figli senza round-trip, e il COMB di ADR-0008 smette di essere l'unica sorgente.");
    }
}
