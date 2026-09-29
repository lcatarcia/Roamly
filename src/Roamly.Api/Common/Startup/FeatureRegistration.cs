using Roamly.Api.Common.Email;
using Roamly.Api.Features.Authentication.ConfirmEmail;
using Roamly.Api.Features.Authentication.Login;
using Roamly.Api.Features.Authentication.Logout;
using Roamly.Api.Features.Authentication.Register;
using Roamly.Api.Features.Identity.GetMe;
using Roamly.Common.Email;

namespace Roamly.Api.Common.Startup;

/// <summary>
/// Registrazione esplicita degli handler di ogni slice (R54, D1/@solomon): una riga per slice,
/// mai riflessione. E' la meta' "registrazione" della tautologia strutturalmente impossibile che
/// R44 verifica: se una slice esiste sotto <c>Features/</c> ma non ha una riga qui, R44 la trova
/// mancante, non R54 stesso — R54 non ha modo di sapere che cosa manca.
/// </summary>
public static class FeatureRegistration
{
    /// <summary>Registra gli handler di tutte le slice applicative.</summary>
    /// <param name="services">Collezione dei servizi.</param>
    /// <returns>La collezione, per concatenare le altre registrazioni.</returns>
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Features/Authentication/
        services.AddScoped<RegisterHandler>();
        services.AddScoped<ConfirmEmailHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<LogoutHandler>();

        // Features/Identity/
        services.AddScoped<GetMeHandler>();

        // Fake di sviluppo: nessun provider SMTP e' stato scelto (OPEN-DECISIONS.md). Sostituirlo
        // e' l'unica riga da cambiare quando arriva un provider reale.
        services.AddSingleton<IEmailSender, LoggingEmailSender>();

        return services;
    }
}
