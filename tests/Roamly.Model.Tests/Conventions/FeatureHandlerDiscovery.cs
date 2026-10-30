using System.Reflection;
using System.Text.RegularExpressions;
using Roamly.Api.Common.Startup;

namespace Roamly.Model.Tests.Conventions;

internal static class FeatureHandlerDiscovery
{
    private static readonly string FeaturesRoot = Path.Combine(
        RepositoryRoot.Path,
        "src",
        "Roamly.Api",
        "Features");

    internal static HashSet<string> DiscoverHandlerNamesFromFileSystem()
    {
        return [.. Directory
            .EnumerateFiles(FeaturesRoot, "*Handler.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Select(name => name!)];
    }

    internal static List<Type> DiscoverHandlerTypesFromAssembly()
    {
        var assembly = typeof(RoamlyServiceCollectionExtensions).Assembly;

        return [.. assembly
            .GetTypes()
            .Where(type =>
                type.Name.EndsWith("Handler", StringComparison.Ordinal)
                && type.Namespace is not null
                && Regex.IsMatch(type.Namespace, @"^Roamly\.Api\.Features(\.|$)", RegexOptions.None, TimeSpan.FromSeconds(1)))];
    }
}
