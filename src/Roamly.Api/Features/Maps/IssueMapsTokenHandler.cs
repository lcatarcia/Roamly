using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Ownership;
using Roamly.Api.Features.Identity.Consents;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Maps;

/// <summary>Recheck locale prima/dopo Entra; nessuna rete mentre la radice account e' bloccata.</summary>
[ProtocolOutcomeHandler("Configurazione/Entra non disponibili 503; account/consenso cambiati o sospensione 403.")]
public sealed class IssueMapsTokenHandler(
    RoamlyDbContext db, GetConsentHandler consents, ConsentPurposeDefinition definition,
    IOptions<MapsRenderOptions> options, [FromKeyedServices("MapsRendering")] TokenCredential credential,
    TimeProvider clock)
{
    public async Task<MapsTokenResult> HandleAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured) return new("maps-not-configured", null);
        var initial = await CheckAsync(null, cancellationToken).ConfigureAwait(false);
        if (initial.Error is not null) return new(initial.Error, null);
        AccessToken token;
        try
        {
            token = await credential.GetTokenAsync(
                new TokenRequestContext(["https://atlas.microsoft.com/.default"]), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is AuthenticationFailedException or CredentialUnavailableException)
        {
            return new("maps-identity-unavailable", null);
        }

        var final = await CheckAsync(initial.Revision, cancellationToken).ConfigureAwait(false);
        if (final.Error is not null) return new(final.Error, null);
        cancellationToken.ThrowIfCancellationRequested();
        return token.ExpiresOn <= clock.GetUtcNow().AddSeconds(30)
            ? new("maps-identity-unavailable", null)
            : new(null, new MapsTokenResponse(token.Token, token.ExpiresOn));
    }

    private async Task<(string? Error, long Revision)> CheckAsync(long? revision, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var account = await AccountDeletionStateOperations.LockAsync(db, db.CurrentUserId, ct).ConfigureAwait(false);
        if (account is null || account.RequiresPasswordChange || account.DeletionRequestedAtUtc is not null)
            return ("maps-account-restricted", 0);
        var consent = await consents.HandleAsync(definition.Purpose, ct).ConfigureAwait(false);
        if (consent?.IsGranted != true || revision.HasValue && consent.Revision != revision)
            return ("maps-consent-required", 0);
        // Personal mode does not invoke the inactive evidence gate. A persisted latch still wins.
        if (await db.MapsEconomicStates.AsNoTracking().AnyAsync(
            state => state.SuspendedAtUtc != null || state.SuspensionReason != null, ct).ConfigureAwait(false))
            return ("maps-economically-suspended", 0);
        return (null, consent.Revision);
    }
}

public sealed record MapsTokenResult(string? Error, MapsTokenResponse? Token);
public sealed record MapsTokenResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
