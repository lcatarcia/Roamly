using Microsoft.AspNetCore.Identity;
using Roamly.Infrastructure.Identity;

namespace Roamly.Api.Features.Authentication.ConfirmEmail;

/// <summary>Esito di <see cref="ConfirmEmailHandler.HandleAsync"/>: qui un oracolo e' inevitabile e accettato — chi possiede <c>UserId</c>+<c>Token</c> ha gia' ricevuto l'email, quindi sapere se il link e' valido non aggiunge informazione.</summary>
internal enum ConfirmEmailOutcome
{
    /// <summary>Conferma riuscita.</summary>
    Confirmed,

    /// <summary>Utente inesistente o token non valido/scaduto: le due cause sono unificate per non distinguere un id casuale da uno reale con token sbagliato.</summary>
    InvalidLink,
}

/// <summary>Conferma l'email a partire dal token generato in fase di registrazione.</summary>
internal sealed class ConfirmEmailHandler
{
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>Costruisce l'handler con <see cref="UserManager{TUser}"/> iniettato.</summary>
    /// <param name="userManager">Gestore utenti Identity.</param>
    public ConfirmEmailHandler(UserManager<ApplicationUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        _userManager = userManager;
    }

    /// <summary>Esegue la conferma.</summary>
    /// <param name="request">Corpo della richiesta.</param>
    public async Task<ConfirmEmailOutcome> HandleAsync(ConfirmEmailRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false);

        if (user is null)
        {
            return ConfirmEmailOutcome.InvalidLink;
        }

        var result = await _userManager.ConfirmEmailAsync(user, request.Token).ConfigureAwait(false);

        return result.Succeeded ? ConfirmEmailOutcome.Confirmed : ConfirmEmailOutcome.InvalidLink;
    }
}
