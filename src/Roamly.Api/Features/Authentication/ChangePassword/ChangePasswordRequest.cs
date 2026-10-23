using System.ComponentModel.DataAnnotations;

namespace Roamly.Api.Features.Authentication.ChangePassword;

/// <summary>Corpo di <c>POST /api/v1/auth/password/change</c>.</summary>
/// <param name="CurrentPassword">Password attuale usata per riautenticare la richiesta.</param>
/// <param name="NewPassword">Nuova password, verificata dalla policy ASP.NET Identity.</param>
public sealed record ChangePasswordRequest(
    [property: Required] string CurrentPassword,
    [property: Required] string NewPassword);
