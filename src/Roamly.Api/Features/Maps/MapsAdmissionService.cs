using Roamly.Api.Common.Ownership;
using Roamly.Api.Features.Identity.Consents;
using Roamly.Infrastructure.Maps;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Maps;

/// <summary>
/// Local opening check for a future consumer, not a credential authorization or reservation.
/// A future issuer must atomically recheck account, consent revision and economic suspension.
/// </summary>
public sealed partial class MapsAdmissionService(
    RoamlyDbContext db, ConsentPurposeDefinition definition, GetConsentHandler consents,
    MapsEconomicGate economicGate, ILogger<MapsAdmissionService> logger)
{
    public async Task<MapsAdmissionReason> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var identity = await CheckIdentityAsync(cancellationToken).ConfigureAwait(false);
            if (identity != MapsAdmissionReason.Admitted) return Deny(identity);
            var economic = await economicGate.CheckAsync(cancellationToken).ConfigureAwait(false);
            if (economic != MapsAdmissionReason.Admitted) return economic;
            var final = await CheckIdentityAsync(cancellationToken).ConfigureAwait(false);
            return final != MapsAdmissionReason.Admitted ? Deny(final) : final;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Deny(MapsAdmissionReason.StorageUnavailable); }
    }

    private async Task<MapsAdmissionReason> CheckIdentityAsync(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var account = await AccountDeletionStateOperations.LockAsync(db, db.CurrentUserId, ct).ConfigureAwait(false);
        if (account is null || account.RequiresPasswordChange || account.DeletionRequestedAtUtc is not null)
            return MapsAdmissionReason.AccountRestricted;
        var consent = await consents.HandleAsync(definition.Purpose, ct).ConfigureAwait(false);
        return consent?.IsGranted == true ? MapsAdmissionReason.Admitted : MapsAdmissionReason.ConsentRequired;
    }

    private MapsAdmissionReason Deny(MapsAdmissionReason reason)
    {
        LogDenied(logger, reason);
        return reason;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Maps local identity admission denied: {Reason}.")]
    private static partial void LogDenied(ILogger logger, MapsAdmissionReason reason);
}
