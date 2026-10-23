using Microsoft.AspNetCore.WebUtilities;

namespace Roamly.Api.Common.Http;

/// <summary>Codifica e legge l'ETag forte usato per un token SQL Server rowversion.</summary>
public static class RowVersionEtag
{
    /// <summary>Restituisce un ETag forte; un token assente non produce un ETag vuoto.</summary>
    public static string? Format(byte[]? rowVersion)
    {
        return rowVersion is { Length: > 0 }
            ? $"\"{WebEncoders.Base64UrlEncode(rowVersion)}\""
            : null;
    }

    /// <summary>Legge il token generato da <see cref="Format"/> senza accettare forme ambigue.</summary>
    public static bool TryParse(string? etag, out byte[] rowVersion)
    {
        rowVersion = [];
        if (etag is null || etag.Length < 2 || etag[0] != '"' || etag[^1] != '"')
        {
            return false;
        }

        try
        {
            rowVersion = WebEncoders.Base64UrlDecode(etag[1..^1]);
            return rowVersion.Length == 8;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
