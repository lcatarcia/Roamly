using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Common.Startup;

/// <summary>
/// Health check (R62): <c>/health/live</c> non tocca il database, <c>/health/ready</c> si'.
/// Entrambi fuori da <c>/api/v1</c> e anonimi — esclusi deliberatamente da R45 (nessun
/// <c>ProblemDetails</c>: il payload di un health check e' un contratto di infrastruttura,
/// non dell'API pubblica).
/// </summary>
public static class ObservabilityRegistration
{
    /// <summary>Nome del check di readiness, usato per filtrare i tag in <c>Program.cs</c>.</summary>
    public const string ReadyTag = "ready";

    /// <summary>Registra i health check.</summary>
    /// <param name="services">Collezione dei servizi.</param>
    /// <returns>La collezione, per concatenare le altre registrazioni.</returns>
    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag]);

        return services;
    }
}

/// <summary>
/// Verifica che il database sia raggiungibile con un timeout breve: <c>/health/ready</c> non deve
/// restare appeso quanto il timeout di comando di default di EF Core.
/// </summary>
internal sealed class DatabaseHealthCheck : IHealthCheck
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

    private readonly RoamlyDbContext _context;

    /// <summary>Costruisce il check sopra il contesto EF gia' registrato nel container.</summary>
    /// <param name="context">Contesto su cui verificare la connessione.</param>
    public DatabaseHealthCheck(RoamlyDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = new CancellationTokenSource(ConnectTimeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        try
        {
            var canConnect = await _context.Database
                .CanConnectAsync(linkedSource.Token)
                .ConfigureAwait(false);

            return canConnect
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database non raggiungibile.");
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Database non raggiungibile entro il timeout.");
        }
    }
}
