using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Roamly.Api.Common.Startup;
using Xunit;

namespace Roamly.Model.Tests.Conventions;

/// <summary>
/// <b>R44 (ARCHITECTURE.md §4, decisione #5):</b> ogni tipo il cui nome termina in <c>Handler</c>
/// sotto <c>Features/</c> e' risolvibile dal container di <c>Roamly.Api</c>. Senza mediator, questo
/// e' l'unico presidio contro un handler nuovo e mai registrato in <see cref="FeatureRegistration"/>.
/// <para>
/// Tre insiemi indipendenti, cosi' un bug in uno non maschera un buco nell'altro (R38):
/// <b>T</b> testuale (nomi file <c>*Handler.cs</c> sotto <c>src/Roamly.Api/Features</c>),
/// <b>R</b> riflessivo (<see cref="Assembly.GetTypes"/>, mai <c>GetExportedTypes</c>: gli handler
/// sono <c>internal</c>), <b>C</b> container (il grafo DI reale costruito da <see cref="RoamlyServiceCollectionExtensions.AddRoamly"/>
/// con <c>validateScopes:true</c>/<c>validateOnBuild:true</c> su una connection string finta).
/// </para>
/// <para>
/// Vincoli L0: mai <c>WebApplicationFactory</c>, mai <c>builder.Build()</c> di un host, nessun
/// hosted service in esecuzione. <see cref="ServiceProviderOptions.ValidateOnBuild"/> costruisce
/// ogni servizio in uno scope di validazione — anche <c>RoamlyDbContext</c> — ma costruire un
/// <c>DbContext</c> con delle <c>DbContextOptions</c> non apre mai una connessione: e' lo stesso
/// principio per cui <c>ModelFixture</c> resta offline.
/// </para>
/// </summary>
public sealed class R44_HandlerRegistrationTests
{
    private static readonly string FeaturesRoot = Path.Combine(RepositoryRoot.Path, "src", "Roamly.Api", "Features");

    [Fact]
    public void Every_handler_file_has_a_matching_type_discoverable_by_reflection()
    {
        var textualNames = DiscoverHandlerNamesFromFileSystem();
        var reflectedNames = DiscoverHandlerTypesFromAssembly().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        var missingFromAssembly = textualNames.Except(reflectedNames).ToList();

        Assert.True(
            missingFromAssembly.Count == 0,
            "File *Handler.cs senza un tipo omonimo nell'assembly Roamly.Api: "
            + string.Join(", ", missingFromAssembly)
            + ". Una cartella rinominata o un tipo rinominato senza il file, o viceversa, "
            + "romperebbe il confronto senza che R44 se ne accorga.");
    }

    [Fact]
    public void Every_handler_type_is_resolvable_from_the_composition_root_container()
    {
        var handlerTypes = DiscoverHandlerTypesFromAssembly();

        Assert.NotEmpty(handlerTypes);

        using var provider = BuildValidatedContainer();
        using var scope = provider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        var unresolvable = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            try
            {
                var instance = scopedProvider.GetService(handlerType);

                if (instance is null)
                {
                    unresolvable.Add(handlerType.FullName + " (nessuna registrazione)");
                }
            }
            catch (InvalidOperationException exception)
            {
                unresolvable.Add(handlerType.FullName + " (" + exception.Message + ")");
            }
        }

        Assert.True(
            unresolvable.Count == 0,
            "Handler non risolvibili dal container di FeatureRegistration.AddFeatures: "
            + string.Join("; ", unresolvable));
    }

    private static ServiceProvider BuildValidatedContainer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Roamly"] = "Server=fake;Database=fake;TrustServerCertificate=True",
            })
            .Build();

        var services = new ServiceCollection();

        // TimeProvider.System e' registrato di default dall'host generico (WebApplication.CreateBuilder);
        // qui costruiamo un ServiceCollection nudo, quindi va aggiunto a mano — non e' un host, e' un
        // singolo servizio di libreria, esattamente come nella produzione.
        services.AddSingleton(TimeProvider.System);

        // AddAuthorization() (dentro AddSecurity) registra AuthorizationPolicyCache, che richiede un
        // EndpointDataSource — normalmente fornito da UseRouting() sull'host reale. Un
        // DefaultEndpointDataSource vuoto e' un dettaglio del framework, non un host: non fa girare
        // nessuna pipeline HTTP, serve solo a soddisfare la validazione del grafo DI.
        services.AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource());

        services.AddRoamly(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    private static HashSet<string> DiscoverHandlerNamesFromFileSystem()
    {
        return [.. Directory
            .EnumerateFiles(FeaturesRoot, "*Handler.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Select(name => name!)];
    }

    private static List<Type> DiscoverHandlerTypesFromAssembly()
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
