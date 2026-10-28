using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Common.Identity;

/// <summary>Provisioning amministrativo locale senza endpoint pubblico o password negli argomenti.</summary>
public sealed class ProvisionUserJob(
    UserManager<ApplicationUser> users,
    RoamlyDbContext dbContext,
    TimeProvider clock,
    IProvisioningConsole console)
{
    private const int MaximumAccounts = 3;

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        EnsureInteractive();
        await EnsureCapacityAvailableAsync(cancellationToken).ConfigureAwait(false);
        var email = ReadEmail();
        var password = ReadConfirmedPassword();
        await CreateUserAsync(email, password, cancellationToken).ConfigureAwait(false);
        console.WriteLine($"Account creato per {email}. EmailConfirmed resta false.");
    }

    private void EnsureInteractive()
    {
        if (!console.IsInteractive)
        {
            throw new InvalidOperationException(
                "Il provisioning richiede una console interattiva; input reindirizzato non supportato.");
        }
    }

    private string ReadEmail()
    {
        console.WriteLine("Email:");
        var email = console.ReadEmail()?.Trim();
        return string.IsNullOrWhiteSpace(email)
            ? throw new InvalidOperationException("L'email non puo' essere vuota.")
            : email;
    }

    private string ReadConfirmedPassword()
    {
        var password = console.ReadSecret("Password: ");
        var confirmation = console.ReadSecret("Conferma password: ");
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmation))
        {
            throw new InvalidOperationException("Password e conferma non possono essere vuote.");
        }

        return password == confirmation
            ? password
            : throw new InvalidOperationException("Le password non corrispondono.");
    }

    private async Task CreateUserAsync(string email, string password, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await EnsureCapacityAvailableAsync(cancellationToken).ConfigureAwait(false);
        var user = CreateUnconfirmedUser(email);
        await CreateIdentityUserAsync(user, password).ConfigureAwait(false);
        await MarkAsProvisionedAsync(user).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureCapacityAvailableAsync(CancellationToken cancellationToken)
    {
        var accountCount = await dbContext.Users.CountAsync(cancellationToken).ConfigureAwait(false);
        if (accountCount >= MaximumAccounts)
        {
            throw new InvalidOperationException(
                $"Limite massimo di {MaximumAccounts} account raggiunto.");
        }
    }

    private ApplicationUser CreateUnconfirmedUser(string email) => new()
    {
        UserName = email,
        Email = email,
        EmailConfirmed = false,
        RequiresPasswordChange = true,
        CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
    };

    private async Task CreateIdentityUserAsync(ApplicationUser user, string password)
    {
        var result = await users.CreateAsync(user, password).ConfigureAwait(false);
        EnsureSucceeded(result, "Impossibile creare l'account");
    }

    private async Task MarkAsProvisionedAsync(ApplicationUser user)
    {
        var result = await users.AddClaimAsync(
            user,
            new Claim(ProvisionedAccountClaims.UnconfirmedLogin, "true")).ConfigureAwait(false);
        EnsureSucceeded(result, "Impossibile completare il provisioning dell'account");
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"{message}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }
}
