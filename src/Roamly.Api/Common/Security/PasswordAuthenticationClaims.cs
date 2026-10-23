using System.Globalization;
using System.Security.Claims;

namespace Roamly.Api.Common.Security;

/// <summary>Claim firmato nel ticket cookie che attesta un login/password completato.</summary>
internal static class PasswordAuthenticationClaims
{
    private static readonly TimeSpan MaximumAge = TimeSpan.FromMinutes(5);

    /// <summary>Tipo claim riservato al login/password di Identity.</summary>
    public const string AuthenticatedAtUtc = "roamly:password-authenticated-at-utc";

    /// <summary>Crea la data UTC da aggiungere al cookie dopo una verifica password riuscita.</summary>
    public static Claim Create(TimeProvider clock) =>
        new(AuthenticatedAtUtc, clock.GetUtcNow().UtcDateTime.ToString("O", CultureInfo.InvariantCulture));

    /// <summary>Rifiuta claim assente, duplicato, malformato, futuro o più vecchio di cinque minuti.</summary>
    public static bool IsFresh(ClaimsPrincipal principal, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var claims = principal.FindAll(AuthenticatedAtUtc).ToArray();
        if (claims.Length != 1
            || !DateTime.TryParseExact(
                claims[0].Value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var authenticatedAtUtc)
            || authenticatedAtUtc.Kind != DateTimeKind.Utc)
        {
            return false;
        }

        var age = now.UtcDateTime - authenticatedAtUtc;
        return age >= TimeSpan.Zero && age <= MaximumAge;
    }
}
