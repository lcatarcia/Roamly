namespace Roamly.Api.Common.Http;

/// <summary>Politica dichiarata da ogni endpoint POST (R50, ADR-0005).</summary>
public enum IdempotencyMode
{
    /// <summary>L'identita' client-side determina il replay.</summary>
    IdentityBased,

    /// <summary>L'operazione non richiede deduplicazione.</summary>
    NotRequired,

    /// <summary>La richiesta richiede una chiave esplicita.</summary>
    KeyRequired,
}

/// <summary>Metadata OpenAPI/runtime della politica di idempotenza di un POST.</summary>
/// <param name="Mode">Politica scelta per l'endpoint.</param>
public sealed record IdempotencyPolicy(IdempotencyMode Mode);
