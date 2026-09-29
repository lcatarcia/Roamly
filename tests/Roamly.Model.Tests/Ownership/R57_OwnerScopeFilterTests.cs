using Roamly.Infrastructure.Persistence;

namespace Roamly.Model.Tests.Ownership;

/// <summary>
/// <b>R57</b> (verificatore mancante di R2, <c>SECURITY.md</c> §2.2): ogni entita' owned porta
/// **esattamente un** query filter, nominato <see cref="OwnedResourceConvention.OwnerScopeFilter"/>;
/// nessuna entita' non-owned ne ha uno.
/// <para>
/// Il valore della costante e' asserito **letteralmente uguale** a <c>"OwnerScope"</c>: il nome non
/// e' un dettaglio estetico, e' la stringa che <c>IgnoreQueryFilters(["OwnerScope"])</c> usa per
/// riaprire il filtro dentro <c>Common/Ownership/</c> (R5). Se la costante cambiasse valore senza
/// che questo test lo sappia, R5 continuerebbe a citare un nome che non esiste piu' nel modello.
/// </para>
/// </summary>
/// <param name="model">Modello costruito una volta per assembly.</param>
public sealed class R57_OwnerScopeFilterTests(ModelFixture model)
{
    [Fact]
    public void The_filter_constant_is_literally_owner_scope()
    {
        Assert.Equal("OwnerScope", OwnedResourceConvention.OwnerScopeFilter);
    }

    [Fact]
    public void Every_owned_entity_has_exactly_one_query_filter_named_owner_scope()
    {
        var violations = new List<string>();

        foreach (var entityType in model.OwnedEntityTypes)
        {
            var filterNames = entityType.GetDeclaredQueryFilters().Select(f => f.Key).ToArray();

            if (filterNames.Length != 1 || filterNames[0] != OwnedResourceConvention.OwnerScopeFilter)
            {
                violations.Add(
                    $"{entityType.ClrType.Name}: filtri = [{string.Join(", ", filterNames)}], " +
                    $"atteso esattamente [{OwnedResourceConvention.OwnerScopeFilter}]. " +
                    "Conseguenza: isolamento in lettura perso o silenziosamente duplicato.");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Violazioni di R57 (nome del query filter):\n" + string.Join('\n', violations));
    }

    [Fact]
    public void No_non_owned_entity_carries_the_owner_scope_filter()
    {
        var violations = model.FullSchemaModel.GetEntityTypes()
            .Where(e => !ModelFixture.IsOwned(e))
            .Where(e => e.GetDeclaredQueryFilters().Any(f => f.Key == OwnedResourceConvention.OwnerScopeFilter))
            .Select(e => e.ClrType.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Entita' non owned con il filtro OwnerScope: " + string.Join(", ", violations) +
            ". Conseguenza: un filtro su un'entita' senza OwnerId leggerebbe una proprieta' " +
            "inesistente o, peggio, ne introdurrebbe una solo per il filtro.");
    }
}
