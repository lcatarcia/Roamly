using Roamly.Api.Common.Identity;
using Roamly.Common.Security;

namespace Roamly.Api.Common.Startup;

/// <summary>Instrada i job amministrativi one-shot prima dell'avvio del server HTTP.</summary>
public static class OneShotJobDispatcher
{
    public static async Task<bool> RunIfRequestedAsync(
        string[] args,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(services);

        if (args is ["--job", "provision-user"])
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ProvisionUserJob>()
                .RunOnceAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (args is ["--job", "login-attempt-counter-cleanup"])
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ILoginAttemptCounterMaintenance>()
                .CleanupExpiredAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (args.Contains("--job", StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Uso job non valido. Modalita' supportate: --job provision-user, " +
                "--job login-attempt-counter-cleanup.");
        }

        return false;
    }
}
