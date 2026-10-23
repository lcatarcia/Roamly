using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Common;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Identity.AccountDeletion;

/// <summary>Espone solo lo stato e le date della richiesta di cancellazione dell'account corrente.</summary>
[ProtocolOutcomeHandler("Una richiesta assente è rappresentata da 204 senza contenuto.")]
internal sealed class GetAccountDeletionStatusHandler
{
    private readonly RoamlyDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetAccountDeletionStatusHandler(RoamlyDbContext db, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(currentUser);
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AccountDeletionStatusResponse?> HandleAsync(CancellationToken cancellationToken)
    {
        var state = await _db.Users
            .AsNoTracking()
            .Where(user => user.Id == _currentUser.Id)
            .Select(user => new
            {
                user.DeletionRequestedAtUtc,
                user.DeletionScheduledForUtc,
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return state?.DeletionRequestedAtUtc is { } requestedAtUtc
            && state.DeletionScheduledForUtc is { } scheduledForUtc
                ? new AccountDeletionStatusResponse("scheduled", requestedAtUtc, scheduledForUtc)
                : null;
    }
}

/// <summary>Stato della cancellazione privo di dati e conteggi owned.</summary>
/// <param name="Status">Stato della richiesta.</param>
/// <param name="DeletionRequestedAtUtc">Istante della richiesta.</param>
/// <param name="DeletionScheduledForUtc">Scadenza persistita della grazia.</param>
public sealed record AccountDeletionStatusResponse(
    string Status,
    DateTime DeletionRequestedAtUtc,
    DateTime DeletionScheduledForUtc);
