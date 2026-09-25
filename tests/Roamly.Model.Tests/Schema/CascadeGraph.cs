using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Roamly.Model.Tests.Schema;

/// <summary>
/// Un arco del grafo delle azioni referenziali: si percorre <b>dal principal verso il dependent</b>,
/// perche' e' la direzione in cui la cancellazione si propaga.
/// </summary>
/// <param name="ForeignKey">Foreign key che genera l'arco.</param>
internal sealed record CascadeEdge(IForeignKey ForeignKey)
{
    /// <summary>Tabella di partenza (il padre).</summary>
    public IEntityType Principal => ForeignKey.PrincipalEntityType;

    /// <summary>Tabella di arrivo (il figlio).</summary>
    public IEntityType Dependent => ForeignKey.DeclaringEntityType;

    /// <summary>Descrizione leggibile dell'arco, con colonne e azione referenziale.</summary>
    /// <returns>Testo per il messaggio di fallimento.</returns>
    public string Describe()
    {
        var columns = string.Join(", ", ForeignKey.Properties.Select(p => p.Name));
        return $"--[({columns}) {ForeignKey.DeleteBehavior}]--> {ModelFixture.TableName(Dependent)}";
    }
}

/// <summary>Un cammino di azione referenziale fra due tabelle.</summary>
/// <param name="Source">Tabella di partenza.</param>
/// <param name="Target">Tabella di arrivo.</param>
/// <param name="Edges">Sequenza di foreign key attraversate.</param>
internal sealed record CascadePath(string Source, string Target, IReadOnlyList<CascadeEdge> Edges)
{
    /// <summary>Rende il cammino come sequenza esplicita di FK.</summary>
    /// <returns>Testo per il messaggio di fallimento.</returns>
    public string Describe()
        => Source + " " + string.Join(" ", Edges.Select(e => e.Describe()));
}

/// <summary>
/// Riproduzione a livello di modello dell'errore SQL Server <b>1785</b>, che si manifesta solo a
/// <c>CREATE TABLE</c>: per ogni coppia (tabella di partenza, tabella di arrivo) puo' esistere
/// <b>al massimo un</b> cammino di azione referenziale.
/// </summary>
internal static class CascadeGraph
{
    /// <summary>
    /// Una FK genera un cammino solo se la cancellazione si propaga: <c>Cascade</c>, <c>SetNull</c>
    /// e <c>ClientSetNull</c>. <c>NoAction</c>, <c>Restrict</c> e <c>ClientNoAction</c> interrompono
    /// il cammino (CONTEXT.md §5.1, ADR-0009 §5).
    /// </summary>
    /// <param name="foreignKey">Foreign key da classificare.</param>
    /// <returns><see langword="true"/> se la FK e' un cammino.</returns>
    public static bool IsPath(IForeignKey foreignKey)
    {
        ArgumentNullException.ThrowIfNull(foreignKey);
        return foreignKey.DeleteBehavior is DeleteBehavior.Cascade
            or DeleteBehavior.SetNull
            or DeleteBehavior.ClientSetNull;
    }

    /// <summary>Estrae gli archi di cammino dal modello.</summary>
    /// <param name="model">Modello EF da analizzare.</param>
    /// <returns>Gli archi del grafo delle azioni referenziali.</returns>
    public static IReadOnlyList<CascadeEdge> EdgesOf(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return [.. model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Distinct()
            .Where(IsPath)
            .Select(fk => new CascadeEdge(fk))];
    }

    /// <summary>
    /// Enumera <b>tutti</b> i cammini semplici del grafo. La visita non rivisita una tabella gia'
    /// presente nel cammino corrente: e' cio' che garantisce la terminazione anche se il grafo
    /// contenesse cicli.
    /// </summary>
    /// <param name="edges">Archi del grafo.</param>
    /// <returns>Tutti i cammini, di qualunque lunghezza.</returns>
    public static IReadOnlyList<CascadePath> EnumerateAllPaths(IReadOnlyList<CascadeEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);

        var outgoing = edges
            .GroupBy(e => ModelFixture.TableName(e.Principal), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CascadeEdge>)[.. g], StringComparer.Ordinal);

        var paths = new List<CascadePath>();

        foreach (var source in outgoing.Keys)
        {
            Walk(source, source, [], new HashSet<string>(StringComparer.Ordinal) { source }, outgoing, paths);
        }

        return paths;
    }

    private static void Walk(
        string source,
        string current,
        List<CascadeEdge> travelled,
        HashSet<string> visited,
        Dictionary<string, IReadOnlyList<CascadeEdge>> outgoing,
        List<CascadePath> paths)
    {
        if (!outgoing.TryGetValue(current, out var next))
        {
            return;
        }

        foreach (var edge in next)
        {
            var target = ModelFixture.TableName(edge.Dependent);
            if (!visited.Add(target))
            {
                continue;
            }

            travelled.Add(edge);
            paths.Add(new CascadePath(source, target, [.. travelled]));

            Walk(source, target, travelled, visited, outgoing, paths);

            travelled.RemoveAt(travelled.Count - 1);
            visited.Remove(target);
        }
    }
}
