using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Roamly.Api.Common.Security;

/// <summary>
/// Rate limiting **per-account**, non per IP (SECURITY.md §6): la chiave e' l'identificativo
/// tentato (email normalizzata), non l'indirizzo del chiamante. Il rate limiting per IP dipende
/// dalla topologia di deploy (decisione #9, ancora aperta — <c>ForwardedHeaders</c> configurato sui
/// proxy fidati) e resta deliberatamente fuori scope.
/// <para>
/// Implementazione in-memory: adatta a un singolo processo. Se Roamly scalasse a piu' istanze
/// servirebbe uno store condiviso (Redis) — non necessario finche' il deploy resta a istanza
/// singola (decisione #9 ancora aperta).
/// </para>
/// </summary>
public interface IPerAccountRateLimiter
{
    /// <summary>Registra un tentativo per la chiave data. Restituisce <see langword="false"/> se la soglia e' superata.</summary>
    /// <param name="accountKey">Identificativo dell'account, gia' normalizzato dal chiamante.</param>
    bool TryAcquire(string accountKey);
}

/// <summary>Soglie del limiter, sovrascrivibili da configurazione (obbligatorio per non rendere flaky i test — vedi <c>SECURITY.md</c> §6.3).</summary>
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
