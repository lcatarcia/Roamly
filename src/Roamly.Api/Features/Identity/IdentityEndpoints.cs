using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;
using Roamly.Api.Features.Identity.GetMe;

namespace Roamly.Api.Features.Identity;

/// <summary>Un solo <c>Map*Endpoints</c> per modulo (R54): registra gli endpoint di <c>Features/Identity/</c>.</summary>
public static class IdentityEndpoints
{
    private const string NotFoundProblemType = "https://roamly.dev/problems/not-found";

    /// <summary>Registra <c>GET /api/v1/me</c>.</summary>
    /// <param name="app">Route builder dell'applicazione.</param>
    /// <returns>Il route builder, per concatenare le altre registrazioni.</returns>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/me", async (GetMeHandler handler, HttpContext context) =>
        {
            var response = await handler.HandleAsync().ConfigureAwait(false);

            return response is null
                ? Results.Problem(ProblemFactory.Create(
                    context,
                    StatusCodes.Status404NotFound,
                    "Risorsa non trovata.",
                    NotFoundProblemType))
                : Results.Ok(response);
        })
        .RequireAuthorization()
        .WithRateLimitPolicy("None");

        return app;
    }
}
