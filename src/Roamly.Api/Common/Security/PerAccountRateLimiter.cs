using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Roamly.Common.Security;

namespace Roamly.Api.Common.Security;

/// <summary>
/// Rate limiting **in-memory per-account** per le azioni sensibili di export e cancellazione
/// account. Il login usa il distinto <see cref="ILoginAttemptRateLimiter"/> condiviso su SQL.
/// <para>
/// Questa implementazione resta invariata per preservare soglie e semantica dei flussi esistenti;
/// non va riutilizzata per introdurre limiti al login.
/// </para>
/// </summary>
public interface IPerAccountRateLimiter
{
    /// <summary>Registra un tentativo per la chiave data. Restituisce <see langword="false"/> se la soglia e' superata.</summary>
    /// <param name="accountKey">Identificativo dell'account, gia' normalizzato dal chiamante.</param>
    bool TryAcquire(string accountKey);
}

/// <summary>Soglie delle azioni sensibili, sovrascrivibili da configurazione.</summary>
public sealed class PerAccountRateLimiterOptions
{
    /// <summary>Numero massimo di tentativi ammessi nella finestra.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Ampiezza della finestra scorrevole.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(5);
}

/// <inheritdoc cref="IPerAccountRateLimiter" />
public sealed class PerAccountRateLimiter : IPerAccountRateLimiter
{
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<PerAccountRateLimiterOptions> _options;
    private readonly TimeProvider _clock;

    /// <summary>Costruisce il limiter con cache, opzioni e orologio iniettati.</summary>
    /// <param name="cache">Cache in-memory su cui vivono i bucket per account.</param>
    /// <param name="options">Soglie correnti, rilette a ogni chiamata per essere sovrascrivibili a runtime dai test.</param>
    /// <param name="clock">Orologio iniettato (R34): niente lettura diretta del tempo di sistema.</param>
    public PerAccountRateLimiter(IMemoryCache cache, IOptionsMonitor<PerAccountRateLimiterOptions> options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _cache = cache;
        _options = options;
        _clock = clock;
    }

    /// <inheritdoc />
    public bool TryAcquire(string accountKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);

        var currentOptions = _options.CurrentValue;
        var bucket = _cache.GetOrCreate($"auth-rate-limit:{accountKey}", entry =>
        {
            entry.SlidingExpiration = currentOptions.Window;
            return new AttemptBucket();
        })!;

        lock (bucket.Attempts)
        {
            var now = _clock.GetUtcNow();
            bucket.Attempts.RemoveAll(attempt => now - attempt > currentOptions.Window);

            if (bucket.Attempts.Count >= currentOptions.MaxAttempts)
            {
                return false;
            }

            bucket.Attempts.Add(now);
            return true;
        }
    }

    private sealed class AttemptBucket
    {
        public List<DateTimeOffset> Attempts { get; } = [];
    }
}
