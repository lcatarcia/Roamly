using Roamly.Common;

namespace Roamly.Api.Common.Ownership;

/// <summary>Scope mutabile limitato al singolo servizio DI, senza stato statico o AsyncLocal.</summary>
public sealed class CurrentUserScope : ICurrentUserScope
{
    private readonly object _gate = new();
    private Execution? _activeExecution;

    /// <inheritdoc />
    public bool IsRunningAsSystem => CurrentExecution()?.Mode == ExecutionMode.System;

    /// <inheritdoc />
    public bool IsRunningAsUser => CurrentExecution()?.Mode == ExecutionMode.User;

    /// <inheritdoc />
    public Guid UserId => CurrentExecution() is { Mode: ExecutionMode.User, UserId: { } userId }
        ? userId
        : throw new InvalidOperationException(
            "ICurrentUserScope.UserId e' disponibile solo dentro un'operazione RunAsUser.");

    /// <inheritdoc />
    public async Task<TResult> RunAsSystemAsync<TResult>(string reason, Func<Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var execution = Enter(ExecutionMode.System, null, reason);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            Exit(execution);
        }
    }

    /// <inheritdoc />
    public async Task<TResult> RunAsUserAsync<TResult>(
        Guid userId,
        string reason,
        Func<Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("RunAsUser richiede un identificativo valido.", nameof(userId));
        }

        var execution = Enter(ExecutionMode.User, userId, reason);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            Exit(execution);
        }
    }

    private Execution Enter(ExecutionMode mode, Guid? userId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Il motivo dell'esecuzione e' obbligatorio.", nameof(reason));
        }

        lock (_gate)
        {
            if (_activeExecution is not null)
            {
                throw new InvalidOperationException(
                    "Un'operazione RunAs e' gia' attiva in questo scope DI.");
            }

            _activeExecution = new Execution(mode, userId, reason);
            return _activeExecution;
        }
    }

    private Execution? CurrentExecution()
    {
        lock (_gate)
        {
            return _activeExecution;
        }
    }

    private void Exit(Execution expected)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_activeExecution, expected))
            {
                throw new InvalidOperationException("Lo scope RunAs e' stato alterato prima del completamento.");
            }

            _activeExecution = null;
        }
    }

    private enum ExecutionMode
    {
        System,
        User,
    }

    private sealed record Execution(ExecutionMode Mode, Guid? UserId, string Reason);
}
