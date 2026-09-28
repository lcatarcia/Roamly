using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Roamly.TestSupport;

/// <summary>
/// Registro dei builder: li scopre per riflessione e li ordina <b>topologicamente sul grafo delle
/// FK derivato da <see cref="IModel"/></b> — lo stesso ordine che <c>AccountErasureJob</c> percorre
/// al contrario (ADR-0004, DATA.md §6). <b>Nessuna lista di entita' scritta a mano, da nessuna parte.</b>
/// </summary>
public static class BuilderRegistry
{
    /// <summary>
    /// Scopre i builder concreti.
    /// <para>
    /// 🔴 L'ancoraggio e' <c>typeof(IEntityBuilder).Assembly</c>, <b>non</b>
    /// <c>AppDomain.CurrentDomain.GetAssemblies()</c>: il CLR carica gli assembly pigramente, quindi
    /// a L0 una scansione dell'AppDomain puo' restituire <b>zero</b> builder e rendere R33 verde su
    /// un registro vuoto. E' la terza variante dello stesso fallimento silenzioso gia' visto in
    /// TESTING.md §8.1 e §8.2. Per la stessa ragione il metodo <b>lancia se non trova nulla</b>:
    /// un registro vuoto e' un guasto, mai un insieme legittimo.
    /// </para>
    /// </summary>
    /// <returns>I builder concreti, in ordine di nome. Per l'ordine di costruzione vedi <see cref="InTopologicalOrder"/>.</returns>
    /// <exception cref="InvalidOperationException">Nessun builder trovato nell'assembly.</exception>
    public static IReadOnlyList<IEntityBuilder> Discover()
    {
        var assembly = typeof(IEntityBuilder).Assembly;

        var builders = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IEntityBuilder).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttribute<OutsideTheRegistryAttribute>() is null)
            .Select(t => (IEntityBuilder)Activator.CreateInstance(t)!)
            .OrderBy(b => b.EntityType.Name, StringComparer.Ordinal)
            .ToList();

        if (builders.Count == 0)
        {
            throw new InvalidOperationException(
                "BuilderRegistry.Discover() non ha trovato alcun builder in "
                + assembly.GetName().Name
                + ". Conseguenza: R33 (ADR-0009) confronterebbe le entita' owned con un insieme vuoto "
                + "e sarebbe verde su un registro inesistente, esattamente il fallimento silenzioso "
                + "che il verificatore esiste per impedire.");
        }

        return builders;
    }

    /// <summary>
    /// Ordina i builder in modo che ogni padre preceda i suoi figli, secondo le FK del modello.
    /// </summary>
    /// <param name="model">Modello EF da cui si legge il grafo.</param>
    /// <returns>I builder in ordine di costruzione.</returns>
    /// <exception cref="InvalidOperationException">Il grafo delle FK contiene un ciclo fra tipi con builder.</exception>
    public static IReadOnlyList<IEntityBuilder> InTopologicalOrder(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var builders = Discover();
        var byType = builders.ToDictionary(b => b.EntityType);

        // Dipendenze: i principal delle FK dichiarate dal tipo, limitati ai tipi che hanno un
        // builder. Le FK verso AspNetUsers cadono da sole, perche' l'utente non e' un'entita' owned.
        var dependencies = builders.ToDictionary(
            b => b.EntityType,
            b => ParentsOf(model, b.EntityType, byType.Keys));

        var ordered = new List<IEntityBuilder>(builders.Count);
        var placed = new HashSet<Type>();

        while (ordered.Count < builders.Count)
        {
            var ready = builders
                .Where(b => !placed.Contains(b.EntityType))
                .Where(b => dependencies[b.EntityType].All(placed.Contains))
                .ToList();

            if (ready.Count == 0)
            {
                var stuck = builders
                    .Where(b => !placed.Contains(b.EntityType))
                    .Select(b => b.EntityType.Name)
                    .OrderBy(n => n, StringComparer.Ordinal);

                throw new InvalidOperationException(
                    "Ciclo nel grafo delle FK fra i tipi con builder: " + string.Join(", ", stuck)
                    + ". Conseguenza: non esiste un ordine di inserimento valido, quindi nemmeno "
                    + "un ordine di cancellazione (DATA.md §6). Va indagata la topologia, non il registro.");
            }

            foreach (var builder in ready)
            {
                ordered.Add(builder);
                placed.Add(builder.EntityType);
            }
        }

        return ordered;
    }

    /// <summary>
    /// Costruisce un'istanza per ogni builder, in ordine topologico, registrando ciascuna nel
    /// contesto perche' i figli la trovino con <see cref="BuilderContext.Parent{T}"/>.
    /// <b>Non persiste nulla</b>: <c>AddRange</c> e <c>SaveChangesAsync</c> spettano al chiamante L1.
    /// </summary>
    /// <param name="context">Contesto di costruzione.</param>
    /// <returns>Le istanze, nello stesso ordine in cui vanno inserite.</returns>
    public static IReadOnlyList<object> BuildAll(BuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var built = new List<object>();

        foreach (var builder in InTopologicalOrder(context.Model))
        {
            var entity = builder.Build(context);

            if (entity is null || !builder.EntityType.IsInstanceOfType(entity))
            {
                throw new InvalidOperationException(
                    "Il builder di " + builder.EntityType.Name + " ha prodotto "
                    + (entity?.GetType().Name ?? "null")
                    + " invece di un'istanza del tipo dichiarato in EntityType. Conseguenza: il "
                    + "popolamento inserirebbe una tabella diversa da quella che il test crede di coprire.");
            }

            context.Register(entity);
            built.Add(entity);
        }

        return built;
    }

    /// <summary>Descrizione diagnostica dell'ordine, utile nei messaggi di fallimento.</summary>
    /// <param name="model">Modello EF.</param>
    /// <returns>L'ordine di costruzione in forma leggibile.</returns>
    public static string DescribeOrder(IModel model)
    {
        var names = InTopologicalOrder(model).Select((b, i) =>
            (i + 1).ToString(CultureInfo.InvariantCulture) + ". " + b.EntityType.Name);

        return string.Join(Environment.NewLine, names);
    }

    private static IReadOnlyList<Type> ParentsOf(IModel model, Type entityType, IEnumerable<Type> known)
    {
        var knownTypes = known.ToHashSet();
        var mapped = model.FindEntityType(entityType);

        if (mapped is null)
        {
            throw new InvalidOperationException(
                "Il builder dichiara " + entityType.Name + ", che non e' un entity type del modello. "
                + "Conseguenza: il registro coprirebbe una tabella inesistente, e R33 confronterebbe "
                + "due insiemi disallineati.");
        }

        return [.. mapped.GetForeignKeys()
            .Select(fk => fk.PrincipalEntityType.ClrType)
            .Where(t => t != entityType && knownTypes.Contains(t))
            .Distinct()];
    }
}
