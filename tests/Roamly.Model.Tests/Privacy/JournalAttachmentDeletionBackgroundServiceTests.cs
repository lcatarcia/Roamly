using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Roamly.Api.Common.Ownership;
using Roamly.Api.Common.Startup;

namespace Roamly.Model.Tests.Privacy;

public sealed class JournalAttachmentDeletionBackgroundServiceTests
{
    [Fact]
    public async Task Normal_api_registration_drains_immediately_and_retries_after_each_five_minute_tick()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 8, 0, 0, TimeSpan.Zero));
        var drainer = new FailOnceJournalAttachmentDeletionDrainer();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJournalAttachmentDeletionDrainer>(drainer);
        services.AddSingleton<TimeProvider>(clock);
        services.AddJournalAttachmentDeletionBackgroundService([]);
        using var provider = services.BuildServiceProvider();
        var hostedService = Assert.IsType<JournalAttachmentDeletionBackgroundService>(
            Assert.Single(provider.GetServices<IHostedService>()));

        try
        {
            await hostedService.StartAsync(CancellationToken.None);
            await drainer.WaitForCallAsync(1);
            clock.Advance(TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(59)));
            Assert.Equal(1, drainer.CallCount);

            clock.Advance(TimeSpan.FromSeconds(1));
            await drainer.WaitForCallAsync(2);
            clock.Advance(TimeSpan.FromMinutes(5));
            await drainer.WaitForCallAsync(3);
            Assert.Equal(1, drainer.MaximumConcurrentCalls);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void One_shot_invocations_do_not_register_the_periodic_hosted_service()
    {
        var services = new ServiceCollection();

        services.AddJournalAttachmentDeletionBackgroundService(["--job", "journal-attachment-deletion-outbox"]);

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(JournalAttachmentDeletionBackgroundService));
    }

    private sealed class FailOnceJournalAttachmentDeletionDrainer : IJournalAttachmentDeletionDrainer
    {
        private readonly TaskCompletionSource[] _calls =
        [
            NewSignal(),
            NewSignal(),
            NewSignal(),
        ];
        private int _callCount;
        private int _activeCalls;
        private int _maximumConcurrentCalls;

        public int CallCount => Volatile.Read(ref _callCount);
        public int MaximumConcurrentCalls => Volatile.Read(ref _maximumConcurrentCalls);

        public async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _activeCalls);
            UpdateMaximum(active);
            var call = Interlocked.Increment(ref _callCount);

            try
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                if (call == 1)
                {
                    throw new InvalidOperationException("Transient storage error.");
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeCalls);
                if (call <= _calls.Length)
                {
                    _calls[call - 1].TrySetResult();
                }
            }
        }

        public Task WaitForCallAsync(int call) =>
            _calls[call - 1].Task.WaitAsync(TimeSpan.FromSeconds(5));

        private void UpdateMaximum(int active)
        {
            while (active > MaximumConcurrentCalls)
            {
                var prior = Interlocked.CompareExchange(ref _maximumConcurrentCalls, active, MaximumConcurrentCalls);
                if (prior == MaximumConcurrentCalls)
                {
                    return;
                }
            }
        }

        private static TaskCompletionSource NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
