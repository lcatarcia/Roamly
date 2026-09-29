using Microsoft.AspNetCore.Identity;
using Roamly.Common;
using Roamly.Infrastructure.Identity;

namespace Roamly.Api.Features.Identity.GetMe;

/// <summary>Legge il profilo dell'utente autenticato. Endpoint protetto: <see cref="ICurrentUser.Id"/> non lancia mai qui.</summary>
internal sealed class GetMeHandler
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;

    /// <summary>Costruisce l'handler con <see cref="UserManager{TUser}"/> e <see cref="ICurrentUser"/> iniettati.</summary>
    /// <param name="userManager">Gestore utenti Identity.</param>
    /// <param name="currentUser">Utente corrente della richiesta.</param>
    public GetMeHandler(UserManager<ApplicationUser> userManager, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(currentUser);

        _userManager = userManager;
        _currentUser = currentUser;
    }

    /// <summary>Restituisce il profilo dell'utente corrente, o <see langword="null"/> se e' stato cancellato tra l'autenticazione del cookie e questa lettura.</summary>
    public async Task<GetMeResponse?> HandleAsync()
    {
        var user = await _userManager.FindByIdAsync(_currentUser.Id.ToString()).ConfigureAwait(false);

        return user is null
            ? null
            : new GetMeResponse(user.Id, user.Email ?? string.Empty, user.DisplayName, user.ReportingCurrency);
    }
}
