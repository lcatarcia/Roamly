using Microsoft.Extensions.DependencyInjection;
using Roamly.Api.Common.Startup;
using Roamly.Infrastructure.Maps;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Model.Tests.Maps;

public sealed class MapsDefaultRegistrationTests
{
    [Fact]
    public async Task Production_registration_is_inactive_and_denies_without_opening_a_database()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped(_ => new RoamlyDbContextFactory().CreateDbContext([]));
        services.AddFeatures();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var policy = scope.ServiceProvider.GetRequiredService<MapsEconomicPolicy>();
        Assert.False(policy.Enabled);
        Assert.False(policy.IsConfigured);
        Assert.IsType<UnavailableMapsCostObserver>(scope.ServiceProvider.GetRequiredService<IMapsCostObserver>());
        Assert.Equal(MapsAdmissionReason.NotConfigured, await scope.ServiceProvider.GetRequiredService<MapsEconomicGate>()
            .CheckAsync(TestContext.Current.CancellationToken));
    }
}
