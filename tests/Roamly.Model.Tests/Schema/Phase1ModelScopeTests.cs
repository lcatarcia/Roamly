using Roamly.Domain;

namespace Roamly.Model.Tests.Schema;

/// <summary>
/// Controparte di R32 sul contesto applicativo: <c>RoamlyDbContext</c> contiene <b>solo</b> le sei
/// entita' della Phase 1. Le entita' a tabella rimandata non devono entrarci, altrimenti la prima
/// migration creerebbe otto tabelle vuote in produzione (CONTEXT.md §2.6).
/// <para>
/// Esiste perche' l'errore e' a un solo carattere di distanza: <c>ApplyAll</c> al posto di
/// <c>ApplyPhase1</c>, oppure un <c>ApplyConfigurationsFromAssembly</c> scritto per abitudine.
/// </para>
/// </summary>
/// <param name="model">Modello costruito una volta per assembly.</param>
public sealed class Phase1ModelScopeTests(ModelFixture model)
{
    [Fact]
    public void The_application_context_maps_every_phase1_entity()
    {
        var mapped = model.Phase1Model.GetEntityTypes().Select(e => e.ClrType).ToHashSet();

        var missing = DomainModelManifest.Phase1EntityTypes
            .Where(t => !mapped.Contains(t))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Entita' Phase 1 assenti da RoamlyDbContext: " + string.Join(", ", missing)
            + ". Conseguenza: la prima migration non creerebbe la loro tabella e la slice corrispondente "
            + "fallirebbe solo a runtime, sul database.");
    }

    [Fact]
    public void The_application_context_maps_no_deferred_entity()
    {
        var mapped = model.Phase1Model.GetEntityTypes().Select(e => e.ClrType).ToHashSet();

        var leaked = DomainModelManifest.LaterPhaseEntityTypes
            .Where(mapped.Contains)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            leaked.Count == 0,
            "Entita' a tabella rimandata entrate in RoamlyDbContext: " + string.Join(", ", leaked)
            + ". Conseguenza: la prima migration creerebbe tabelle vuote che nessuna slice usa, "
            + "cioe' debito operativo in produzione per mesi (CONTEXT.md §2.6).");
    }
}
