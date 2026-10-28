using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Identity;
using Roamly.Api.Common.Security;
using Roamly.Common.Security;
using Roamly.Infrastructure.Identity;

namespace Roamly.Api.Features.Authentication.Login;

/// <summary>Login: valida le credenziali via <see cref="SignInManager{TUser}"/> ed emette il cookie di sessione.</summary>
[ProtocolOutcomeHandler("Successo, credenziali invalide e rate limit hanno status HTTP distinti definiti dal contratto di autenticazione.")]
internal sealed partial class LoginHandler
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly DummyLoginPasswordHash _dummyPasswordHash;
    private readonly ILoginAttemptRateLimiter _rateLimiter;
    private readonly TimeProvider _clock;
    private readonly ILogger<LoginHandler> _logger;

    /// <summary>Costruisce l'handler con i servizi Identity e di rate limiting iniettati.</summary>
    /// <param name="signInManager">Gestore del cookie di sessione.</param>
    /// <param name="userManager">Gestore utenti, per costruire il profilo di risposta.</param>
    /// <param name="dummyPasswordHash">Verificatore con hash fittizio condiviso per utenti inesistenti.</param>
    /// <param name="rateLimiter">Rate limiter per-account (SECURITY.md §6).</param>
    /// <param name="clock">Orologio applicativo usato per verificare il periodo di grazia.</param>
    /// <param name="logger">Logger senza identificatori o credenziali.</param>
    public LoginHandler(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        DummyLoginPasswordHash dummyPasswordHash,
        ILoginAttemptRateLimiter rateLimiter,
        TimeProvider clock,
        ILogger<LoginHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(signInManager);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(dummyPasswordHash);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _signInManager = signInManager;
        _userManager = userManager;
        _dummyPasswordHash = dummyPasswordHash;
        _rateLimiter = rateLimiter;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Esegue il login.</summary>
    /// <param name="request">Corpo della richiesta.</param>
    /// <param name="clientIpAddress">Indirizzo client verificato dalla pipeline HTTP.</param>
    /// <param name="cancellationToken">Token di annullamento della richiesta.</param>
    public async Task<LoginResult> HandleAsync(
        LoginRequest request,
        string? clientIpAddress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(clientIpAddress))
        {
            LogMissingClientIp(_logger);
            return new LoginResult(LoginOutcome.Unavailable, null);
        }

        var ipStatus = await _rateLimiter.CheckIpAsync(
            clientIpAddress,
            cancellationToken).ConfigureAwait(false);
        if (ipStatus == LoginAttemptLimitStatus.IpTooManyAttempts)
        {
            return new LoginResult(LoginOutcome.TooManyAttempts, null);
        }

        if (ipStatus == LoginAttemptLimitStatus.Unavailable)
        {
            return new LoginResult(LoginOutcome.Unavailable, null);
        }

        var normalizedEmail = request.Email.Trim();
        var user = await _userManager.FindByNameAsync(normalizedEmail).ConfigureAwait(false);
        if (user is null)
        {
            var unknownStart = await _rateLimiter.BeginUnknownAccountAsync(cancellationToken).ConfigureAwait(false);
            _dummyPasswordHash.Verify(_userManager.PasswordHasher, request.Password);

            if (unknownStart.Status == LoginAttemptLimitStatus.AccountTooManyAttempts)
            {
                return new LoginResult(LoginOutcome.InvalidCredentials, null);
            }

            if (unknownStart.Status == LoginAttemptLimitStatus.Unavailable
                || unknownStart.Reservation is null)
            {
                return new LoginResult(LoginOutcome.Unavailable, null);
            }

            await using var unknownReservation = unknownStart.Reservation;
            return await CompleteAttemptAsync(unknownReservation, credentialsInvalid: true, cancellationToken)
                .ConfigureAwait(false);
        }

        var start = await _rateLimiter.BeginAccountAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (start.Status == LoginAttemptLimitStatus.AccountTooManyAttempts)
        {
            _dummyPasswordHash.Verify(_userManager.PasswordHasher, request.Password);
            return new LoginResult(LoginOutcome.InvalidCredentials, null);
        }

        if (start.Status == LoginAttemptLimitStatus.Unavailable)
        {
            return new LoginResult(LoginOutcome.Unavailable, null);
        }

        if (start.Reservation is null)
        {
            return new LoginResult(LoginOutcome.Unavailable, null);
        }

        await using var reservation = start.Reservation;
        var passwordCheck = await _signInManager
            .CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (!passwordCheck.Succeeded)
        {
            return await CompleteAttemptAsync(reservation, credentialsInvalid: true, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!await CanAuthenticateWithoutConfirmedEmailAsync(user).ConfigureAwait(false))
        {
            return await CompleteAttemptAsync(reservation, credentialsInvalid: false, cancellationToken)
                .ConfigureAwait(false);
        }

        var limitedSession = user.DeletionRequestedAtUtc is not null;
        if (limitedSession && !IsWithinGracePeriod(user))
        {
            return await CompleteAttemptAsync(reservation, credentialsInvalid: false, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!await reservation.CompleteAsync(credentialsInvalid: false, cancellationToken).ConfigureAwait(false))
        {
            return new LoginResult(LoginOutcome.Unavailable, null);
        }

        var claims = new List<Claim> { PasswordAuthenticationClaims.Create(_clock) };
        if (limitedSession)
        {
            claims.Add(new Claim(AccountSessionClaims.ModeClaimType, AccountSessionClaims.DeletionLimited));
        }
        await _signInManager.SignInWithClaimsAsync(
            user,
            isPersistent: false,
            claims)
            .ConfigureAwait(false);

        return new LoginResult(LoginOutcome.Success, new LoginProfile(
            user.Id,
            user.Email ?? normalizedEmail,
            user.DisplayName,
            limitedSession ? "limited" : null));
    }

    private static async Task<LoginResult> CompleteAttemptAsync(
        ILoginAttemptReservation reservation,
        bool credentialsInvalid,
        CancellationToken cancellationToken)
    {
        var completed = await reservation
            .CompleteAsync(credentialsInvalid, cancellationToken)
            .ConfigureAwait(false);

        return completed
            ? new LoginResult(LoginOutcome.InvalidCredentials, null)
            : new LoginResult(LoginOutcome.Unavailable, null);
    }

    private bool IsWithinGracePeriod(ApplicationUser user) =>
        user.DeletionScheduledForUtc is { } scheduledForUtc
        && scheduledForUtc > _clock.GetUtcNow().UtcDateTime;

    private async Task<bool> CanAuthenticateWithoutConfirmedEmailAsync(ApplicationUser user)
    {
        if (user.EmailConfirmed)
        {
            return true;
        }

        var claims = await _userManager.GetClaimsAsync(user).ConfigureAwait(false);
        return claims.Any(claim => claim.Type == ProvisionedAccountClaims.UnconfirmedLogin
            && claim.Value == "true");
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Login rejected because no trusted client IP was available.")]
    private static partial void LogMissingClientIp(ILogger logger);
}
