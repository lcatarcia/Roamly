using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Ownership;
using Roamly.Common;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Identity.AccountDeletion;

/// <summary>Calcola i conteggi owned informativi per la schermata di conferma.</summary>
[ProtocolOutcomeHandler("La risposta HTTP di successo contiene una fotografia informativa dei conteggi.")]
internal sealed class GetAccountDeletionPreviewHandler
{
    private readonly RoamlyDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly OwnedResourceWalker _walker;
    private readonly TimeProvider _clock;

    public GetAccountDeletionPreviewHandler(
        RoamlyDbContext db,
        ICurrentUser currentUser,
        OwnedResourceWalker walker,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(walker);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _currentUser = currentUser;
        _walker = walker;
        _clock = clock;
    }

    public async Task<AccountDeletionPreviewResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var entityCounts = await _walker.CountAsync(_db, _currentUser.Id, cancellationToken).ConfigureAwait(false);
        var totalCount = checked(entityCounts.Sum(item => item.Count));
        return new AccountDeletionPreviewResponse(_clock.GetUtcNow().UtcDateTime, entityCounts, totalCount);
    }
}

/// <summary>Fotografia informativa dei record owned al momento della richiesta.</summary>
/// <param name="GeneratedAtUtc">Istante server di generazione dei conteggi.</param>
/// <param name="EntityCounts">Conteggi per tipo owned presente nel modello runtime.</param>
/// <param name="TotalCount">Somma dei conteggi, non una promessa per il job futuro.</param>
public sealed record AccountDeletionPreviewResponse(
    DateTime GeneratedAtUtc,
    IReadOnlyList<OwnedResourceCount> EntityCounts,
    long TotalCount);
