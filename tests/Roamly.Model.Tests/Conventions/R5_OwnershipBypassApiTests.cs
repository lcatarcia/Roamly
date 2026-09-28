using System.Text.RegularExpressions;

namespace Roamly.Model.Tests.Conventions;

/// <summary>
/// <b>R5 (ADR-0003)</b>: le sette famiglie di API EF che <b>non passano dal query filter di ownership</b> sono
/// vietate in tutto <c>src/</c>, tranne in <c>src/Roamly.Api/Common/Ownership/</c>, l'unico luogo autorizzato
/// ad aggirare il filtro.
/// <para>
/// <c>ARCHITECTURE.md</c> §4 dichiarava il divieto applicato da <c>BannedApiAnalyzers</c>: era falso
/// (<c>TESTING.md</c> §8.8). L'analyzer non conosce le eccezioni per percorso, quindi il meccanismo reale e'
/// questo lint testuale L0, istanza di <see cref="PathScopedBan"/>.
/// </para>
/// <para>
/// ⚠️ <b><c>Entry()</c> non e' bandito.</b> Vietata e' solo l'assegnazione <c>Entry(...).State = ...</c>.
/// <c>Entry(e).Property(x).OriginalValue</c> e' il pattern che <b>ADR-0005 R49</b> prescrive per la concorrenza
/// ottimistica: un lint piu' largo renderebbe impossibile la regola di un altro ADR.
/// </para>
/// </summary>
public sealed class R5_OwnershipBypassApiTests
{
    /// <summary>
    /// Riconosce il destinatario di una chiamata EF: un identificatore che contiene <c>db</c>/<c>context</c>,
    /// oppure un <c>Set&lt;T&gt;()</c>. Serve a distinguere <c>db.Campers.Update(x)</c> da <c>UpdateCamper(x)</c>,
    /// <c>UpdatedAtUtc</c> e <c>LastUpdated</c>, che il solo nome del metodo confonderebbe.
    /// </summary>
    private const string EfReceiver =
        @"(?:\w*(?:[Dd]b|[Cc]ontext)\w*|Set\s*<[^<>]{1,120}>\s*\(\s*\))\s*\.(?:\w+\s*\.)?";

    internal static PathScopedBan Ban { get; } = new()
    {
        RuleId = "R5",
        Source = "ADR-0003",
        PerimeterDescription = "src/ (tutto il codice di produzione)",
        Perimeter = path => path.StartsWith("src/", StringComparison.OrdinalIgnoreCase),
        Exemptions =
        [
            new PathExemption(
                "src/Roamly.Api/Common/Ownership",
                MustExistToday: false,
                "ADR-0003: unico luogo autorizzato ad aggirare il query filter. Oggi la cartella NON esiste "
                + "(non c'e' ancora composition root), quindi l'esenzione e' dichiarata ma non esercitata: "
                + "nascera' con la prima slice e il lint la rispettera' senza modifiche."),
        ],
        Symbols =
        [
            new BannedSymbol(
                "FindAsync",
                @"\.FindAsync\s*\(",
                "Legge per chiave primaria saltando i query filter: restituisce l'entita' di un altro owner."),
            new BannedSymbol(
                "Find",
                @"\b" + EfReceiver + @"Find\s*\(",
                "Come FindAsync. Riconosciuto solo sulla chiamata a un DbSet/DbContext, per non colpire "
                + "List<T>.Find e omonimi di dominio."),
            new BannedSymbol(
                "Attach/AttachRange",
                @"\.Attach(?:Range)?\s*\(",
                "Aggancia al change tracker un'istanza mai letta dal database: l'ownership non e' mai verificata."),
            new BannedSymbol(
                "UpdateRange",
                @"\.UpdateRange\s*\(",
                "Come Attach, su un insieme."),
            new BannedSymbol(
                "Update",
                @"\b" + EfReceiver + @"Update\s*\(",
                "Marca Modified un grafo non tracciato. Riconosciuto solo sulla chiamata a un DbSet/DbContext, "
                + "per non colpire UpdateCamper/UpdatedAtUtc/LastUpdated."),
            new BannedSymbol(
                "Entry().State = ...",
                @"\.Entry\s*\([^()]*\)\s*\.\s*State\s*=|\.State\s*=\s*EntityState\.",
                "Forza lo stato del change tracker senza passare da una query filtrata. "
                + "NON e' vietato Entry(e).Property(...).OriginalValue, che ADR-0005 R49 prescrive."),
            new BannedSymbol(
                "ExecuteUpdate/ExecuteDelete",
                @"\.Execute(?:Update|Delete)(?:Async)?\s*\(",
                "Scrittura set-based emessa in SQL: non attraversa il change tracker ne' il query filter."),
            new BannedSymbol(
                "FromSqlRaw/FromSqlInterpolated",
                @"\.FromSql(?:Raw|Interpolated)\s*\(",
                "SQL scritto a mano: il filtro di ownership c'e' solo se lo si riscrive, e prima o poi non lo si fa."),
            new BannedSymbol(
                "IgnoreQueryFilters",
                @"\.IgnoreQueryFilters\s*\(",
                "Disattiva esplicitamente la garanzia di sicurezza centrale del progetto."),
        ],
    };

