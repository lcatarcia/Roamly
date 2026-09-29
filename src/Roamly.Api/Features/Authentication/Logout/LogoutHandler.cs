using Microsoft.AspNetCore.Identity;
using Roamly.Infrastructure.Identity;

namespace Roamly.Api.Features.Authentication.Logout;

/// <summary>Logout: invalida il cookie di sessione. Idempotente — funziona anche se gia' disconnesso.</summary>
internal sealed class LogoutHandler
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    /// <summary>Costruisce l'handler con <see cref="SignInManager{TUser}"/> iniettato.</summary>
    /// <param name="signInManager">Gestore del cookie di sessione.</param>
    public LogoutHandler(SignInManager<ApplicationUser> signInManager)
    {
        ArgumentNullException.ThrowIfNull(signInManager);
        _signInManager = signInManager;
    }

    /// <summary>Esegue il logout.</summary>
    public Task HandleAsync() => _signInManager.SignOutAsync();
}
