using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Ownership;
using Roamly.Api.Common.Security;
using Roamly.Common;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Identity.AccountDeletion;

/// <summary>Registra una richiesta di cancellazione senza spostarne la scadenza in caso di replay.</summary>
[ProtocolOutcomeHandler("Password errata, rate limit e account mancante hanno esiti HTTP distinti.")]
internal sealed class RequestAccountDeletionHandler
{
    private readonly RoamlyDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;
    private readonly IPerAccountRateLimiter _rateLimiter;
    private readonly TimeProvider _clock;

    public RequestAccountDeletionHandler(
        RoamlyDbContext db,
        UserManager<ApplicationUser> userManager,
        ICurrentUser currentUser,
        IPerAccountRateLimiter rateLimiter,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(clock);

        _db = db;
        _userManager = userManager;
        _currentUser = currentUser;
        _rateLimiter = rateLimiter;
        _clock = clock;
    }

    public async Task<RequestAccountDeletionResult> HandleAsync(
        RequestAccountDeletionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_rateLimiter.TryAcquire($"account-deletion:{_currentUser.Id:D}"))
        {
            return new RequestAccountDeletionResult(RequestAccountDeletionOutcome.TooManyAttempts, null);
        }

        var user = await _userManager.FindByIdAsync(_currentUser.Id.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return new RequestAccountDeletionResult(RequestAccountDeletionOutcome.AccountNotFound, null);
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password).ConfigureAwait(false))
        {
            return new RequestAccountDeletionResult(RequestAccountDeletionOutcome.InvalidPassword, null);
        }

        return await ScheduleOrReadExistingAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<RequestAccountDeletionResult> ScheduleOrReadExistingAsync(CancellationToken cancellationToken)
    {
        var requestedAtUtc = _clock.GetUtcNow().UtcDateTime;
        var scheduledForUtc = requestedAtUtc.AddDays(30);
        var affected = await AccountDeletionStateOperations.TryScheduleAsync(
            _db,
            _currentUser.Id,
            requestedAtUtc,
            scheduledForUtc,
            cancellationToken).ConfigureAwait(false);

        if (affected == 1)
        {
            return Scheduled(requestedAtUtc, scheduledForUtc);
        }

        return await ReadExistingRequestAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<RequestAccountDeletionResult> ReadExistingRequestAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.Users
            .AsNoTracking()
            .Where(user => user.Id == _currentUser.Id)
            .Select(user => new
            {
                user.DeletionRequestedAtUtc,
                user.DeletionScheduledForUtc,
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing?.DeletionRequestedAtUtc is { } requestedAtUtc
            && existing.DeletionScheduledForUtc is { } scheduledForUtc)
        {
            return Scheduled(requestedAtUtc, scheduledForUtc);
        }

        return new RequestAccountDeletionResult(RequestAccountDeletionOutcome.StateChanged, null);
    }

    private static RequestAccountDeletionResult Scheduled(DateTime requestedAtUtc, DateTime scheduledForUtc) =>
        new(RequestAccountDeletionOutcome.Scheduled,
            new AccountDeletionResponse(requestedAtUtc, scheduledForUtc));
}

internal enum RequestAccountDeletionOutcome
{
    Scheduled,
    InvalidPassword,
    TooManyAttempts,
    AccountNotFound,
    StateChanged,
}

internal sealed record RequestAccountDeletionResult(
    RequestAccountDeletionOutcome Outcome,
    AccountDeletionResponse? Response);
