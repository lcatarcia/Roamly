using System.Text.RegularExpressions;

namespace Roamly.Model.Tests.Conventions;

/// <summary>
/// <b>R36 (ADR-0009)</b>: il tag dell'immagine SQL Server e' pinnato in un punto unico e versionato.
/// Mai <c>latest</c>, mai duplicato fra fixture e workflow.
/// <para>
/// Il verificatore e' testuale come quelli di R30: legge i file di codice della soluzione e conta
/// le occorrenze del registro. Il <c>ci.yml</c> del Blocco 5 dovra' riusare <b>quella</b> costante,
/// non riscrivere il tag: un tag duplicato diverge in silenzio, e la CI finisce per testare
/// un'immagine diversa da quella locale.
/// </para>
/// <para>
/// ⚠️ Ambito dichiarato: la scansione esclude <c>docs/</c>, dove il tag compare come citazione in
/// ADR-0009 e in TESTING.md §4.1. Il vincolo e' sul <b>codice eseguibile</b>, non sulla prosa.
/// </para>
/// </summary>
public sealed class R36_PinnedSqlServerImageTests
{
    // Spezzato in due letterali di proposito: cosi' questo file, che pure parla del registro,
    // non e' esso stesso un'occorrenza della stringa cercata.
    private const string Registry = "mcr.microsoft.com/" + "mssql";

    [Fact]
    public void The_sql_server_image_tag_appears_exactly_once_in_the_solution()
    {
        var occurrences = RepositoryRoot.CodeFiles()
            .Select(file => (File: file, Hits: CountOccurrences(File.ReadAllText(file))))
            .Where(x => x.Hits > 0)
            .ToList();

        var total = occurrences.Sum(x => x.Hits);

        Assert.True(
            total == 1,
            "Occorrenze del registro dell'immagine SQL Server nel codice: " + total + " (attesa: 1). "
            + "Trovate in: "
            + string.Join(", ", occurrences.Select(x => RepositoryRoot.Relative(x.File) + " x" + x.Hits))
            + ". Conseguenza di un duplicato: il tag locale e quello della CI divergono senza che "
            + "nulla diventi rosso, e i test girano su due motori diversi (R36, ADR-0009).");
    }

    [Fact]
    public void The_pinned_tag_is_not_a_floating_tag()
    {
        var lines = RepositoryRoot.CodeFiles()
            .SelectMany(File.ReadAllLines)
            .Where(line => line.Contains(Registry, StringComparison.Ordinal))
            .ToList();

        Assert.True(lines.Count == 1, "Atteso un solo punto di pin; trovati " + lines.Count + ".");

        var pinned = lines[0];

        Assert.DoesNotContain("latest", pinned, StringComparison.OrdinalIgnoreCase);

        Assert.True(
            Regex.IsMatch(pinned, @":\d{4}-CU\d+-ubuntu-\d+\.\d+", RegexOptions.None, TimeSpan.FromSeconds(1)),
            "Il tag pinnato non ha la forma '<anno>-CU<n>-ubuntu-<versione>': " + pinned.Trim()
            + ". Un tag senza cumulative update non identifica un'immagine riproducibile.");
    }

    private static int CountOccurrences(string content)
    {
        var count = 0;
        var index = content.IndexOf(Registry, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = content.IndexOf(Registry, index + Registry.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
