using System.Text.RegularExpressions;

namespace Roamly.Model.Tests.Conventions;

/// <summary>
/// Sotto <c>tests/</c> non vive solo codice di test: <c>Roamly.TestSupport</c> e' una libreria
/// (builder e registro di R33). <c>tests/Directory.Build.props</c> imporrebbe a tutto cio' che sta
/// li' sotto <c>OutputType=Exe</c> e il runner di Microsoft.Testing.Platform — che tratta
/// "zero test eseguiti" come <b>fallimento con exit code 8</b> (TESTING.md §13).
/// <para>
/// Questo verificatore e' nello spirito di R30 e R36: legge i <c>.csproj</c> per via testuale e
/// asserisce che il discriminatore sia in vigore. Senza, il guasto non e' un errore di
/// compilazione ma una solution che esce 8 con "non riuscito: 0" — un fallimento che non sembra tale.
/// </para>
/// </summary>
public sealed class TestProjectShapeTests
{
    [Fact]
    public void No_library_under_tests_is_built_as_a_test_executable()
    {
        var offenders = LibraryProjects()
            .Where(project => Regex.IsMatch(
                File.ReadAllText(project),
                @"<(OutputType|UseMicrosoftTestingPlatformRunner)\s*>",
                RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(1)))
            .Select(RepositoryRoot.Relative)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Progetti sotto tests/ che non finiscono in 'Tests' ma dichiarano OutputType o il runner: "
            + string.Join(", ", offenders)
            + ". Conseguenza: Microsoft.Testing.Platform li esegue come progetti di test senza test, "
            + "e 'dotnet test --solution Roamly.slnx' esce con exit code 8 pur con zero fallimenti.");
    }

    /// <summary>
    /// La condizione deve restare nel file delle props: rimuoverla non romperebbe la build, solo
    /// l'esecuzione della suite — cioe' proprio il tipo di guasto che si scopre tardi.
    /// </summary>
    [Fact]
    public void The_tests_props_apply_the_test_platform_only_to_test_projects()
    {
        var props = Path.Combine(RepositoryRoot.Path, "tests", "Directory.Build.props");
        var content = File.ReadAllText(props);

        var guarded = Regex.Matches(
            content,
            @"Condition\s*=\s*""'\$\(IsTestProject\)'\s*==\s*'true'""",
            RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));

        Assert.True(
            guarded.Count >= 3,
            "tests/Directory.Build.props condiziona su IsTestProject solo " + guarded.Count
            + " gruppi (attesi almeno 3: OutputType/runner, i PackageReference di xunit, l'using globale). "
            + "Conseguenza: Roamly.TestSupport tornerebbe a essere trattata come progetto di test.");
    }

    private static IEnumerable<string> LibraryProjects()
        => Directory.EnumerateFiles(
                Path.Combine(RepositoryRoot.Path, "tests"),
                "*.csproj",
                SearchOption.AllDirectories)
            .Where(project => !Path.GetFileNameWithoutExtension(project)
                .EndsWith("Tests", StringComparison.Ordinal));
}
