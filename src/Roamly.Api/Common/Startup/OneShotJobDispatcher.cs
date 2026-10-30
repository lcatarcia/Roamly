using Roamly.Api.Common.Identity;
using Roamly.Api.Common.Ownership;
using Roamly.Api.Features.Journal;
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

        if (args is ["--job", "account-erasure"])
        {
            await RunAccountErasureAsync(services, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (args is ["--job", "provision-user"])
        {
            await RunUserProvisioningAsync(services, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (args is ["--job", "journal-attachment-reconciliation"])
        {
            await RunJournalAttachmentReconciliationAsync(services, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (args is ["--job", "journal-attachment-deletion-outbox"])
        {
            await RunJournalAttachmentDeletionOutboxAsync(services, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (args is ["--job", "login-attempt-counter-cleanup"])
        {
            await RunLoginAttemptCounterCleanupAsync(services, cancellationToken).ConfigureAwait(false);
            return true;
        }

        ThrowIfInvalidJob(args);
        return false;
    }

    private static async Task RunAccountErasureAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AccountErasureJob>()
            .RunOnceAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunUserProvisioningAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProvisionUserJob>()
            .RunOnceAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ThrowIfInvalidJob(string[] args)
    {
        if (args.Contains("--job", StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Uso job non valido. Modalita' supportate: --job account-erasure, --job provision-user, " +
                "--job journal-attachment-reconciliation, --job journal-attachment-deletion-outbox, " +
                "--job login-attempt-counter-cleanup.");
        }
    }

    private static async Task RunLoginAttemptCounterCleanupAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ILoginAttemptCounterMaintenance>()
            .CleanupExpiredAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunJournalAttachmentReconciliationAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<JournalAttachmentReconciliationJob>()
            .RunOnceAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunJournalAttachmentDeletionOutboxAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<JournalAttachmentDeletionOutboxJob>()
            .RunOnceAsync(cancellationToken).ConfigureAwait(false);
    }
}
