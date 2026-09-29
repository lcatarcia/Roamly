using Microsoft.AspNetCore.Identity;
using Roamly.Api.Common.Security;
using Roamly.Infrastructure.Identity;

namespace Roamly.Api.Features.Authentication.Login;

/// <summary>Login: valida le credenziali via <see cref="SignInManager{TUser}"/> ed emette il cookie di sessione.</summary>
internal sealed class LoginHandler
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPerAccountRateLimiter _rateLimiter;

    /// <summary>Costruisce l'handler con <see cref="SignInManager{TUser}"/>, <see cref="UserManager{TUser}"/> e rate limiter iniettati.</summary>
    /// <param name="signInManager">Gestore del cookie di sessione.</param>
    /// <param name="userManager">Gestore utenti, per costruire il profilo di risposta.</param>
    /// <param name="rateLimiter">Rate limiter per-account (SECURITY.md §6).</param>
    public LoginHandler(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, IPerAccountRateLimiter rateLimiter)
    {
        ArgumentNullException.ThrowIfNull(signInManager);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(rateLimiter);

        _signInManager = signInManager;
        _userManager = userManager;
        _rateLimiter = rateLimiter;
    }

    /// <summary>Esegue il login.</summary>
    /// <param name="request">Corpo della richiesta.</param>
    public async Task<LoginResult> HandleAsync(LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedEmail = request.Email.Trim();

        if (!_rateLimiter.TryAcquire(normalizedEmail))
        {
            return new LoginResult(LoginOutcome.TooManyAttempts, null);
        }

        var signInResult = await _signInManager
            .PasswordSignInAsync(normalizedEmail, request.Password, isPersistent: false, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (!signInResult.Succeeded)
        {
            return new LoginResult(LoginOutcome.InvalidCredentials, null);
        }

        var user = await _userManager.FindByNameAsync(normalizedEmail).ConfigureAwait(false);

        if (user is null)
        {
            // Non dovrebbe accadere (il sign-in e' appena riuscito su questo username), ma senza
            // l'utente non c'e' profilo da restituire: trattato come le altre credenziali non valide.
            return new LoginResult(LoginOutcome.InvalidCredentials, null);
        }

        return new LoginResult(LoginOutcome.Success, new LoginProfile(user.Id, user.Email ?? normalizedEmail, user.DisplayName));
    }
}
