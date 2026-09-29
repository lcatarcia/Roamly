using Microsoft.AspNetCore.Identity;
using Roamly.Domain.Abstractions;
using Roamly.Domain.Entities;

namespace Roamly.Model.Tests.Ownership;

/// <summary>
/// <b>R56</b> (verificatore mancante di R1, <c>SECURITY.md</c> §2.2): ogni entity type dello
/// schema completo e' <c>IOwnedResource</c>, oppure un tipo di ASP.NET Core Identity, oppure e'
/// dichiarato qui con una motivazione.
/// <para>
/// 🔴 La popolazione di questo verificatore parte da <c>GetEntityTypes()</c> **senza** alcun filtro.
/// Se filtrasse con lo stesso predicato <c>IsOwned</c> che seleziona <c>OwnedEntityTypes</c>, un'entita'
/// che dimentica <see cref="IOwnedResource"/> si auto-escluderebbe dalla popolazione invece di violare
/// la regola: e' esattamente il modo in cui R1 e' rimasta invisibile finora (TESTING.md §8, reperto S4).
/// </para>
/// <para>
/// La whitelist e' **bidirezionale**, sul modello di <c>FullSchemaCompletenessTests</c>: non basta che
/// ogni voce non-owned sia in whitelist, serve anche che ogni voce della whitelist corrisponda a
/// un'entita' realmente presente. Una whitelist con voci morte accumula eccezioni che nessuno rivede.
/// </para>
/// </summary>
/// <param name="model">Modello costruito una volta per assembly.</param>
public sealed class R56_OwnedResourceCoverageTests(ModelFixture model)
{
    /// <summary>
    /// Eccezioni consapevoli a R1. Aggiungere una voce e' una decisione di sicurezza, non una
    /// riparazione di test: ogni voce dichiara perche' quell'entita' non porta <c>OwnerId</c>.
    /// </summary>
    private static readonly IReadOnlyDictionary<Type, string> NotOwnedByDesign = new Dictionary<Type, string>
    {
        [typeof(ErasureReceipt)] =
            "Prova della cancellazione: sopravvive per definizione oltre la cancellazione dell'account " +
            "che l'ha generata (ADR-0004). Una FK verso OwnerId non avrebbe senso su un record che esiste " +
            "proprio perche' quell'utente non esiste piu'.",
    };

    [Fact]
    public void Every_entity_type_is_owned_identity_or_a_declared_exception()
    {
        var violations = new List<string>();

        foreach (var entityType in model.FullSchemaModel.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (typeof(IOwnedResource).IsAssignableFrom(clrType))
            {
                continue;
            }

            if (IsIdentityType(clrType))
            {
                continue;
            }

            if (NotOwnedByDesign.ContainsKey(clrType))
            {
                continue;
            }

            violations.Add(
                $"{clrType.Name}: non implementa IOwnedResource, non e' un tipo di Identity e non e' " +
                "nella whitelist di R56. Conseguenza: nessun OwnerId, nessun query filter, nessuna FK " +
                "verso AspNetUsers, nessuna cancellazione a cascata (R9) — sei presidi persi in silenzio.");
        }

        Assert.True(
            violations.Count == 0,
            "Violazioni di R56 (copertura IOwnedResource):\n" + string.Join('\n', violations));
    }

    [Fact]
    public void Every_whitelist_entry_matches_an_entity_actually_present_in_the_model()
    {
        var mapped = model.FullSchemaModel.GetEntityTypes().Select(e => e.ClrType).ToHashSet();

        var deadEntries = NotOwnedByDesign.Keys
            .Where(t => !mapped.Contains(t))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            deadEntries.Count == 0,
            "Voci morte nella whitelist di R56: " + string.Join(", ", deadEntries) +
            ". Conseguenza: se un tipo con quel nome tornasse a esistere, sarebbe gia' esentato in " +
            "silenzio da chi ha scritto la whitelist mesi prima, senza che nessuno lo decida di nuovo.");
    }

    /// <summary>
    /// Riconosce i tipi di Identity per **proprieta'**, non per nome: qualunque tipo che derivi da
    /// <see cref="IdentityUser{TKey}"/>, <see cref="IdentityRole{TKey}"/> o viva nel namespace
    /// <c>Microsoft.AspNetCore.Identity</c> (le cinque entita' di supporto: claim, login, token,
    /// user-role, role-claim). E' piu' robusto di una lista di nomi: un tipo di Identity aggiunto
    /// domani da un aggiornamento del framework non farebbe lamentare il verificatore di un tipo
    /// che non e' nostro.
    /// </summary>
    private static bool IsIdentityType(Type clrType)
    {
        if (clrType.Namespace is { } ns && ns.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal))
        {
            return true;
        }

        var current = clrType.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType)
            {
                var definition = current.GetGenericTypeDefinition();
                if (definition == typeof(IdentityUser<>) || definition == typeof(IdentityRole<>))
                {
                    return true;
                }
            }

            current = current.BaseType;
        }

        return false;
    }
}
