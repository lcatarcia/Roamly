using Microsoft.Extensions.Options;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;

namespace Roamly.Api.Features.Maps;

public static class MapsEndpoints
{
    public static IEndpointRouteBuilder MapMapsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/maps/config", (IOptions<MapsRenderOptions> options, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var configured = options.Value.IsConfigured;
            return Results.Ok(new MapsConfigResponse(configured,
                configured ? options.Value.AccountClientId : null, "eu.atlas.microsoft.com"));
        })
        .RequireAuthorization().WithRateLimitPolicy("None")
        .WithName("GetMapsConfig").WithTags("Maps")
        .WithSummary("Configurazione pubblica del rendering Maps; nessuna chiave")
        .Produces<MapsConfigResponse>()
        .ProducesProblem(401).ProducesProblem(403);

        app.MapPost("/api/v1/maps/token", IssueAsync)
        .RequireAuthorization().WithRateLimitPolicy("PerAccount")
        .WithMetadata(new IdempotencyPolicy(IdempotencyMode.NotRequired))
        .WithName("IssueMapsToken").WithTags("Maps")
        .WithSummary("Rendering Maps personale, dopo verifica account e sospensione economica")
        .Produces<MapsTokenResponse>()
        .ProducesProblem(401).ProducesProblem(403).ProducesProblem(429).ProducesProblem(503);
        return app;
    }

    private static async Task<IResult> IssueAsync(
        IssueMapsTokenHandler handler, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var result = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
        if (result.Token is not null) return Results.Ok(result.Token);
        var status = result.Error is "maps-not-configured" or "maps-identity-unavailable" ? 503 : 403;
        return Results.Problem(ProblemFactory.Create(context, status,
            "Mappa non disponibile. La lista rimane utilizzabile.",
            "https://roamly.dev/problems/" + result.Error));
    }
}

public sealed record MapsConfigResponse(bool IsConfigured, string? AccountClientId, string Domain);
