using Roamly.Domain;

namespace Roamly.Model.Tests.Schema;

/// <summary>
/// Controparte di R32 sul contesto applicativo: contiene le entita' gia' attive, piu' SavedPlace
/// solo per la FK nullable di TripStop. Le altre entita' future restano escluse dalle migration.
/// <para>
/// Esiste perche' l'errore e' a un solo carattere di distanza: <c>ApplyAll</c> al posto di
/// <c>ApplyPhase1</c>, oppure un <c>ApplyConfigurationsFromAssembly</c> scritto per abitudine.
/// </para>
/// </summary>
/// <param name="model">Modello costruito una volta per assembly.</param>
public sealed class ApplicationModelScopeTests(ModelFixture model)
{
    [Fact]
    public void The_application_context_maps_every_active_entity()
    {
        var mapped = model.ApplicationModel.GetEntityTypes().Select(e => e.ClrType).ToHashSet();

        var missing = DomainModelManifest.ApplicationEntityTypes
            .Where(t => !mapped.Contains(t))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Entita' applicative assenti da RoamlyDbContext: " + string.Join(", ", missing)
            + ". Conseguenza: le migration non creerebbero la loro tabella e la slice corrispondente "
            + "fallirebbe solo a runtime, sul database.");
    }

    [Fact]
    public void The_application_context_maps_no_other_later_phase_entity()
    {
        var mapped = model.ApplicationModel.GetEntityTypes().Select(e => e.ClrType).ToHashSet();
        var active = DomainModelManifest.ApplicationEntityTypes.ToHashSet();

        var leaked = DomainModelManifest.LaterPhaseEntityTypes
            .Where(type => !active.Contains(type) && mapped.Contains(type))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            leaked.Count == 0,
            "Entita' future non attive entrate in RoamlyDbContext: " + string.Join(", ", leaked)
            + ". Conseguenza: le migration creerebbero tabelle che nessuna slice usa.");
    }
}
