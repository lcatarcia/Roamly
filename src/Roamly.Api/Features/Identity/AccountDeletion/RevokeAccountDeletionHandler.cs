using Roamly.Api.Common.Http;
using Roamly.Api.Common.Ownership;
using Roamly.Common;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Identity.AccountDeletion;

/// <summary>Revoca la richiesta solo se è ancora attiva; l'UPDATE condizionale serializza il lock account.</summary>
[ProtocolOutcomeHandler("La revoca restituisce sempre 204, sia con richiesta attiva sia senza richiesta.")]
internal sealed class RevokeAccountDeletionHandler
{
    private readonly RoamlyDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public RevokeAccountDeletionHandler(RoamlyDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task HandleAsync(CancellationToken cancellationToken)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        await AccountDeletionStateOperations.TryRevokeAsync(
            _db,
            _currentUser.Id,
            nowUtc,
            cancellationToken)
            .ConfigureAwait(false);
    }
}
