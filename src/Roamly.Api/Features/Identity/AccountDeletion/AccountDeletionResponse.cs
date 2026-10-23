namespace Roamly.Api.Features.Identity.AccountDeletion;

/// <summary>Date della richiesta di cancellazione, lette o calcolate dal server.</summary>
/// <param name="DeletionRequestedAtUtc">Istante UTC della richiesta persistita.</param>
/// <param name="DeletionScheduledForUtc">Scadenza UTC persistita del periodo di grazia.</param>
public sealed record AccountDeletionResponse(
    DateTime DeletionRequestedAtUtc,
    DateTime DeletionScheduledForUtc);
