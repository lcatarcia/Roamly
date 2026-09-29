namespace Roamly.Api.Common.Security;

/// <summary>
/// Marca un endpoint con la politica di rate limiting che dichiara di seguire — <c>"None"</c>
/// incluso. Serve a rendere l'omissione un errore visibile: un futuro verificatore (R61,
/// modellato su R50 di ADR-0005 per <c>If-Match</c>/<c>NotRequired</c>) confronta ogni endpoint
/// sotto <c>Features/Authentication</c> con la presenza di questo metadata, non con un comportamento
/// runtime implicito.
/// </summary>
/// <param name="PolicyName">Nome della politica dichiarata (<c>"None"</c> e' un valore esplicito, non l'assenza del metadata).</param>
public sealed record RateLimitPolicyMetadata(string PolicyName);

/// <summary>Estensioni per dichiarare <see cref="RateLimitPolicyMetadata"/> sugli endpoint minimal API.</summary>
public static class RateLimitPolicyMetadataExtensions
{
    /// <summary>Dichiara la politica di rate limiting dell'endpoint.</summary>
    /// <param name="builder">Builder dell'endpoint.</param>
    /// <param name="policyName">Nome della politica, <c>"None"</c> compreso.</param>
    public static RouteHandlerBuilder WithRateLimitPolicy(this RouteHandlerBuilder builder, string policyName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        return builder.WithMetadata(new RateLimitPolicyMetadata(policyName));
    }
}
