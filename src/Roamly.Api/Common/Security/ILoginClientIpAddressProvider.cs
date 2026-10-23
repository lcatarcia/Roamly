using System.Net;

namespace Roamly.Api.Common.Security;

/// <summary>Risolve l'indirizzo del client per il rate limit login.</summary>
public interface ILoginClientIpAddressProvider
{
    /// <summary>Restituisce l'indirizzo client attendibile, oppure null se assente/non valido.</summary>
    IPAddress? GetClientIpAddress(HttpContext context);
}

/// <summary>
/// In produzione usa il solo ultimo elemento di X-Forwarded-For, aggiunto dall'ingress ACA.
/// In Development e Testing ignora gli header inoltrati e usa la connessione controllata dal test.
/// </summary>
public sealed class LoginClientIpAddressProvider : ILoginClientIpAddressProvider
{
    private readonly IHostEnvironment _environment;

    /// <summary>Costruisce il provider per l'ambiente attivo.</summary>
    public LoginClientIpAddressProvider(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    /// <inheritdoc />
    public IPAddress? GetClientIpAddress(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
        {
            return context.Connection.RemoteIpAddress;
        }

        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        var lastValue = forwardedFor.Split(',', StringSplitOptions.TrimEntries).LastOrDefault();

        return IPAddress.TryParse(lastValue, out var address) ? address : null;
    }
}
