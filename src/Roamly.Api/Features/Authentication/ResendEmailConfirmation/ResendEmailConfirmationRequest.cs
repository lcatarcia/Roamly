using System.ComponentModel.DataAnnotations;

namespace Roamly.Api.Features.Authentication.ResendEmailConfirmation;

/// <summary>Richiesta di reinvio della conferma email locale.</summary>
/// <param name="Email">Indirizzo email dell'account.</param>
public sealed record ResendEmailConfirmationRequest(
    [property: Required, EmailAddress] string Email);
