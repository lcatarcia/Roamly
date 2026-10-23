using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Infrastructure;

public sealed class SqlConnectionRetryTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task runtime_sql_connection_has_open_retry_without_ef_execution_strategy_retries()
    {
        var phase1 = await sqlServer.Phase1MigratedAsync();
        Assert.Null(phase1.Failure);

        await using var factory = new RoamlyApiFactory(phase1.Lease.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
        var connection = Assert.IsType<SqlConnection>(context.Database.GetDbConnection());
        Assert.NotNull(connection.RetryLogicProvider);
        var retryLogic = connection.RetryLogicProvider.RetryLogic;

        Assert.Equal(5, retryLogic.NumberOfTries);
        Assert.Equal(TimeSpan.FromSeconds(1), retryLogic.RetryIntervalEnumerator.GapTimeInterval);
        Assert.Equal(TimeSpan.FromSeconds(20), retryLogic.RetryIntervalEnumerator.MaxTimeInterval);
        Assert.False(context.Database.CreateExecutionStrategy().RetriesOnFailure);
    }
}
