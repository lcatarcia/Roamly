using System.Net.Http.Json;

namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>
/// Client cookie-aware per i test L2: mantiene nascosta l'estrazione antiforgery e la ripete
/// dopo il login, quando cambia l'identita' associata al token (R35, ADR-0009).
/// </summary>
public sealed class RoamlyClient : IDisposable
{
    private readonly HttpClient _httpClient;

    /// <summary>Costruisce il wrapper sopra un client della WebApplicationFactory.</summary>
    public RoamlyClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>Esegue il login vero dell'utente e conserva il cookie applicativo.</summary>
    public Task<HttpResponseMessage> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        return LoginWithForwardedForAsync(email, password, null, cancellationToken);
    }

    /// <summary>Esegue il login aggiungendo X-Forwarded-For per i test dell'ingress ACA.</summary>
    public async Task<HttpResponseMessage> LoginWithForwardedForAsync(
        string email,
        string password,
        string? forwardedFor,
        CancellationToken cancellationToken)
    {
        var token = await FetchCsrfTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password }),
        };
        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add("X-XSRF-TOKEN", token);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Esegue un POST JSON dopo aver ottenuto e applicato il token antiforgery.</summary>
    public async Task<HttpResponseMessage> PostJsonAsync(
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        return await PostJsonWithCsrfHeadersAsync(
            path,
            body,
            includeCustomRequestHeader: true,
            includeAntiforgeryHeader: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Esegue un POST ottenendo il cookie antiforgery e scegliendo quali delle due intestazioni
    /// inviare. Consente ai test HTTP di dimostrare che entrambe le barriere sono necessarie.
    /// </summary>
    public async Task<HttpResponseMessage> PostJsonWithCsrfHeadersAsync(
        string path,
        object body,
        bool includeCustomRequestHeader,
        bool includeAntiforgeryHeader,
        CancellationToken cancellationToken)
    {
        var token = await FetchCsrfTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        if (includeCustomRequestHeader)
        {
            request.Headers.Add("X-Roamly-Request", "1");
        }

        if (includeAntiforgeryHeader)
        {
            request.Headers.Add("X-XSRF-TOKEN", token);
        }

        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Esegue un DELETE con entrambe le barriere CSRF, senza aggiungere If-Match.</summary>
    public async Task<HttpResponseMessage> DeleteWithCsrfAsync(string path, CancellationToken cancellationToken)
    {
        var token = await FetchCsrfTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Esegue una richiesta GET mantenendo il cookie applicativo del client.</summary>
    public Task<HttpResponseMessage> GetAsync(string path, CancellationToken cancellationToken)
    {
        return _httpClient.GetAsync(path, cancellationToken);
    }

    /// <summary>Invia una richiesta gia' costruita mantenendo il cookie applicativo del client.</summary>
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _httpClient.SendAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _httpClient.Dispose();

    private async Task<string> FetchCsrfTokenAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetFromJsonAsync<CsrfTokenResponse>(
            "/api/v1/csrf-token",
            cancellationToken).ConfigureAwait(false);

        return response?.Token
            ?? throw new InvalidOperationException("L'endpoint CSRF non ha restituito un token.");
    }

    private sealed record CsrfTokenResponse(string Token);
}
