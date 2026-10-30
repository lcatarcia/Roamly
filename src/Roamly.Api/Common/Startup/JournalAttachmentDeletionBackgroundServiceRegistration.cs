using Roamly.Api.Common.Ownership;

namespace Roamly.Api.Common.Startup;

/// <summary>Registra il drainer periodico solo quando si avvia l'host HTTP normale.</summary>
public static class JournalAttachmentDeletionBackgroundServiceRegistration
{
    public static IServiceCollection AddJournalAttachmentDeletionBackgroundService(
        this IServiceCollection services,
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(args);

        if (!args.Contains("--job", StringComparer.Ordinal))
        {
            services.AddHostedService<JournalAttachmentDeletionBackgroundService>();
        }

        return services;
    }
}
