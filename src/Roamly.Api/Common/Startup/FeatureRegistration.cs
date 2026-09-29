namespace Roamly.Api.Common.Startup;

/// <summary>
/// Registrazione esplicita degli handler di ogni slice (R54, D1/@solomon): una riga per slice,
/// mai riflessione. E' la meta' "registrazione" della tautologia strutturalmente impossibile che
/// R44 verifica: se una slice esiste sotto <c>Features/</c> ma non ha una riga qui, R44 la trova
/// mancante, non R54 stesso — R54 non ha modo di sapere che cosa manca.
/// <para>
/// ⚠️ Vuoto finche' non esiste la prima slice (<c>Features/Authentication/</c>). Non e' un
/// placeholder dimenticato: e' esattamente cio' che rende R44 verificabile invece che vacuo, il
/// giorno in cui la prima riga arriva.
/// </para>
/// </summary>
public static class FeatureRegistration
{
    /// <summary>Registra gli handler di tutte le slice applicative.</summary>
    /// <param name="services">Collezione dei servizi.</param>
    /// <returns>La collezione, per concatenare le altre registrazioni.</returns>
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
