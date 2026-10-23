using Microsoft.EntityFrameworkCore.Metadata;
using Roamly.Domain.Abstractions;

namespace Roamly.Api.Common.Ownership;

/// <summary>Ordine model-driven di rimozione: ogni figlio owned precede ogni suo padre owned.</summary>
public static class AccountErasurePlan
{
    /// <summary>Restituisce tutti i tipi owned del modello nell'ordine sicuro di cancellazione.</summary>
    public static IReadOnlyList<IEntityType> GetDeletionOrder(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var ownedTypes = model.GetEntityTypes()
            .Where(type => typeof(IOwnedResource).IsAssignableFrom(type.ClrType))
            .OrderBy(type => type.ClrType.FullName, StringComparer.Ordinal)
            .ToArray();
        var dependencies = ownedTypes.ToDictionary(type => type, type => OwnedParents(type, ownedTypes));
        var ordered = TopologicallyOrder(ownedTypes, dependencies);
        ordered.Reverse();
        return ordered;
    }

    private static HashSet<IEntityType> OwnedParents(IEntityType child, IReadOnlyCollection<IEntityType> ownedTypes)
    {
        var ownedSet = ownedTypes.ToHashSet();
        return child.GetForeignKeys()
            .Select(foreignKey => foreignKey.PrincipalEntityType)
            .Where(ownedSet.Contains)
            .ToHashSet();
    }

    private static List<IEntityType> TopologicallyOrder(
        IEntityType[] types,
        Dictionary<IEntityType, HashSet<IEntityType>> dependencies)
    {
        var ordered = new List<IEntityType>(types.Length);
        var placed = new HashSet<IEntityType>();

        while (ordered.Count < types.Length)
        {
            var ready = types
                .Where(type => !placed.Contains(type) && dependencies[type].All(placed.Contains))
                .OrderBy(type => type.ClrType.FullName, StringComparer.Ordinal)
                .ToArray();
            if (ready.Length == 0)
            {
                throw new InvalidOperationException(
                    "Il grafo delle foreign key owned contiene un ciclo; l'account non puo' essere cancellato.");
            }

            foreach (var type in ready)
            {
                ordered.Add(type);
                placed.Add(type);
            }
        }

        return ordered;
    }
}
