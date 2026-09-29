using System.ComponentModel.DataAnnotations;

namespace Roamly.Api.Features.Authentication.ConfirmEmail;

/// <summary>Corpo di <c>POST /api/v1/auth/email/confirm</c>.</summary>
/// <param name="UserId">Identificativo utente, dal link ricevuto via email.</param>
/// <param name="Token">Token di conferma generato da Identity.</param>
public sealed record ConfirmEmailRequest(
    [property: Required] Guid UserId,
    [property: Required] string Token);
