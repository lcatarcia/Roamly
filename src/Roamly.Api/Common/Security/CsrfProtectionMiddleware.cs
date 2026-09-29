using Microsoft.AspNetCore.Antiforgery;
using Roamly.Api.Common.Http;

namespace Roamly.Api.Common.Security;

/// <summary>
/// Seconda barriera CSRF (SECURITY.md §1): ogni richiesta non-GET deve portare l'header custom
/// <c>X-Roamly-Request</c> (i form HTML cross-site non possono impostare header custom senza far
/// scattare un preflight CORS, bloccato dall'allowlist) **e** superare la validazione antiforgery
/// standard (<see cref="IAntiforgery"/>, coppia cookie/header ottenuta da
/// <c>GET /api/v1/csrf-token</c>). Le due barriere sono indipendenti: un client che aggirasse
/// l'header custom cadrebbe comunque sul token antiforgery, e viceversa.
/// </summary>
public sealed class CsrfProtectionMiddleware
{
    private const string CustomHeaderName = "X-Roamly-Request";
    private const string ProblemType = "https://roamly.dev/problems/csrf-protection";

    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace,
    };

    private readonly RequestDelegate _next;
    private readonly IAntiforgery _antiforgery;

    /// <summary>Costruisce il middleware con il prossimo delegato e il servizio antiforgery.</summary>
    /// <param name="next">Prossimo delegato della pipeline.</param>
    /// <param name="antiforgery">Servizio antiforgery su cui validare la coppia cookie/header.</param>
    public CsrfProtectionMiddleware(RequestDelegate next, IAntiforgery antiforgery)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(antiforgery);

        _next = next;
        _antiforgery = antiforgery;
    }

    /// <summary>Applica le due barriere alle richieste non sicure, poi invoca il resto della pipeline.</summary>
    /// <param name="context">Contesto della richiesta corrente.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (SafeMethods.Contains(context.Request.Method))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (!context.Request.Headers.ContainsKey(CustomHeaderName))
        {
            await WriteProblemAsync(context, "Header di sicurezza mancante.").ConfigureAwait(false);
            return;
        }

        try
        {
            await _antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException)
        {
            await WriteProblemAsync(context, "Token antiforgery mancante o non valido.").ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static async Task WriteProblemAsync(HttpContext context, string title)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        var problem = ProblemFactory.Create(context, StatusCodes.Status400BadRequest, title, ProblemType);
        await context.Response.WriteAsJsonAsync(problem).ConfigureAwait(false);
    }
}
