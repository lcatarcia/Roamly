using System.Text.RegularExpressions;

namespace Roamly.Model.Tests.Conventions;

/// <summary>
/// <b>R41 (ADR-0008)</b>: nessun tipo di dominio produce un <c>Id</c> da se'. L'unica sorgente di
/// <c>Guid</c> e' il generatore COMB, perche' la clustering key <c>(OwnerId, Id)</c> degrada in inserimenti
/// casuali se gli id non sono sequenziali nell'ordinamento di SQL Server.
/// <para>
/// ADR-0008 prescriveva un <i>«regex su <c>src/**/Domain/**</c> che vieta quelle chiamate fuori da
/// <c>Roamly.Common.SequentialGuidGenerator</c>»</i>, e il divieto di <c>Guid.CreateVersion7()</c> in tutta la
/// persistenza. Non esisteva in nessuna forma (<c>TESTING.md</c> §8.7): questo e' quel lint, come seconda
/// istanza di <see cref="PathScopedBan"/>.
/// </para>
/// <para>
/// ⚠️ Il tipo dichiarato da ADR-0008 e' <c>Roamly.Common.SequentialGuidGenerator</c> e nel repo esiste con
/// quel nome, in <c>src/Roamly.Common/SequentialGuidGenerator.cs</c>. Il percorso d'esenzione e' <b>quel file</b>,
/// non l'intero progetto <c>Roamly.Common</c>: un'esenzione a livello di progetto autorizzerebbe la generazione
/// di id in qualunque futuro tipo di <c>Common/</c>.
/// </para>
/// </summary>
public sealed class R41_IdGenerationTests
{
    private const string GeneratorFile = "src/Roamly.Common/SequentialGuidGenerator.cs";

    private static readonly PathExemption CombGenerator = new(
        GeneratorFile,
        MustExistToday: true,
        "ADR-0008: e' l'unica sorgente di Id del sistema. E' esso stesso a chiamare Guid.NewGuid() e "
        + "new Guid(byte[]) per costruire il COMB, quindi deve restare lecito esattamente li'.");

