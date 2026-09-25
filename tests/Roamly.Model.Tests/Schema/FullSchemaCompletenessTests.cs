using System.Globalization;
using System.Text;
using Roamly.Domain;

namespace Roamly.Model.Tests.Schema;

/// <summary>
/// <b>R32 (ADR-0009)</b>: ogni entita' del dominio e' registrata in <c>FullSchemaDbContext</c>.
/// E' il meta-verificatore che protegge il test 1785: senza, fra sei mesi qualcuno aggiunge
/// un'entita', non la registra, e l'analizzatore resta verde <b>su uno schema incompleto</b>.
/// </summary>
/// <param name="model">Modello costruito una volta per assembly.</param>
public sealed class FullSchemaCompletenessTests(ModelFixture model)
{
    [Fact]
    public void Every_entity_of_the_domain_manifest_is_registered_in_the_full_schema_context()
    {
        var mapped = model.FullSchemaModel.GetEntityTypes()
            .Select(e => e.ClrType)
            .ToHashSet();

        var missing = DomainModelManifest.AllEntityTypes
            .Where(t => !mapped.Contains(t))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0, BuildMissingMessage(missing));
    }

    /// <summary>
    /// La direzione opposta: un'entita' di dominio mappata ma <b>assente dal manifesto</b> renderebbe
    /// R32 cieco proprio su di essa. Identity e' esclusa confrontando sul manifesto e su
    /// <c>IOwnedResource</c>, non su una whitelist scritta a mano.
    /// </summary>
    [Fact]
    public void Every_owned_entity_of_the_model_is_declared_in_the_domain_manifest()
    {
        var declared = DomainModelManifest.AllEntityTypes.ToHashSet();

        var undeclared = model.OwnedEntityTypes
            .Where(e => !declared.Contains(e.ClrType))
            .Select(e => e.ClrType.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            undeclared.Count == 0,
            "Entita' owned presenti nel modello ma assenti da DomainModelManifest: "
            + string.Join(", ", undeclared)
            + ". Conseguenza: R32 non le sorveglia, e il giorno in cui sparissero dal contesto "
            + "nessun test lo direbbe.");
    }

    private static string BuildMissingMessage(List<string> missing)
    {
        if (missing.Count == 0)
        {
            return string.Empty;
        }

        var message = new StringBuilder();
        message.AppendLine(CultureInfo.InvariantCulture, $"Entita' del manifesto non registrate in FullSchemaDbContext: {string.Join(", ", missing)}.");
        message.AppendLine("Conseguenza: il test 1785 resterebbe VERDE su uno schema incompleto, cioe' smetterebbe");
        message.AppendLine("di dimostrare la topologia di CONTEXT.md §5.2 senza che nulla diventi rosso.");
        message.AppendLine("Rimedio: aggiungerle in ModelConfigurationRegistry.ApplyAll con la loro IEntityTypeConfiguration.");
        return message.ToString();
    }
}
