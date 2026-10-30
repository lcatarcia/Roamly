using Microsoft.AspNetCore.Identity;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;
using Roamly.Common;
using Roamly.Infrastructure.Identity;

namespace Roamly.Api.Features.Authentication.ChangePassword;

[ProtocolOutcomeHandler("La modifica password distingue riautenticazione, policy password e aggiornamento fallito.")]
internal sealed class ChangePasswordHandler
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;
    private readonly IPerAccountRateLimiter _rateLimiter;

    public ChangePasswordHandler(
        UserManager<ApplicationUser> userManager,
        ICurrentUser currentUser,
        IPerAccountRateLimiter rateLimiter)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(rateLimiter);

        _userManager = userManager;
        _currentUser = currentUser;
        _rateLimiter = rateLimiter;
    }

    public async Task<ChangePasswordResult> HandleAsync(ChangePasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_rateLimiter.TryAcquire($"password-change:{_currentUser.Id:D}"))
        {
            return new ChangePasswordResult(ChangePasswordOutcome.TooManyAttempts, []);
        }

        var user = await _userManager.FindByIdAsync(_currentUser.Id.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return new ChangePasswordResult(ChangePasswordOutcome.AccountNotFound, []);
        }

        if (!await _userManager.CheckPasswordAsync(user, request.CurrentPassword).ConfigureAwait(false))
        {
            return new ChangePasswordResult(ChangePasswordOutcome.InvalidCurrentPassword, []);
        }

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            return new ChangePasswordResult(
                ChangePasswordOutcome.PasswordRejected,
                ["La nuova password deve essere diversa da quella attuale."]);
        }

        var errors = await ValidateNewPasswordAsync(user, request.NewPassword).ConfigureAwait(false);
        if (errors.Count > 0)
        {
            return new ChangePasswordResult(ChangePasswordOutcome.PasswordRejected, errors);
        }

        return await ChangePasswordAsync(user, request).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<string>> ValidateNewPasswordAsync(
        ApplicationUser user,
        string newPassword)
    {
        var errors = new List<string>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_userManager, user, newPassword).ConfigureAwait(false);
            errors.AddRange(result.Errors.Select(error => error.Description));
        }

        return errors;
    }

    private async Task<ChangePasswordResult> ChangePasswordAsync(
        ApplicationUser user,
        ChangePasswordRequest request)
    {
        var requiresPasswordChange = user.RequiresPasswordChange;
        user.RequiresPasswordChange = false;
        var result = await _userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword).ConfigureAwait(false);

        if (result.Succeeded)
        {
            return new ChangePasswordResult(ChangePasswordOutcome.Success, []);
        }

        user.RequiresPasswordChange = requiresPasswordChange;
        return result.Errors.Any(error => error.Code == "PasswordMismatch")
            ? new ChangePasswordResult(ChangePasswordOutcome.InvalidCurrentPassword, [])
            : new ChangePasswordResult(ChangePasswordOutcome.UpdateFailed, []);
    }
}

internal enum ChangePasswordOutcome
{
    Success,
    InvalidCurrentPassword,
    PasswordRejected,
    AccountNotFound,
    TooManyAttempts,
    UpdateFailed,
}

internal sealed record ChangePasswordResult(ChangePasswordOutcome Outcome, IReadOnlyList<string> Errors);
