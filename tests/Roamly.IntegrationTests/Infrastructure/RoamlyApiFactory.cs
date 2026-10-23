using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Roamly.Api.Common.Security;

namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>Host HTTP d'integrazione puntato al database SQL Server temporaneo di L1.</summary>
public sealed class RoamlyApiFactory(
    string connectionString,
    string environmentName = "Testing",
    Action<IServiceCollection>? configureTestServices = null,
    IPAddress? controlledTestIpAddress = null) : WebApplicationFactory<Program>
{
    private static readonly object EnvironmentVariableLock = new();
    private static readonly string TestHmacSecret =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly IPAddress? _testIpAddress = controlledTestIpAddress
        ?? (environmentName is "Development" or "Testing" ? CreateTestIpAddress() : null);

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.ConfigureTestServices(services =>
        {
            if (_testIpAddress is not null)
            {
                services.RemoveAll<ILoginClientIpAddressProvider>();
                services.AddSingleton<ILoginClientIpAddressProvider>(
                    new TestLoginClientIpAddressProvider(_testIpAddress));
            }

            configureTestServices?.Invoke(services);
        });
    }

    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        lock (EnvironmentVariableLock)
        {
            const string connectionStringVariable = "ConnectionStrings__Roamly";
            const string hmacSecretVariable = "RateLimiting__Login__HmacSecret";
            var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringVariable);
            var previousHmacSecret = Environment.GetEnvironmentVariable(hmacSecretVariable);

            try
            {
                // Minimal hosting runs Program while the factory creates its deferred host.
                Environment.SetEnvironmentVariable(connectionStringVariable, connectionString);
                Environment.SetEnvironmentVariable(hmacSecretVariable, TestHmacSecret);
                return base.CreateHost(builder);
            }
            finally
            {
                Environment.SetEnvironmentVariable(connectionStringVariable, previousConnectionString);
                Environment.SetEnvironmentVariable(hmacSecretVariable, previousHmacSecret);
            }
        }
    }

    private static IPAddress CreateTestIpAddress()
    {
        var address = RandomNumberGenerator.GetBytes(4);
        address[0] = 198;
        address[1] = 18;
        return new IPAddress(address);
    }
}
