using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Roamly.Api.Common.Startup;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Hosting;

[Collection(DatabaseCollection.Name)]
public sealed class HealthChecksTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task Live_is_independent_from_sql_while_ready_reports_sql_failure()
    {
        var database = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(database.Failure);

        await using var factory = new RoamlyApiFactory(
            database.Lease.ConnectionString,
            configureTestServices: services =>
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    var sqlCheck = options.Registrations.Single(registration => registration.Name == "database");
                    options.Registrations.Remove(sqlCheck);
                    options.Registrations.Add(new HealthCheckRegistration(
                        "database",
                        _ => new UnhealthySqlHealthCheck(),
                        HealthStatus.Unhealthy,
                        [ObservabilityRegistration.ReadyTag]));
                }));
        using var client = factory.CreateClient();

        using var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        using var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }

    private sealed class UnhealthySqlHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Unhealthy("SQL unavailable"));
    }
}
