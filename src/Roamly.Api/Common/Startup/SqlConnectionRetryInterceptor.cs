using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Roamly.Api.Common.Startup;

/// <summary>
/// Configura retry limitati alla fase di apertura delle connessioni SQL Server.
/// </summary>
internal sealed class SqlConnectionRetryInterceptor : DbConnectionInterceptor
{
    /// <inheritdoc />
    public override DbConnection ConnectionCreated(ConnectionCreatedEventData eventData, DbConnection connection)
    {
        if (connection is SqlConnection sqlConnection)
        {
            sqlConnection.RetryLogicProvider = SqlConfigurableRetryFactory.CreateExponentialRetryProvider(
                new SqlRetryLogicOption
                {
                    NumberOfTries = 5,
                    DeltaTime = TimeSpan.FromSeconds(1),
                    MaxTimeInterval = TimeSpan.FromSeconds(20),
                });
        }

        return connection;
    }
}
