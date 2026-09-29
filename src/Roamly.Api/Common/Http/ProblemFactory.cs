using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Roamly.Api.Common.Http;

/// <summary>
/// Unica sorgente dei <see cref="ProblemDetails"/> dell'API (RFC 9457, R45/R46,
/// <c>API-CONVENTIONS.md</c> §2). Nessun endpoint costruisce un <see cref="ProblemDetails"/> a mano
/// e nessuno eredita il default di ASP.NET Core: e' cosi' che l'insieme delle chiavi di
/// <see cref="ProblemDetails.Extensions"/> resta una lista chiusa, dichiarata qui e in nessun altro
/// punto — oggi solo <c>traceId</c>.
/// <para>
/// <b>R46 (l'oracolo che R7 vieta):</b> <see cref="ProblemDetails.Detail"/> e
/// <see cref="ProblemDetails.Instance"/> non sono mai valorizzati. Un <c>404</c> di ownership deve
/// essere byte-identico a un <c>404</c> di risorsa davvero inesistente: qualunque differenza — un
/// <c>Detail</c> presente in un caso e assente nell'altro, un <c>Instance</c> che rivela l'id
/// richiesto — sarebbe un canale laterale che permette di distinguere i due casi dall'esterno.
/// </para>
/// </summary>
public static class ProblemFactory
{
    /// <summary>Costruisce il <see cref="ProblemDetails"/> per la richiesta corrente.</summary>
    /// <param name="httpContext">Contesto della richiesta in corso, sorgente di <c>traceId</c>.</param>
    /// <param name="statusCode">Status HTTP associato.</param>
    /// <param name="title">Titolo breve e stabile del problema — mai il messaggio di un'eccezione.</param>
    /// <param name="type">URI del tipo di problema (RFC 9457 §3.1.1).</param>
    /// <returns>Il <see cref="ProblemDetails"/> pronto per la risposta.</returns>
    public static ProblemDetails Create(HttpContext httpContext, int statusCode, string title, string type)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return problem;
    }
}
