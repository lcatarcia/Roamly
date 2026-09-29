namespace Roamly.Api.Common.Startup;

/// <summary>
/// Composition root (R54): un unico metodo di estensione puro, senza I/O, che compone le
/// registrazioni per area. "Puro" e' la condizione tecnica di R44 (<c>ARCHITECTURE.md</c> §4):
/// un verificatore L0 che costruisse davvero l'host aprirebbe connessioni vere, il che lo
/// sposterebbe fuori dal budget del L0 e nella suite con Testcontainers.
/// </summary>
public static class RoamlyServiceCollectionExtensions
{
    /// <summary>Registra tutti i servizi applicativi di Roamly.</summary>
    /// <param name="services">Collezione dei servizi.</param>
    /// <param name="configuration">Configurazione dell'host.</param>
    /// <returns>La collezione, per concatenare l'avvio dell'host.</returns>
    public static IServiceCollection AddRoamly(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services
            .AddPersistence(configuration)
            .AddSecurity(configuration)
            .AddHttpConcerns()
            .AddObservability()
            .AddFeatures();
    }
}
