using System.Globalization;
using System.Text;

namespace Roamly.Model.Tests.Schema;

/// <summary>
/// <b>Test A</b> (ADR-0009 §5): riproduzione a livello di modello dell'errore SQL Server <b>1785</b>.
/// Costa ~10 ms, non richiede alcun database, e gira a ogni push.
/// <para>
/// Il suo valore proprio rispetto all'errore reale e' il <b>messaggio</b>: SQL Server dice soltanto
/// <i>"may cause cycles or multiple cascade paths"</i> e nomina la FK rifiutata, non l'altro percorso.
/// Qui vengono nominati la coppia di tabelle <b>e entrambi i cammini in conflitto</b>, FK per FK.
/// </para>
/// </summary>
/// <param name="model">Modello costruito una volta per assembly.</param>
public sealed class CascadePathAnalyzerTests(ModelFixture model)
{
    [Fact]
    public void At_most_one_referential_action_path_exists_between_any_two_tables()
    {
        var edges = CascadeGraph.EdgesOf(model.FullSchemaModel);

        var offenders = CascadeGraph.EnumerateAllPaths(edges)
            .GroupBy(p => (p.Source, p.Target))
            .Where(g => g.Count() > 1)
            .OrderBy(g => g.Key.Source, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Target, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0, BuildFailureMessage(offenders));
    }

    /// <summary>
    /// Verifica che l'analizzatore stia guardando qualcosa: un grafo vuoto renderebbe il test
    /// sopra verde per costruzione, che e' il fallimento silenzioso di TESTING.md §8.1.
    /// </summary>
    [Fact]
    public void The_analyzer_actually_sees_referential_action_edges()
    {
        var edges = CascadeGraph.EdgesOf(model.FullSchemaModel);

        Assert.True(
            edges.Count > 0,
            "Il grafo delle azioni referenziali e' vuoto: l'analizzatore 1785 sarebbe verde per "
            + "costruzione e non potrebbe mai diventare rosso.");
    }

    private static string BuildFailureMessage(List<IGrouping<(string Source, string Target), CascadePath>> offenders)
    {
        if (offenders.Count == 0)
        {
            return string.Empty;
        }

        var message = new StringBuilder();
        message.AppendLine(CultureInfo.InvariantCulture, $"Errore 1785 in arrivo: {offenders.Count} coppia/e di tabelle hanno piu' di un cammino di azione referenziale.");
        message.AppendLine("SQL Server rifiuterebbe la CREATE TABLE senza dire qual e' l'altro percorso. Eccoli entrambi:");

        foreach (var offender in offenders)
        {
            message.AppendLine();
            message.AppendLine(CultureInfo.InvariantCulture, $"  {offender.Key.Source} -> {offender.Key.Target}: {offender.Count()} cammini");

            var index = 0;
            foreach (var path in offender.OrderBy(p => p.Edges.Count))
            {
                index++;
                message.AppendLine(CultureInfo.InvariantCulture, $"    [{index}] {path.Describe()}");
            }
        }

        message.AppendLine();
        message.AppendLine("Rimedio: portare a DeleteBehavior.NoAction una delle FK elencate, mai a SetNull/SetDefault");
        message.AppendLine("(contano come cammino: CONTEXT.md §5.2, R40 di ADR-0008). Poi aggiornare CONTEXT.md §5.1.");
        return message.ToString();
    }
}
