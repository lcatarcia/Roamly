using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Roamly.Infrastructure.Storage;

/// <summary>Configurazione selezionata per Azurite locale oppure Azure Blob.</summary>
public sealed record JournalImageStorageSettings(
    string Provider,
    string ContainerName,
    string? ConnectionString,
    Uri? ServiceUri,
    bool AzureAccountConfigurationVerified)
{
    /// <summary>Risoluzione fail-fast: nessun fallback locale silenzioso in un ambiente non locale.</summary>
    public static JournalImageStorageSettings From(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var configuredProvider = configuration["JournalImages:Provider"];
        var provider = configuredProvider
            ?? (environment.IsDevelopment() || environment.IsEnvironment("Testing") ? "Azurite" : string.Empty);

        if (string.Equals(provider, "Azurite", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException(
                    "JournalImages:Provider=Azurite e' consentito soltanto negli ambienti Development e Testing.");
            }

            return new JournalImageStorageSettings(
                "Azurite",
                configuration["JournalImages:ContainerName"] ?? "roamly-journal-images",
                configuration["JournalImages:Azurite:ConnectionString"] ?? "UseDevelopmentStorage=true",
                null,
                true);
        }

        if (string.Equals(provider, "AzureBlob", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(configuration["JournalImages:Azure:ServiceUri"], UriKind.Absolute, out var serviceUri)
            && string.Equals(serviceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(configuration["JournalImages:Azure:ContainerName"]))
        {
            return new JournalImageStorageSettings(
                "AzureBlob",
                configuration["JournalImages:Azure:ContainerName"]!,
                null,
                serviceUri,
                configuration.GetValue<bool>("JournalImages:Azure:AccountConfigurationVerified"));
        }

        throw new InvalidOperationException(
            "Configurare JournalImages:Provider=Azurite in locale/test oppure AzureBlob con ServiceUri e ContainerName.");
    }
}
