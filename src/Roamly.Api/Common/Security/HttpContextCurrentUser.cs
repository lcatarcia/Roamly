using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Roamly.Common;

namespace Roamly.Api.Common.Security;

/// <summary>
/// <see cref="ICurrentUser"/> vero, letto dal cookie di autenticazione della richiesta HTTP in corso.
/// E' l'unica implementazione che il container vede durante una richiesta reale (R56: registrata
/// con scope <c>Scoped</c>, mai <c>Singleton</c> — <see cref="IHttpContextAccessor"/> cambia contesto
/// a ogni richiesta).
/// </summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Costruisce l'utente corrente sopra l'accessor del contesto HTTP.</summary>
    /// <param name="httpContextAccessor">Accessor del contesto della richiesta in corso.</param>
    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public Guid Id
    {
        get
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            var claim = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!IsAuthenticated || claim is null || !Guid.TryParse(claim, out var id))
            {
                throw new InvalidOperationException(
                    "Nessun utente autenticato in questa richiesta: ICurrentUser.Id e' stato letto " +
                    "prima che il middleware di autenticazione popolasse un'identita' valida, o da un " +
                    "endpoint anonimo che non doveva chiamarlo (R6, fail-closed).");
            }

            return id;
        }
    }
}
