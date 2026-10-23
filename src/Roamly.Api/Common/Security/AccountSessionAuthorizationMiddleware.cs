using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Identity;

namespace Roamly.Api.Common.Security;

/// <summary>
/// Verifica lo stato persistito dell'account per ogni richiesta protetta, dopo l'autenticazione
/// cookie e prima dell'autorizzazione. La lettura non usa cache condivisa: ogni richiesta osserva
/// il flag cambio password e lo stato di cancellazione correnti.
/// </summary>
public sealed class AccountSessionAuthorizationMiddleware(RequestDelegate next)
{
    private const string DeletionPendingProblemType = "https://roamly.dev/problems/account-deletion-pending";
    private const string RestrictedSessionProblemType = "https://roamly.dev/problems/restricted-session";
    private const string PasswordChangeRequiredProblemType = "https://roamly.dev/problems/password-change-required";

    /// <summary>Controlla sessione limitata e stato persistito dell'account.</summary>
    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(userManager);

        if (!IsProtectedRequest(context) || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var account = await FindAccountAsync(context, userManager).ConfigureAwait(false);
        if (account is null)
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, "Autenticazione richiesta.",
                DeletionPendingProblemType, signOut: true).ConfigureAwait(false);
            return;
        }

        if (IsDeletionLimited(context.User))
        {
            if (!AllowsRestrictedSession(context))
            {
                await RejectAsync(context, StatusCodes.Status403Forbidden, "Accesso non consentito.",
                    RestrictedSessionProblemType, signOut: false).ConfigureAwait(false);
                return;
            }
        }
        else if (account.DeletionRequestedAtUtc is not null)
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, "Autenticazione richiesta.",
                DeletionPendingProblemType, signOut: true).ConfigureAwait(false);
            return;
        }

        if (account.RequiresPasswordChange && !AllowsPasswordChangeSession(context))
        {
            await RejectAsync(context, StatusCodes.Status403Forbidden, "Cambio password richiesto.",
                PasswordChangeRequiredProblemType, signOut: false).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool IsProtectedRequest(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;

    private static bool IsDeletionLimited(ClaimsPrincipal principal) =>
        principal.FindFirstValue(AccountSessionClaims.ModeClaimType) == AccountSessionClaims.DeletionLimited;

    private static bool AllowsRestrictedSession(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<AllowRestrictedAccountSessionAttribute>() is not null;

    private static bool AllowsPasswordChangeSession(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<AllowPasswordChangeSessionAttribute>() is not null;

    private static async Task<ApplicationUser?> FindAccountAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out _)
            ? await userManager.FindByIdAsync(userId!).ConfigureAwait(false)
            : null;
    }

    private static async Task RejectAsync(
        HttpContext context,
        int statusCode,
        string title,
        string problemType,
        bool signOut)
    {
        if (signOut)
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(
            ProblemFactory.Create(context, statusCode, title, problemType),
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted).ConfigureAwait(false);
    }
}
