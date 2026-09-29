using Microsoft.AspNetCore.Identity;
using Roamly.Common.Email;
using Roamly.Infrastructure.Identity;

namespace Roamly.Api.Features.Authentication.Register;

/// <summary>
/// Registrazione: crea l'utente Identity e invia il token di conferma via <see cref="IEmailSender"/>.
/// Non owned (R3 non si applica: <see cref="ApplicationUser"/> non implementa <c>IOwnedResource</c>).
/// </summary>
internal sealed class RegisterHandler
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly TimeProvider _clock;

    /// <summary>Costruisce l'handler con <see cref="UserManager{TUser}"/>, mittente email e orologio iniettati.</summary>
    /// <param name="userManager">Gestore utenti Identity.</param>
    /// <param name="emailSender">Mittente su cui recapitare il token di conferma.</param>
    /// <param name="clock">Orologio iniettato (R34).</param>
    public RegisterHandler(UserManager<ApplicationUser> userManager, IEmailSender emailSender, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(emailSender);
        ArgumentNullException.ThrowIfNull(clock);

        _userManager = userManager;
        _emailSender = emailSender;
        _clock = clock;
    }

    /// <summary>Esegue la registrazione, restituendo un esito che l'endpoint traduce in risposta HTTP.</summary>
    /// <param name="request">Corpo della richiesta.</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    public async Task<RegisterResult> HandleAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedEmail = request.Email.Trim();
        var candidate = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            DisplayName = request.DisplayName,
            CreatedAtUtc = _clock.GetUtcNow().UtcDateTime,
        };

        // La policy password e' valutata su un candidato mai persistito, *prima* di cercare
        // l'utente esistente: cosi' un errore di formato password non rivela mai se l'account
        // esiste gia'. E' l'unico controllo che l'endpoint puo' mostrare in modo puntuale senza
        // riaprire l'oracolo su "email gia' registrata".
        var passwordErrors = new List<string>();

        foreach (var validator in _userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(_userManager, candidate, request.Password).ConfigureAwait(false);

            if (!validation.Succeeded)
            {
                passwordErrors.AddRange(validation.Errors.Select(error => error.Description));
            }
        }

        if (passwordErrors.Count > 0)
        {
            return new RegisterResult(RegisterOutcome.PasswordRejected, passwordErrors);
        }

        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail).ConfigureAwait(false);

        if (existingUser is not null)
        {
            return new RegisterResult(RegisterOutcome.Accepted, []);
        }

        var createResult = await _userManager.CreateAsync(candidate, request.Password).ConfigureAwait(false);

        if (!createResult.Succeeded)
        {
            // La password e' gia' stata validata sopra: se CreateAsync fallisce qui, e' per un
            // motivo diverso (es. race su username duplicato). Resta mascherato per lo stesso
            // principio: il chiamante non deve poter distinguere questo caso da "email gia' in uso".
            return new RegisterResult(RegisterOutcome.Accepted, []);
        }

        var confirmationToken = await _userManager
            .GenerateEmailConfirmationTokenAsync(candidate)
            .ConfigureAwait(false);

        await _emailSender
            .SendAsync(
                normalizedEmail,
                "Conferma il tuo account Roamly",
                $"UserId: {candidate.Id}\nToken: {confirmationToken}",
                cancellationToken)
            .ConfigureAwait(false);

        return new RegisterResult(RegisterOutcome.Accepted, []);
    }
}
