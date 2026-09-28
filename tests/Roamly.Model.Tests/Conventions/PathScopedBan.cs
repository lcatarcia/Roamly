using System.Globalization;
using System.Text.RegularExpressions;

namespace Roamly.Model.Tests.Conventions;

/// <summary>
/// Un simbolo vietato, con la forma testuale che lo riconosce e la ragione del divieto.
/// </summary>
/// <param name="Symbol">Nome leggibile del simbolo, come compare nei documenti normativi.</param>
/// <param name="Pattern">Espressione regolare che riconosce l'uso del simbolo in una riga di codice.</param>
/// <param name="Rationale">Perche' e' vietato: finisce nel messaggio di errore.</param>
internal sealed record BannedSymbol(string Symbol, string Pattern, string Rationale)
{
    /// <summary>Matcher compilato una volta, con timeout esplicito come in <see cref="RepositoryRoot"/>.</summary>
    public Regex Matcher { get; } = new(
        Pattern,
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));
}

/// <summary>
/// Un percorso esentato dal divieto: il luogo unico in cui il simbolo e' lecito.
/// </summary>
/// <param name="Path">Percorso relativo alla radice del repository, separatori <c>/</c>.</param>
/// <param name="MustExistToday">
/// <see langword="true"/> se il percorso deve gia' esistere: un'esenzione verso un percorso
/// inesistente e' un'esenzione che nessuno sta esercitando, e va saputo.
/// </param>
/// <param name="Rationale">Perche' quel percorso e' autorizzato.</param>
internal sealed record PathExemption(string Path, bool MustExistToday, string Rationale);

/// <summary>Una violazione trovata: file, riga, simbolo, testo.</summary>
/// <param name="RelativePath">Percorso del file relativo alla radice.</param>
/// <param name="LineNumber">Numero di riga, base 1.</param>
/// <param name="Symbol">Simbolo vietato che ha prodotto il match.</param>
/// <param name="Line">Testo della riga, ripulito dagli spazi laterali.</param>
internal sealed record BanViolation(string RelativePath, int LineNumber, string Symbol, string Line);

/// <summary>Esito di una scansione: quanti file sono stati davvero ispezionati e cosa e' stato trovato.</summary>
/// <param name="InspectedFiles">Percorsi relativi dei file dentro il perimetro, esenzioni escluse.</param>
/// <param name="ExemptedFiles">Percorsi relativi dei file dentro il perimetro ma coperti da un'esenzione.</param>
/// <param name="Violations">Violazioni trovate.</param>
internal sealed record LintOutcome(
    IReadOnlyList<string> InspectedFiles,
    IReadOnlyList<string> ExemptedFiles,
    IReadOnlyList<BanViolation> Violations);

/// <summary>
/// <b>Lint testuale parametrico: «questo simbolo e' vietato in questo perimetro, tranne in questi percorsi».</b>
/// <para>
/// Esiste perche' <c>BannedApiAnalyzers</c> <b>non sa esprimere eccezioni per percorso</b>: bandisce un simbolo
/// nell'intera compilazione. Due regole di Roamly hanno esattamente quella forma — <b>R5</b> (ADR-0003: le API che
/// aggirano il query filter di ownership, lecite solo in <c>Common/Ownership/</c>) e <b>R41</b> (ADR-0008: la
/// generazione di <c>Guid</c>, lecita solo nel generatore COMB) — ed entrambe erano dichiarate applicate da
/// BannedSymbols senza esserlo (<c>TESTING.md</c> §8.7 e §8.8). Un solo verificatore parametrico, istanziato due
/// volte, invece di due verificatori gemelli.
/// </para>
/// <para>
/// <b>Auto-riferimento.</b> Un lint testuale contiene per forza le stringhe che vieta. <c>R36</c> lo risolve
/// spezzando il letterale in due; qui il meccanismo e' <b>piu' forte e dichiarato</b>: il perimetro di entrambe le
/// istanze e' <c>src/</c>, mentre il lint e le sue istanze vivono sotto <c>tests/</c>. Nessun file di questo
/// verificatore puo' finire nell'insieme ispezionato, quindi non esiste auto-accusa da neutralizzare — e non serve
/// nessuna esclusione per percorso che, se sbagliata, spegnerebbe il lint in silenzio.
/// </para>
/// <para>
/// <b>Il lint non deve poter essere verde a vuoto.</b> <see cref="AssertNoViolations"/> asserisce che l'insieme
/// ispezionato sia non vuoto: zero file significa lint inerte, non codice pulito (<c>TESTING.md</c> §8).
/// </para>
/// </summary>
internal sealed class PathScopedBan
{
    /// <summary>Codice della regola, es. <c>R5</c>.</summary>
    public required string RuleId { get; init; }

    /// <summary>Fonte normativa, es. <c>ADR-0003</c>.</summary>
    public required string Source { get; init; }

    /// <summary>Descrizione leggibile del perimetro, per i messaggi d'errore.</summary>
    public required string PerimeterDescription { get; init; }

