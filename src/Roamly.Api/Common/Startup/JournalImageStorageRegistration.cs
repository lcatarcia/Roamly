using Azure.Identity;
using Azure.Storage.Blobs;
using Roamly.Common.Storage;
using Roamly.Infrastructure.Storage;

namespace Roamly.Api.Common.Startup;

/// <summary>Registra l'adapter Azurite locale o Azure Blob senza creare container o risorse.</summary>
public static class JournalImageStorageRegistration
{
    public static IServiceCollection AddJournalImageStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(provider => JournalImageStorageSettings.From(
            configuration,
            provider.GetRequiredService<IHostEnvironment>()));
        services.AddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<JournalImageStorageSettings>();
            return settings.Provider == "Azurite"
                ? new BlobServiceClient(settings.ConnectionString)
                : new BlobServiceClient(settings.ServiceUri!, new DefaultAzureCredential());
        });
        services.AddSingleton<IJournalImageStorage, AzureJournalImageStorage>();
        return services;
    }
}
