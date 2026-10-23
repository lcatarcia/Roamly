using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Cryptography;
using System.Net;
using Roamly.Api.Common.Security;

namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>Host HTTP d'integrazione puntato al database SQL Server temporaneo di L1.</summary>
public sealed class RoamlyApiFactory(
    string connectionString,
    string environmentName = "Testing",
    Action<IServiceCollection>? configureTestServices = null,
    IPAddress? controlledTestIpAddress = null,
    string? journalImagesAzuriteConnectionString = null,
    bool useAzureBlobJournalImageStorage = false,
    bool azureBlobAccountConfigurationVerified = false) : WebApplicationFactory<Program>
{
    private static readonly string TestHmacSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly IPAddress _testIpAddress = controlledTestIpAddress ?? CreateIsolatedTestIpAddress();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);

        builder.ConfigureTestServices(services =>
        {
            if (environmentName == "Testing" || environmentName == Environments.Development)
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
        builder.ConfigureHostConfiguration(configuration =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Roamly"] = connectionString,
                ["RateLimiting:Login:HmacSecret"] = TestHmacSecret,
            };
            if (journalImagesAzuriteConnectionString is not null)
            {
                settings["JournalImages:Provider"] = "Azurite";
                settings["JournalImages:Azurite:ConnectionString"] = journalImagesAzuriteConnectionString;
            }

            if (useAzureBlobJournalImageStorage)
            {
                settings["JournalImages:Provider"] = "AzureBlob";
                settings["JournalImages:Azure:ServiceUri"] =
                    "https://storoamlywse7wit7fzpja.blob.core.windows.net";
                settings["JournalImages:Azure:ContainerName"] = "roamly-journal-images";
                settings["JournalImages:Azure:AccountConfigurationVerified"] =
                    azureBlobAccountConfigurationVerified.ToString();
            }

            configuration.AddInMemoryCollection(settings);
        });

        return base.CreateHost(builder);
    }

    private static IPAddress CreateIsolatedTestIpAddress()
    {
        var address = RandomNumberGenerator.GetBytes(4);
        address[0] = 198;
        address[1] = 18;
        return new IPAddress(address);
    }
}