    /// <summary>Predicato sul percorso relativo normalizzato (separatori <c>/</c>) che definisce il perimetro.</summary>
    public required Func<string, bool> Perimeter { get; init; }

    /// <summary>I percorsi in cui il simbolo resta lecito.</summary>
    public required IReadOnlyList<PathExemption> Exemptions { get; init; }

    /// <summary>I simboli vietati dentro il perimetro.</summary>
    public required IReadOnlyList<BannedSymbol> Symbols { get; init; }

    /// <summary>Esegue la scansione.</summary>
    /// <returns>L'esito, comprensivo dei file ispezionati e di quelli esentati.</returns>
    public LintOutcome Run()
    {
        var inspected = new List<string>();
        var exempted = new List<string>();
        var violations = new List<BanViolation>();

        foreach (var absolute in RepositoryRoot.CodeFiles())
        {
            var relative = Normalize(RepositoryRoot.Relative(absolute));

            if (!Perimeter(relative))
            {
                continue;
            }

            if (Exemptions.Any(exemption => IsUnder(relative, exemption.Path)))
            {
                exempted.Add(relative);
                continue;
            }

            inspected.Add(relative);

            var lines = File.ReadAllLines(absolute);

            for (var index = 0; index < lines.Length; index++)
            {
                foreach (var symbol in Symbols)
                {
                    if (symbol.Matcher.IsMatch(lines[index]))
                    {
                        violations.Add(new BanViolation(
                            relative,
                            index + 1,
                            symbol.Symbol,
                            lines[index].Trim()));
                    }
                }
            }
        }

        return new LintOutcome(inspected, exempted, violations);
    }

    /// <summary>
    /// Asserisce che il perimetro sia non vuoto e che non contenga violazioni. Il messaggio di rosso
    /// <b>nomina file, riga e simbolo</b>: un lint che dice solo "fallito" non e' usabile (R38, ADR-0009).
    /// </summary>
    public void AssertNoViolations()
    {
        var outcome = Run();

        Assert.True(
            outcome.InspectedFiles.Count > 0,
            RuleId + " (" + Source + "): il perimetro '" + PerimeterDescription + "' non contiene alcun file. "
            + "Zero file ispezionati NON significa codice pulito: significa che questo lint e' INERTE — "
            + "passa senza guardare nulla, come i verificatori catalogati in TESTING.md §8. "
            + "Cause possibili: il perimetro e' stato rinominato, la scansione di RepositoryRoot.CodeFiles() "
            + "e' cambiata, oppure un'esenzione e' cosi' larga da coprire l'intero perimetro (file esentati: "
            + outcome.ExemptedFiles.Count + ").");

        if (outcome.Violations.Count == 0)
        {
            return;
        }

        var detail = string.Join(
            Environment.NewLine,
            outcome.Violations.Select(v =>
                "  - " + v.RelativePath + ":" + v.LineNumber.ToString(CultureInfo.InvariantCulture)
                + " usa il simbolo vietato '" + v.Symbol + "' -> " + v.Line
                + " | " + Symbols.First(s => s.Symbol == v.Symbol).Rationale));

        Assert.Fail(
            RuleId + " (" + Source + "): "
            + outcome.Violations.Count.ToString(CultureInfo.InvariantCulture)
            + " violazione/i nel perimetro '" + PerimeterDescription + "' ("
            + outcome.InspectedFiles.Count.ToString(CultureInfo.InvariantCulture) + " file ispezionati, "
            + outcome.ExemptedFiles.Count.ToString(CultureInfo.InvariantCulture) + " esentati):"
            + Environment.NewLine + detail + Environment.NewLine
            + "Percorsi in cui questi simboli sono leciti: "
            + string.Join(", ", Exemptions.Select(e => e.Path)) + ".");
    }

    /// <summary>
    /// Asserisce che le esenzioni dichiarate obbligatorie esistano davvero su disco. Un'esenzione verso un
    /// percorso inesistente non e' un errore in se', ma va distinta da una esercitata.
    /// </summary>
    public void AssertDeclaredExemptionsExist()
    {
        foreach (var exemption in Exemptions.Where(e => e.MustExistToday))
        {
            var absolute = System.IO.Path.Combine(
                RepositoryRoot.Path,
                exemption.Path.Replace('/', System.IO.Path.DirectorySeparatorChar));

            Assert.True(
                File.Exists(absolute) || Directory.Exists(absolute),
                RuleId + " (" + Source + "): il percorso di esenzione '" + exemption.Path
                + "' non esiste. L'esenzione e' morta: o il percorso e' stato rinominato — e allora il simbolo e' "
                + "ora vietato anche nel suo unico luogo lecito — oppure e' il divieto a essere diventato "
                + "incondizionato senza che nessuno lo abbia deciso. " + exemption.Rationale);
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static bool IsUnder(string relativePath, string exemptionPath) =>
        relativePath.Equals(exemptionPath, StringComparison.OrdinalIgnoreCase)
        || relativePath.StartsWith(
            exemptionPath.EndsWith('/') ? exemptionPath : exemptionPath + "/",
            StringComparison.OrdinalIgnoreCase);
}
