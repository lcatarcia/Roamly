using Roamly.Api.Common.Http;

namespace Roamly.Api.Common.Startup;

/// <summary>
/// Pipeline HTTP: gestione centralizzata delle eccezioni (R45/R46) e validazione nativa (R48).
/// </summary>
public static class HttpRegistration
{
    /// <summary>Registra <see cref="RoamlyExceptionHandler"/>, <c>ProblemDetails</c> e validazione.</summary>
    /// <param name="services">Collezione dei servizi.</param>
    /// <returns>La collezione, per concatenare le altre registrazioni.</returns>
    public static IServiceCollection AddHttpConcerns(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionHandler<RoamlyExceptionHandler>();

        // AddProblemDetails() da solo copre i 4xx/5xx generati dal framework (404 di routing,
        // 400 di model binding). RoamlyExceptionHandler resta l'unico punto che costruisce un
        // ProblemDetails per un'eccezione applicativa non gestita (R45): mai il default ereditato.
        services.AddProblemDetails();

        // Validazione nativa .NET 10 (R48), non FluentValidation: produce HttpValidationProblemDetails
        // con "errors" chiavizzato sui nomi JSON dei campi.
        services.AddValidation();

        return services;
    }
}