    /// <summary>
    /// Riconosce un segmento di percorso che e' il dominio: sia <c>src/Roamly.Domain/</c> (forma odierna)
    /// sia <c>src/&lt;Modulo&gt;/Domain/</c> (forma prescritta da ADR-0008, oggi inesistente).
    /// Dichiarato prima dei divieti: e' referenziato dai loro inizializzatori.
    /// </summary>
    private static readonly Regex DomainSegment = new(
        @"(^|/)[^/]*Domain[^/]*/",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Istanza (a): dentro il dominio nessun <c>Guid</c> nasce a mano, in nessuna delle tre forme.
    /// Il perimetro e' <c>src/**/Domain/**</c>, che oggi si materializza in <c>src/Roamly.Domain/</c>.
    /// </summary>
    internal static PathScopedBan DomainBan { get; } = new()
    {
        RuleId = "R41",
        Source = "ADR-0008",
        PerimeterDescription = "src/**/Domain/** (oggi src/Roamly.Domain/)",
        Perimeter = path =>
            path.StartsWith("src/", StringComparison.OrdinalIgnoreCase)
            && DomainSegment.IsMatch(path),
        Exemptions = [CombGenerator],
        Symbols =
        [
            new BannedSymbol(
                "Guid.NewGuid()",
                @"\bGuid\s*\.\s*NewGuid\s*\(",
                "Guid v4 e' casuale: come clustering key produce page split su ogni insert (ADR-0008)."),
            new BannedSymbol(
                "Guid.CreateVersion7()",
                @"\bGuid\s*\.\s*CreateVersion7\s*\(",
                "RFC 9562 mette il timestamp nei byte 0-5; SQL Server ordina uniqueidentifier dai byte 10-15, "
                + "quindi v7 NON e' sequenziale su questo motore (ADR-0008)."),
            new BannedSymbol(
                "new Guid(...)",
                @"\bnew\s+Guid\s*\(",
                "Costruire un Guid a mano aggira il generatore tanto quanto generarlo."),
        ],
    };

    /// <summary>
    /// Istanza (b): <c>Guid.CreateVersion7()</c> e' vietata <b>ovunque</b> nella persistenza, non solo nel
    /// dominio. Qui il perimetro e' tutto <c>src/</c>: e' piu' largo di "persistenza" ed e' deliberato, perche'
    /// v7 non e' mai la risposta giusta su SQL Server, in nessun progetto di questa soluzione.
    /// </summary>
    internal static PathScopedBan Version7Ban { get; } = new()
    {
        RuleId = "R41(b)",
        Source = "ADR-0008",
        PerimeterDescription = "src/ (persistenza e oltre)",
        Perimeter = path => path.StartsWith("src/", StringComparison.OrdinalIgnoreCase),
        Exemptions = [CombGenerator],
        Symbols =
        [
            new BannedSymbol(
                "Guid.CreateVersion7()",
                @"\bGuid\s*\.\s*CreateVersion7\s*\(",
                "Vietata ovunque nella persistenza (ADR-0008): il commento che spiega perche' vive nel "
                + "generatore COMB, che e' l'unico percorso esentato."),
        ],
    };

    /// <summary>Nessun tipo di dominio genera o costruisce un <c>Guid</c>.</summary>
    [Fact]
    public void Domain_types_do_not_create_guids()
        => DomainBan.AssertNoViolations();

    /// <summary>Nessun punto di <c>src/</c> usa <c>Guid.CreateVersion7()</c>.</summary>
    [Fact]
    public void Version7_guids_are_absent_from_production_code()
        => Version7Ban.AssertNoViolations();

    /// <summary>
    /// Il generatore COMB esiste al percorso esentato. Se venisse rinominato, l'esenzione punterebbe nel vuoto
    /// e il lint diventerebbe rosso proprio nell'unico luogo in cui il simbolo e' lecito.
    /// </summary>
    [Fact]
    public void The_comb_generator_exemption_points_at_an_existing_file()
    {
        DomainBan.AssertDeclaredExemptionsExist();
        Version7Ban.AssertDeclaredExemptionsExist();
    }

    /// <summary>
    /// Casi negativi: il generatore e' esentato davvero, e i nomi che contengono <c>Guid</c> senza generarne
    /// uno non fanno scattare il lint.
    /// </summary>
    [Fact]
    public void The_generator_is_exempt_and_lookalikes_are_not_flagged()
    {
        var outcome = Version7Ban.Run();

        Assert.Contains(GeneratorFile, outcome.ExemptedFiles, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(GeneratorFile, outcome.InspectedFiles, StringComparer.OrdinalIgnoreCase);

        var newGuid = DomainBan.Symbols.First(s => s.Symbol == "Guid.NewGuid()").Matcher;
        var ctor = DomainBan.Symbols.First(s => s.Symbol == "new Guid(...)").Matcher;

        Assert.False(newGuid.IsMatch("public Guid Id { get; init; }"), "Una proprieta' Guid e' stata segnalata.");
        Assert.False(newGuid.IsMatch("var empty = Guid.Empty;"), "Guid.Empty e' stato segnalato.");
        Assert.False(ctor.IsMatch("var parsed = Guid.Parse(text);"), "Guid.Parse e' stato segnalato.");
        Assert.False(ctor.IsMatch("private readonly IIdGenerator _ids;"), "Un campo IIdGenerator e' stato segnalato.");

        Assert.True(newGuid.IsMatch("var id = Guid.NewGuid();"), "Guid.NewGuid() non riconosciuto.");
        Assert.True(ctor.IsMatch("return new Guid(bytes);"), "new Guid(bytes) non riconosciuto.");
    }

    /// <summary>
    /// Il perimetro del dominio riconosce sia la forma odierna <c>src/Roamly.Domain/</c> sia la forma
    /// <c>src/&lt;Modulo&gt;/Domain/</c> prescritta da ADR-0008, che oggi non esiste.
    /// </summary>
    [Fact]
    public void The_domain_perimeter_matches_both_layouts()
    {
        Assert.True(DomainBan.Perimeter("src/Roamly.Domain/Entities/Camper.cs"), "Layout odierno non riconosciuto.");
        Assert.True(DomainBan.Perimeter("src/Roamly.Api/Features/Campers/Domain/Rules.cs"), "Layout modulare non riconosciuto.");
        Assert.False(DomainBan.Perimeter("src/Roamly.Infrastructure/Persistence/RoamlyDbContext.cs"), "Perimetro troppo largo.");
        Assert.False(DomainBan.Perimeter("tests/Roamly.Domain.Tests/Foo.cs"), "Il perimetro ha inglobato tests/.");
    }
}
