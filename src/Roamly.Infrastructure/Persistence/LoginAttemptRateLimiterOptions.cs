namespace Roamly.Infrastructure.Persistence;

/// <summary>Configurazione del rate limiter distribuito del login.</summary>
public sealed class LoginAttemptRateLimiterOptions
{
    /// <summary>Chiave HMAC-SHA-256 Base64, stabile e con almeno 256 bit casuali.</summary>
    public string? HmacSecret { get; set; }

    /// <summary>Credenziali errate massime per account.</summary>
    public int MaxFailedAttemptsPerAccount { get; set; } = 10;

    /// <summary>Finestra fissa del contatore account.</summary>
    public TimeSpan AccountWindow { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Richieste massime per IP nella finestra breve.</summary>
    public int MaxAttemptsPerIpPerFiveMinutes { get; set; } = 30;

    /// <summary>Finestra IP breve.</summary>
    public TimeSpan IpFiveMinuteWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Richieste massime per IP nella finestra oraria.</summary>
    public int MaxAttemptsPerIpPerHour { get; set; } = 100;

    /// <summary>Finestra IP oraria.</summary>
    public TimeSpan IpHourlyWindow { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Verifica la configurazione richiesta per eseguire il login fail-closed.</summary>
    public static bool IsValid(LoginAttemptRateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return HasStrongHmacSecret(options.HmacSecret)
            && options.MaxFailedAttemptsPerAccount > 0
            && options.AccountWindow > TimeSpan.Zero
            && options.MaxAttemptsPerIpPerFiveMinutes > 0
            && options.IpFiveMinuteWindow > TimeSpan.Zero
            && options.MaxAttemptsPerIpPerHour > 0
            && options.IpHourlyWindow > TimeSpan.Zero;
    }

    /// <summary>Accetta esclusivamente una chiave Base64 con almeno 256 bit.</summary>
    public static bool HasStrongHmacSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(value).Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
