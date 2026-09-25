using Microsoft.AspNetCore.Identity;

namespace Roamly.Infrastructure.Identity;

/// <summary>
/// Utente applicativo: radice di ownership e principal di ogni FK <c>OwnerId</c> (CONTEXT.md §2.2).
/// La PK <see cref="Guid"/> di Identity fissa il tipo di <c>OwnerId</c> ovunque.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Nome visualizzato.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Valuta preferita per la presentazione. <b>Non converte nulla</b>: Roamly persiste ogni
    /// importo nella valuta in cui e' stato speso (CONTEXT.md §2.1).
    /// </summary>
    public string ReportingCurrency { get; set; } = "EUR";

    /// <summary>Istante di creazione dell'account, UTC.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Quando l'utente ha chiesto la cancellazione. <c>null</c> = nessuna richiesta attiva.</summary>
    public DateTime? DeletionRequestedAtUtc { get; set; }

    /// <summary>
    /// Scadenza della grazia, <b>persistita e non calcolata</b>: se la finestra cambiasse, le
    /// richieste in corso devono mantenere la scadenza promessa (ADR-0004).
    /// </summary>
    public DateTime? DeletionScheduledForUtc { get; set; }

    /// <summary>Versione dell'informativa mostrata. Non e' un consenso.</summary>
    public string? PrivacyPolicyVersionSeen { get; set; }
}
