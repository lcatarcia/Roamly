using System.ComponentModel.DataAnnotations;

namespace Roamly.Api.Features.Authentication.Register;

/// <summary>Corpo di <c>POST /api/v1/auth/register</c>. Nessun <c>OwnerId</c> bindabile (R58): l'identita' nasce qui, non la possiede ancora nessuno.</summary>
/// <param name="Email">Indirizzo email, diventa anche lo username.</param>
/// <param name="Password">Password in chiaro sul solo canale HTTPS: hashing a carico di ASP.NET Identity.</param>
/// <param name="DisplayName">Nome visualizzato, opzionale.</param>
public sealed record RegisterRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password,
    string? DisplayName);
