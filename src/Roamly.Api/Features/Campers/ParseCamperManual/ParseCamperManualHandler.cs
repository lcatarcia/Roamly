using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Roamly.Api.Common.Http;
using Roamly.Infrastructure.Persistence;

namespace Roamly.Api.Features.Campers.ParseCamperManual;

/// <summary>Analizza un manuale PDF o una scheda tecnica esterna per un camper posseduto dall'utente.</summary>
[ProtocolOutcomeHandler("404 per camper non posseduto e 422 quando il PDF o il link non contengono testo tecnico estraibile.")]
public sealed class ParseCamperManualHandler(RoamlyDbContext db)
{
    private const int MaxDownloadBytes = 15 * 1024 * 1024;

    private static readonly HttpClient ExternalHttpClient = CreateExternalHttpClient();

    /// <summary>Esegue l'estrazione della bozza tecnica e delle misure senza modificare il database.</summary>
    public async Task<ParseCamperManualResult> HandleAsync(
        Guid camperId,
        ParseCamperManualRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ownsCamper = await db.Campers
            .AsNoTracking()
            .AnyAsync(
                item => item.OwnerId == db.CurrentUserId && item.Id == camperId,
                cancellationToken)
            .ConfigureAwait(false);

        if (!ownsCamper)
        {
            return new ParseCamperManualResult(ParseCamperManualOutcome.NotFound);
        }

        IReadOnlyList<ExtractedManualPage> pages;
        if (!string.IsNullOrWhiteSpace(request.PdfBase64))
        {
            if (!TryDecodePdfBase64(request.PdfBase64, out var pdfBytes, out var error))
            {
                return new ParseCamperManualResult(ParseCamperManualOutcome.UnprocessableSource, ErrorDetail: error);
            }

            pages = CamperManualTextExtractor.ExtractFromPdf(pdfBytes);
        }
        else if (!string.IsNullOrWhiteSpace(request.RawText))
        {
            pages = CamperManualTextExtractor.ExtractFromTextOrHtml(request.RawText);
        }
        else if (!string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            var fetched = await FetchExternalPagesAsync(request.SourceUrl.Trim(), cancellationToken)
                .ConfigureAwait(false);
            if (fetched.Error is not null)
            {
                return new ParseCamperManualResult(
                    ParseCamperManualOutcome.UnprocessableSource,
                    ErrorDetail: fetched.Error);
            }

            pages = fetched.Pages;
        }
        else
        {
            return new ParseCamperManualResult(
                ParseCamperManualOutcome.UnprocessableSource,
                ErrorDetail: "Nessuna fonte fornita per l'estrazione.");
        }

        if (pages.Count == 0 || pages.All(page => page.Lines.Count == 0))
        {
            return new ParseCamperManualResult(
                ParseCamperManualOutcome.UnprocessableSource,
                ErrorDetail: "Il documento non contiene testo selezionabile: se si tratta di una scansione fotografica, incolla i dati o usa un PDF testuale.");
        }

        var response = CamperManualParser.Parse(pages, request.SourceName, request.SourceUrl);
        return new ParseCamperManualResult(ParseCamperManualOutcome.Parsed, response);
    }

    private static bool TryDecodePdfBase64(string rawBase64, out byte[] bytes, out string? error)
    {
        var payload = rawBase64.Trim();
        var commaIndex = payload.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex >= 0 && payload[..commaIndex].Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            payload = payload[(commaIndex + 1)..].Trim();
        }

        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            bytes = [];
            error = "Il file caricato non è codificato in formato Base64 valido.";
            return false;
        }

        if (bytes.Length == 0 || bytes.Length > MaxDownloadBytes)
        {
            error = "Il file PDF deve avere una dimensione compresa tra 1 byte e 15 MB.";
            return false;
        }

        if (!CamperManualTextExtractor.IsPdf(bytes))
        {
            error = "Il file caricato non è un documento PDF valido.";
            return false;
        }

        error = null;
        return true;
    }

    private static async Task<(IReadOnlyList<ExtractedManualPage> Pages, string? Error)> FetchExternalPagesAsync(
        string url,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return ([], "Il link esterno deve utilizzare il protocollo HTTPS.");
        }

        if (!await IsSafePublicHostAsync(uri.DnsSafeHost, cancellationToken).ConfigureAwait(false))
        {
            return ([], "L'indirizzo specificato non è un host pubblico consentito.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("RoamlyManualParser/1.0");
            using var response = await ExternalHttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return ([], $"Impossibile scaricare il documento dal link specificato (HTTP {(int)response.StatusCode}).");
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (memory.Length + read > MaxDownloadBytes)
                {
                    return ([], "Il documento remoto supera il limite massimo di 15 MB.");
                }

                memory.Write(buffer, 0, read);
            }

            var bytes = memory.ToArray();
            if (CamperManualTextExtractor.IsPdf(bytes))
            {
                return (CamperManualTextExtractor.ExtractFromPdf(bytes), null);
            }

            var text = Encoding.UTF8.GetString(bytes);
            return (CamperManualTextExtractor.ExtractFromTextOrHtml(text), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return ([], "Impossibile contattare il link esterno: verifica che l'URL sia pubblico e direttamente accessibile.");
        }
    }

    private static async Task<bool> IsSafePublicHostAsync(string host, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host)
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            return addresses.Length > 0 && addresses.All(IsPublicIpAddress);
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool IsPublicIpAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                0 or 10 or 127 => false,
                169 when bytes[1] == 254 => false,
                172 when bytes[1] is >= 16 and <= 31 => false,
                192 when bytes[1] == 168 => false,
                100 when bytes[1] is >= 64 and <= 127 => false,
                _ => true,
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !address.IsIPv6LinkLocal
                && !address.IsIPv6SiteLocal
                && !address.IsIPv6Multicast
                && (address.GetAddressBytes()[0] & 0xFE) != 0xFC;
        }

        return false;
    }

    private static HttpClient CreateExternalHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(8),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
    }
}
