using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Authentication;

[Collection(DatabaseCollection.Name)]
public sealed class DataProtectionKeyPersistenceTests(SqlServerFixture sqlServer)
{
    private const string Purpose = "Roamly.IntegrationTests.DataProtectionKeyPersistence";

    [Fact]
    public async Task Protected_payload_survives_api_host_recreation_using_the_database_key_ring()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        var cancellationToken = TestContext.Current.CancellationToken;
        await migrated.Lease.ResetAsync(cancellationToken);

        string protectedPayload;
        await using (var firstHost = new RoamlyApiFactory(migrated.ConnectionString))
        {
            var provider = firstHost.Services.GetRequiredService<IDataProtectionProvider>();
            protectedPayload = provider.CreateProtector(Purpose).Protect("payload");
        }

        var persistedKeyCount = await CountPersistedKeysAsync(migrated.ConnectionString, cancellationToken);
        Assert.True(persistedKeyCount > 0, "La prima istanza non ha persistito chiavi Data Protection in SQL Server.");

        await using var secondHost = new RoamlyApiFactory(migrated.ConnectionString);
        var secondProvider = secondHost.Services.GetRequiredService<IDataProtectionProvider>();
        var unprotectedPayload = secondProvider.CreateProtector(Purpose).Unprotect(protectedPayload);

        Assert.Equal("payload", unprotectedPayload);
    }

    private static async Task<long> CountPersistedKeysAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT_BIG(*) FROM [dbo].[DataProtectionKeys];";
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
