using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Common.Startup;

/// <summary>
/// Persistenza: <see cref="RoamlyDbContext"/>, ASP.NET Core Identity, e l'intercettore di R3.
/// La connection string arriva dalla chiave <c>ConnectionStrings:Roamly</c> (user-secrets in
/// sviluppo, environment variable altrove) — fail-fast se assente: nessuna stringa di comodo,
/// nessun fallback silenzioso su un database sbagliato.
/// </summary>
public static class PersistenceRegistration
{
    /// <summary>Registra <see cref="RoamlyDbContext"/> e Identity sul contesto della Phase 1.</summary>
    /// <param name="services">Collezione dei servizi.</param>
    /// <param name="configuration">Configurazione da cui leggere la connection string.</param>
    /// <returns>La collezione, per concatenare le altre registrazioni.</returns>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Roamly");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string \"ConnectionStrings:Roamly\" assente. Configurala con " +
                "\"dotnet user-secrets set ConnectionStrings:Roamly \"...\"\" in sviluppo, o con la " +
                "environment variable corrispondente altrove. Nessun fallback e' previsto di proposito.");
        }

        services.AddDbContext<RoamlyDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
            options.AddInterceptors(new OwnershipInterceptor());
        });

        services
            .AddIdentityCore<ApplicationUser>(identityOptions =>
            {
                identityOptions.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddSignInManager()
            .AddEntityFrameworkStores<RoamlyDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}
