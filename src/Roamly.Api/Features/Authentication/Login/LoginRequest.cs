using System.ComponentModel.DataAnnotations;

namespace Roamly.Api.Features.Authentication.Login;

/// <summary>Corpo di <c>POST /api/v1/auth/login</c>.</summary>
/// <param name="Email">Email/username.</param>
/// <param name="Password">Password in chiaro sul solo canale HTTPS.</param>
public sealed record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);
