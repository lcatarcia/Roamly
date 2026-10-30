using Microsoft.Extensions.DependencyInjection;

namespace Roamly.Api.Common.Ownership;

/// <summary>Drena gli intenti Blob all'avvio e ogni cinque minuti finché l'API è attiva.</summary>
public sealed partial class JournalAttachmentDeletionBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<JournalAttachmentDeletionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan DrainInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(DrainInterval, timeProvider);
        await DrainAsync(stoppingToken).ConfigureAwait(false);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await DrainAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task DrainAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IJournalAttachmentDeletionDrainer>()
                .RunOnceAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            DrainFailed(logger, exception);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Journal Blob deletion outbox drain failed; pending intents will be retried.")]
    private static partial void DrainFailed(ILogger logger, Exception exception);
}
