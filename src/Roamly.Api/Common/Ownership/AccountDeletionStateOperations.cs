using Microsoft.EntityFrameworkCore;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Common.Ownership;

/// <summary>
/// Scritture condizionali sulla radice Identity. Vive nell'unico perimetro autorizzato da R5 per
/// API EF che bypassano il change tracker; il predicato vincola sempre l'operazione al singolo Id.
/// </summary>
internal static class AccountDeletionStateOperations
{
    public static Task<int> TryScheduleAsync(
        RoamlyDbContext db,
        Guid userId,
        DateTime requestedAtUtc,
        DateTime scheduledForUtc,
        CancellationToken cancellationToken)
    {
        var concurrencyStamp = Guid.NewGuid().ToString();
        return db.Users
            .Where(user => user.Id == userId && user.DeletionRequestedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(user => user.DeletionRequestedAtUtc, (DateTime?)requestedAtUtc)
                    .SetProperty(user => user.DeletionScheduledForUtc, (DateTime?)scheduledForUtc)
                    .SetProperty(user => user.ConcurrencyStamp, concurrencyStamp),
                cancellationToken);
    }

    public static Task<int> TryRevokeAsync(
        RoamlyDbContext db,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var concurrencyStamp = Guid.NewGuid().ToString();
        return db.Users
            .Where(user =>
                user.Id == userId
                && user.DeletionRequestedAtUtc != null
                && user.DeletionScheduledForUtc > nowUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(user => user.DeletionRequestedAtUtc, (DateTime?)null)
                    .SetProperty(user => user.DeletionScheduledForUtc, (DateTime?)null)
                    .SetProperty(user => user.ConcurrencyStamp, concurrencyStamp),
                cancellationToken);
    }
}
