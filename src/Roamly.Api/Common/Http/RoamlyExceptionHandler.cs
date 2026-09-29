using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Roamly.Common;

namespace Roamly.Api.Common.Http;

/// <summary>
/// Ultimo presidio sulle eccezioni non gestite: risponde sempre con un <see cref="ProblemFactory"/>
/// (R45/R46), non con la pagina di sviluppo di default ne' con lo stack trace.
/// <para>
/// <see cref="OwnershipViolationException"/> (R3) e' distinta solo nel log, mai nella risposta:
/// e' un bug applicativo (un tentativo di scrittura fuori scope che ha superato la revisione), non
/// un errore dell'utente, quindi la risposta resta un generico <c>500</c> — la distinzione vive
/// esclusivamente sulla pipeline di logging strutturato (<c>ARCHITECTURE.md</c> §6), mai in una
/// risposta HTTP che un chiamante ostile potrebbe osservare.
/// </para>
/// </summary>
public sealed partial class RoamlyExceptionHandler : IExceptionHandler
{
    private const string GenericProblemType = "https://roamly.dev/problems/unexpected-error";

    private readonly ILogger<RoamlyExceptionHandler> _logger;

    /// <summary>Costruisce l'handler con il logger su cui vivono gli eventi di sicurezza.</summary>
    /// <param name="logger">Logger su cui vive l'evento <c>ownership_violation</c>.</param>
    public RoamlyExceptionHandler(ILogger<RoamlyExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OwnershipViolationException ownershipViolation)
        {
            LogOwnershipViolation(ownershipViolation, ownershipViolation.EntityType);
        }
        else
        {
            LogUnhandledException(exception, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problem = ProblemFactory.Create(
            httpContext,
            StatusCodes.Status500InternalServerError,
            "Si e' verificato un errore imprevisto.",
            GenericProblemType);

        await httpContext.Response
            .WriteAsJsonAsync(problem, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "ownership_violation: tentativo di scrittura fuori scope su {EntityType}")]
    private partial void LogOwnershipViolation(Exception exception, string entityType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Eccezione non gestita su {Path}")]
    private partial void LogUnhandledException(Exception exception, PathString path);
}
