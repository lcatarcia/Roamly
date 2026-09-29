using Roamly.Common.Email;

namespace Roamly.Api.Common.Email;

/// <summary>
/// Fake di sviluppo di <see cref="IEmailSender"/>: scrive sul log strutturato invece di spedire.
/// Il token di conferma email arriva quindi solo nel log, mai in una risposta HTTP — e' cosi' che
/// i test end-to-end possono completare il percorso di <c>register</c> senza un provider SMTP.
/// </summary>
public sealed partial class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    /// <summary>Costruisce il fake con il logger su cui scrivere le email non spedite.</summary>
    /// <param name="logger">Logger di destinazione.</param>
    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        LogEmail(recipient, subject, body);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "email_dev_send: a {To}, oggetto '{Subject}' — {Body}")]
    private partial void LogEmail(string to, string subject, string body);
}
