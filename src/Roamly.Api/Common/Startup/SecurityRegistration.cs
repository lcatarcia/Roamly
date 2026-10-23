using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Ownership;
using Roamly.Api.Common.Security;
using Roamly.Api.Features.Authentication.Login;
using Roamly.Common;
using Roamly.Common.Security;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Common.Startup;

/// <summary>
/// Autenticazione a cookie (R60), <see cref="ICurrentUser"/> reale (R56), antiforgery e CORS.
/// Deliberatamente <b>non</b> <c>MapIdentityApi</c> (<c>SECURITY.md</c> §1): gli endpoint di auth
/// sono una slice come le altre, in <c>Features/Authentication/</c>, conformi a
/// <c>API-CONVENTIONS.md</c>.
/// </summary>
public static class SecurityRegistration
{
    /// <summary>Nome della policy CORS con allowlist esplicita e credenziali.</summary>
    public const string CorsPolicyName = "RoamlyClient";

    /// <summary>Registra autenticazione, autorizzazione, antiforgery e CORS.</summary>
    /// <param name="services">Collezione dei servizi.</param>
    /// <param name="configuration">Configurazione da cui leggere l'allowlist CORS.</param>
    /// <returns>La collezione, per concatenare le altre registrazioni.</returns>
    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserScope, CurrentUserScope>();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        // AddIdentityCore non registra da solo uno schema di autenticazione (a differenza di
        // AddIdentity, che porta con se' una cookie-app pensata per Razor Pages): lo schema va
        // dichiarato qui, esplicitamente, come IdentityConstants.ApplicationScheme — lo stesso
        // nome che SignInManager/AddSignInManager si aspettano di trovare gia' registrato.
        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme);

        services.ConfigureApplicationCookie(cookieOptions =>
        {
            cookieOptions.Cookie.HttpOnly = true;
            cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookieOptions.Cookie.SameSite = SameSiteMode.Lax;

            // Nessun redirect verso una pagina di login: e' un'API, non un'app server-rendered.
            // Il default di Identity risponderebbe 302 su ogni 401/403, un comportamento pensato
            // per Razor Pages che qui romperebbe ogni client HTTP.
            cookieOptions.Events.OnRedirectToLogin = context =>
                WriteProblemAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "Autenticazione richiesta.",
                    "https://roamly.dev/problems/unauthorized");

            cookieOptions.Events.OnRedirectToAccessDenied = context =>
                WriteProblemAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "Accesso non consentito.",
                    "https://roamly.dev/problems/forbidden");
        });

        services.Configure<SecurityStampValidatorOptions>(stampOptions =>
        {
            stampOptions.ValidationInterval = TimeSpan.FromMinutes(5);
            stampOptions.OnRefreshingPrincipal = refresh =>
            {
                var currentPrincipal = refresh.CurrentPrincipal;
                var newPrincipal = refresh.NewPrincipal;
                if (currentPrincipal is not null && newPrincipal?.Identity is ClaimsIdentity newIdentity)
                {
                    CopySessionClaims(currentPrincipal, newIdentity, AccountSessionClaims.ModeClaimType);
                    CopySessionClaims(currentPrincipal, newIdentity, PasswordAuthenticationClaims.AuthenticatedAtUtc);
                }

                return Task.CompletedTask;
            };
        });

        services.AddAuthorization();

        // Header custom letto dallo stesso nome sul client (X-XSRF-TOKEN e' la convenzione delle
        // SPA che leggono il cookie non-httpOnly e lo rimandano come header) — seconda barriera,
        // indipendente dall'header X-Roamly-Request applicato da CsrfProtectionMiddleware.
        services.AddAntiforgery(antiforgeryOptions =>
        {
            antiforgeryOptions.HeaderName = "X-XSRF-TOKEN";
        });

        services.AddMemoryCache();
        services.AddSingleton<IPerAccountRateLimiter, PerAccountRateLimiter>();
        services.Configure<PerAccountRateLimiterOptions>(configuration.GetSection("RateLimiting:Login"));
        services.AddSingleton<DummyLoginPasswordHash>();
        services.AddScoped<LoginAttemptRateLimiter>();
        services.AddScoped<ILoginAttemptRateLimiter>(
            provider => provider.GetRequiredService<LoginAttemptRateLimiter>());
        services.AddScoped<ILoginAttemptCounterMaintenance>(
            provider => provider.GetRequiredService<LoginAttemptRateLimiter>());
        services.AddScoped<ILoginClientIpAddressProvider, LoginClientIpAddressProvider>();
        services.AddOptions<LoginAttemptRateLimiterOptions>()
            .Bind(configuration.GetSection("RateLimiting:Login"))
            .Validate(LoginAttemptRateLimiterOptions.IsValid,
                "RateLimiting:Login requires a Base64 HMAC key of at least 32 bytes and positive limits/windows.")
            .ValidateOnStart();

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(corsOptions => corsOptions.AddPolicy(CorsPolicyName, policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }));

        return services;
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title, string type)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = ProblemFactory.Create(context, statusCode, title, type);
        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            problem,
            JsonSerializerOptions.Web,
            context.RequestAborted);
    }

    private static void CopySessionClaims(ClaimsPrincipal currentPrincipal, ClaimsIdentity refreshedIdentity, string claimType)
    {
        foreach (var claim in refreshedIdentity.FindAll(claimType).ToArray())
        {
            refreshedIdentity.RemoveClaim(claim);
        }

        foreach (var claim in currentPrincipal.FindAll(claimType))
        {
            refreshedIdentity.AddClaim(claim);
        }
    }
}