    /// <summary>Nessuna delle sette famiglie compare in <c>src/</c> fuori dal percorso di ownership.</summary>
    [Fact]
    public void Ownership_bypassing_ef_apis_are_absent_from_production_code()
        => Ban.AssertNoViolations();

    /// <summary>
    /// Il pattern di <c>Entry().State</c> distingue l'assegnazione vietata dall'uso prescritto da ADR-0005 R49.
    /// Un lint che non distingue e' peggio di nessun lint, perche' verra' disattivato.
    /// </summary>
    [Fact]
    public void The_concurrency_pattern_prescribed_by_R49_is_not_flagged()
    {
        var entryState = Matcher("Entry().State = ...");

        Assert.False(
            entryState.IsMatch("db.Entry(entity).Property(e => e.RowVersion).OriginalValue = token;"),
            "Il lint di R5 colpisce Entry(...).Property(...).OriginalValue, che ADR-0005 R49 PRESCRIVE per la "
            + "concorrenza ottimistica. Un divieto su Entry() renderebbe impossibile la regola di un altro ADR.");

        Assert.False(
            entryState.IsMatch("var original = db.Entry(entity).Property(e => e.Name).OriginalValue;"),
            "Lettura di OriginalValue erroneamente segnalata.");

        Assert.True(
            entryState.IsMatch("db.Entry(entity).State = EntityState.Modified;"),
            "Il lint di R5 NON riconosce Entry(...).State = ..., che e' la forma vietata.");

        Assert.True(
            entryState.IsMatch("entry.State = EntityState.Deleted;"),
            "Il lint di R5 non riconosce l'assegnazione di stato passata per variabile intermedia.");
    }

    /// <summary>
    /// I nomi comuni non fanno scattare il lint: il riconoscimento e' sulla <b>chiamata a un DbSet/DbContext</b>,
    /// non sulla sottostringa.
    /// </summary>
    [Fact]
    public void Common_identifiers_named_update_or_find_are_not_flagged()
    {
        var update = Matcher("Update");
        var find = Matcher("Find");

        Assert.False(update.IsMatch("public async Task<Response> UpdateCamper(Command cmd)"), "UpdateCamper segnalato.");
        Assert.False(update.IsMatch("camper.UpdatedAtUtc = clock.GetUtcNow();"), "UpdatedAtUtc segnalato.");
        Assert.False(update.IsMatch("var lastUpdated = item.LastUpdated;"), "LastUpdated segnalato.");
        Assert.False(update.IsMatch("await mediator.Update(request);"), "Chiamata Update su un non-DbContext segnalata.");
        Assert.False(find.IsMatch("var stop = stops.Find(s => s.Order == 1);"), "List<T>.Find segnalato.");

        Assert.True(update.IsMatch("db.Campers.Update(camper);"), "db.Campers.Update non riconosciuto.");
        Assert.True(update.IsMatch("context.Set<Camper>().Update(camper);"), "Set<T>().Update non riconosciuto.");
        Assert.True(find.IsMatch("await _dbContext.Campers.Find(id);"), "_dbContext.Campers.Find non riconosciuto.");
    }

    /// <summary>Le esenzioni dichiarate obbligatorie esistono. Per R5 non ve ne sono: vedi la nota nell'esenzione.</summary>
    [Fact]
    public void Declared_exemptions_exist()
        => Ban.AssertDeclaredExemptionsExist();

    private static Regex Matcher(string symbol)
        => Ban.Symbols.First(s => s.Symbol == symbol).Matcher;
}
