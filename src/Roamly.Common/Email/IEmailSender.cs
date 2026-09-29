namespace Roamly.Common.Email;

/// <summary>
/// Invio email, astratto dal provider. Il provider reale e' fuori scope (dipendenza SMTP non
/// indagata, <c>OPEN-DECISIONS.md</c>): oggi l'unica implementazione registrata e' un fake che
/// scrive sul log strutturato (<c>Roamly.Api.Common.Email.LoggingEmailSender</c>), usato in ogni
/// ambiente finche' non arriva un provider vero.
/// </summary>
public interface IEmailSender
{
    /// <summary>Invia un'email. Il fake di sviluppo la scrive sul log invece di spedirla davvero.</summary>
    /// <param name="recipient">Destinatario.</param>
    /// <param name="subject">Oggetto.</param>
    /// <param name="body">Corpo, testo semplice: nessun template HTML finche' non esiste un provider reale.</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken);
}
