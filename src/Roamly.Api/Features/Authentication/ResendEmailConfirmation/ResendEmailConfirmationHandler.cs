using Microsoft.AspNetCore.Identity;
using Roamly.Api.Common.Http;
using Roamly.Common.Email;
using Roamly.Infrastructure.Identity;

namespace Roamly.Api.Features.Authentication.ResendEmailConfirmation;

/// <summary>Rigenera e invia il token solo per un account esistente non ancora confermato.</summary>
[ProtocolOutcomeHandler("La richiesta di reinvio restituisce sempre una risposta generica.")]
internal sealed class ResendEmailConfirmationHandler
{
    private const string Subject = "Conferma il tuo account Roamly";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;

    /// <summary>Costruisce l'handler con Identity e il mittente email iniettati.</summary>
    public ResendEmailConfirmationHandler(
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(emailSender);

        _userManager = userManager;
        _emailSender = emailSender;
    }

    /// <summary>Invia un nuovo token se l'indirizzo appartiene a un account non confermato.</summary>
    public async Task HandleAsync(
        ResendEmailConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);

        if (user is null || user.EmailConfirmed)
        {
            return;
        }

        await SendConfirmationAsync(user, email, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendConfirmationAsync(
        ApplicationUser user,
        string email,
        CancellationToken cancellationToken)
    {
        var token = await _userManager
            .GenerateEmailConfirmationTokenAsync(user)
            .ConfigureAwait(false);

        await _emailSender.SendAsync(
            user.Email ?? email,
            Subject,
            $"UserId: {user.Id}\nToken: {token}",
            cancellationToken).ConfigureAwait(false);
    }
}
