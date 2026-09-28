using System.Text.RegularExpressions;

namespace Roamly.Model.Tests.Conventions;

/// <summary>
/// Individua la radice del repository risalendo dalla cartella di esecuzione fino al file
/// <c>Roamly.slnx</c>. I verificatori testuali (R30, R36 e la forma dei progetti) leggono file
/// sorgente, non assembly: hanno bisogno di un percorso, e dedurlo da un numero fisso di
/// <c>..</c> si rompe al primo cambio di target framework.
/// </summary>
internal static class RepositoryRoot
{
    /// <summary>Percorso assoluto della radice del repository.</summary>
    public static string Path { get; } = Locate();

    /// <summary>
    /// File di codice della soluzione: sorgenti, progetti, property e workflow. <b>Esclude
    /// <c>docs/</c></b>, dove i tag compaiono legittimamente come citazione, e gli artefatti di build.
    /// </summary>
    /// <returns>I percorsi dei file da ispezionare.</returns>
    public static IReadOnlyList<string> CodeFiles()
    {
        string[] patterns = ["*.cs", "*.csproj", "*.props", "*.targets", "*.slnx", "*.yml", "*.yaml", "*.json"];

        return [.. patterns
            .SelectMany(pattern => Directory.EnumerateFiles(Path, pattern, SearchOption.AllDirectories))
            .Where(file => !Regex.IsMatch(
                file[Path.Length..],
                @"[\\/](bin|obj|docs|\.git|TestResults)[\\/]",
                RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(1)))
            .OrderBy(file => file, StringComparer.Ordinal)];
    }

    /// <summary>Percorso relativo alla radice, per messaggi leggibili.</summary>
    /// <param name="absolutePath">Percorso assoluto.</param>
    /// <returns>Percorso relativo.</returns>
    public static string Relative(string absolutePath)
    {
        ArgumentNullException.ThrowIfNull(absolutePath);
        return System.IO.Path.GetRelativePath(Path, absolutePath);
    }

    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "Roamly.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Radice del repository non trovata risalendo da " + AppContext.BaseDirectory
            + ": manca Roamly.slnx. I verificatori testuali non possono girare, e un verificatore "
            + "che non trova i file da leggere non e' verde, e' inerte.");
    }
}
