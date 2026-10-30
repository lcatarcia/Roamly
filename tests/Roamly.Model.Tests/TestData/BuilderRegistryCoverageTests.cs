using System.Globalization;
using System.Text;
using Roamly.Domain.Abstractions;
using Roamly.TestSupport;

namespace Roamly.Model.Tests.TestData;

/// <summary>
/// <b>R33 (ADR-0009)</b>: ogni entita' owned ha un builder registrato, e il registro si deriva dal
/// modello — <b>nessuna lista di entita' scritta a mano, da nessuna parte</b>.
/// <para>
/// Il verificatore vive qui, a L0, e i builder vivono in <c>Roamly.TestSupport</c>: e' la
/// risoluzione della contraddizione annotata in TESTING.md §3, dove il registro stava in
/// <c>Roamly.IntegrationTests</c> e il suo verificatore in <c>Roamly.Model.Tests</c> — cioe' un
/// progetto di test che ne referenzia un altro.
/// </para>
/// <para>
/// ⚠️ Debolezza dichiarata (ADR-0009): il verificatore garantisce che il builder <b>esista</b>, non
/// che sia <b>significativo</b>. La convenzione "popola tutti i campi non-nullable e almeno un
/// campo nullable per complex type" resta verificabile solo in review.
/// </para>
/// </summary>
/// <param name="model">Modello costruito una volta per assembly.</param>
public sealed class BuilderRegistryCoverageTests(ModelFixture model)
{
    [Fact]
    public void Every_owned_entity_of_the_model_has_a_registered_builder()
    {
        var covered = BuilderRegistry.Discover().Select(b => b.EntityType).ToHashSet();

        var uncovered = model.OwnedEntityTypes
            .Where(e => !covered.Contains(e.ClrType))
            .Select(e => e.ClrType.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(uncovered.Count == 0, BuildUncoveredMessage(uncovered));
    }

    /// <summary>
    /// La direzione opposta, con un messaggio che distingue i due casi. Un builder per un'entita'
    /// <b>non owned</b> — per esempio <c>ErasureReceipt</c> o il contatore infrastrutturale del
    /// rate limit login — non e' "un builder di troppo": e' un'entita' che per costruzione non ha
    /// <c>OwnerId</c> ne' FK verso <c>AspNetUsers</c>, quindi sta fuori dal registro. Senza questa distinzione
    /// R33 fallirebbe <b>per il motivo sbagliato</b>, e si rimedierebbe nel modo sbagliato.
    /// </summary>
    [Fact]
    public void No_builder_is_registered_for_a_type_that_is_not_an_owned_entity()
    {
        var owned = model.OwnedEntityTypes.Select(e => e.ClrType).ToHashSet();

        var strangers = BuilderRegistry.Discover()
            .Select(b => b.EntityType)
            .Where(t => !owned.Contains(t))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            strangers.Count == 0,
            "Builder registrati per tipi che non sono entita' owned: " + string.Join(", ", strangers)
            + ". Un tipo senza " + nameof(IOwnedResource) + " non ha OwnerId e non e' toccato da R9: "
            + "il suo builder va marcato con [OutsideTheRegistry], non aggiunto al registro. "
            + "Casi previsti: ErasureReceipt (R10, ADR-0004) e LoginAttemptCounter (SECURITY.md §1).");
    }

    /// <summary>
    /// 🔴 Il registro non puo' essere vuoto. <c>Discover()</c> si ancora a
    /// <c>typeof(IEntityBuilder).Assembly</c> proprio perche' una scansione di
    /// <c>AppDomain.GetAssemblies()</c>, a L0, puo' restituire <b>zero</b> builder per caricamento
    /// pigro — e R33 sarebbe verde su un insieme vuoto. Questo test rende quel guasto osservabile.
    /// </summary>
    [Fact]
    public void The_registry_is_not_empty()
    {
        var builders = BuilderRegistry.Discover();

        Assert.True(
            builders.Count > 0,
            "BuilderRegistry.Discover() ha restituito zero builder. Conseguenza: R33 confronterebbe "
            + "le entita' owned con un insieme vuoto e resterebbe verde su un registro inesistente.");
    }

    /// <summary>
    /// L'ordine di costruzione si deriva dalle FK del modello: ogni padre precede i suoi figli.
    /// E' lo stesso ordine che la cancellazione di un account percorre al contrario (DATA.md §6).
    /// </summary>
    [Fact]
    public void Every_parent_is_built_before_its_children()
    {
        var ordered = BuilderRegistry.InTopologicalOrder(model.FullSchemaModel);
        var position = ordered
            .Select((b, index) => (b.EntityType, index))
            .ToDictionary(x => x.EntityType, x => x.index);

        var violations = new List<string>();

        foreach (var builder in ordered)
        {
            var entityType = model.FullSchemaModel.FindEntityType(builder.EntityType)!;

            var late = entityType.GetForeignKeys()
                .Select(fk => fk.PrincipalEntityType.ClrType)
                .Where(parent => parent != builder.EntityType && position.ContainsKey(parent))
                .Where(parent => position[parent] > position[builder.EntityType])
                .Select(parent => parent.Name + " dopo " + builder.EntityType.Name);

            violations.AddRange(late);
        }

        Assert.True(
            violations.Count == 0,
            "Ordine topologico violato: " + string.Join(", ", violations)
            + ". Conseguenza: il popolamento L1 inserirebbe un figlio prima del padre e la FK "
            + "composita lo rifiuterebbe. Ordine calcolato:\n"
            + BuilderRegistry.DescribeOrder(model.FullSchemaModel));
    }

    [Fact]
    public void Runtime_builder_plan_contains_only_owned_types_present_in_that_model()
    {
        var expected = model.ApplicationRuntimeModel.GetEntityTypes()
            .Where(entityType => typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType)
            .ToHashSet();

        var actual = BuilderRegistry.InTopologicalOrder(model.ApplicationRuntimeModel)
            .Select(builder => builder.EntityType)
            .ToArray();

        Assert.Equal(expected, actual.ToHashSet());
        Assert.Equal(expected.Count, actual.Length);
    }

    private static string BuildUncoveredMessage(List<string> uncovered)
    {
        if (uncovered.Count == 0)
        {
            return string.Empty;
        }

        var message = new StringBuilder();
        message.AppendLine(CultureInfo.InvariantCulture, $"Entita' owned senza builder: {string.Join(", ", uncovered)}.");
        message.AppendLine("Il test R9 'erase and sweep' NON le coprirebbe: sarebbe verde su una cancellazione parziale,");
        message.AppendLine("cioe' dichiarerebbe cancellato un account che ha lasciato righe personali nel database.");
        message.AppendLine("Rimedio: un IEntityBuilder in Roamly.TestSupport/Builders, non una riga di setup in un test.");
        return message.ToString();
    }
}
