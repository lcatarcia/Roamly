using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Roamly.Domain.ValueObjects;

namespace Roamly.Api.Features.Places;

/// <summary>Estrae esclusivamente coordinate esplicite da URL Google Maps, senza rete.</summary>
public static partial class GoogleMapsPosition
{
    private static readonly HashSet<string> Hosts = new(
        new[] { "com", "it", "fr", "de", "es", "co.uk", "nl", "pt", "ch", "at", "be", "ca", "com.au", "co.jp" }
            .SelectMany(suffix => new[] { $"google.{suffix}", $"www.google.{suffix}", $"maps.google.{suffix}" }),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Restituisce null per link non supportati, ambigui o coordinate non valide.</summary>
    public static Coordinates? Parse(string? value)
    {
        var uri = ReadMapsUri(value);
        if (uri is null) return null;
        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        if (path == "/maps/dir" || path.StartsWith("/maps/dir/", StringComparison.Ordinal)) return null;
        return ReadPosition(path, uri.Query);
    }

    private static Uri? ReadMapsUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) return null;
        var text = value.Trim();
        if (text.Any(character => character <= ' ' || character == '\\')
            || !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(text, UriKind.Absolute, out var uri) || !IsMapsUrl(uri)) return null;
        var authority = text[8..].Split('/', '?', '#')[0];
        if (authority.EndsWith(":443", StringComparison.Ordinal)) authority = authority[..^4];
        if (!Hosts.Contains(authority) || InvalidEscape().IsMatch(text)) return null;
        return uri;
    }

    private static Coordinates? ReadPosition(string path, string search)
    {
        var pairs = PlacePair().Matches(path);
        var query = QueryHelpers.ParseQuery(search);
        var values = query.Where(item => item.Key is "q" or "query").SelectMany(item => item.Value).ToArray();
        if (pairs.Count > 1 || PlaceMarker().Count(path) != pairs.Count * 2
            || values.Length > 1 || pairs.Count > 0 && values.Length > 0) return null;
        var match = pairs.Count == 1 ? pairs[0]
            : values.Length == 1 && (!query.ContainsKey("query")
                || query.TryGetValue("api", out var api) && api == "1")
                ? NumericPair().Match(values[0] ?? "") : null;
        return ToCoordinates(match);
    }

    private static bool IsMapsUrl(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 && uri.IsDefaultPort
        && Hosts.Contains(uri.IdnHost)
        && (uri.AbsolutePath == "/maps" || uri.AbsolutePath.StartsWith("/maps/", StringComparison.Ordinal)
            || uri.Host.StartsWith("maps.", StringComparison.Ordinal) && uri.AbsolutePath == "/");

    private static Coordinates? ToCoordinates(Match? match)
    {
        if (match?.Success != true
            || !decimal.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var latitude)
            || !decimal.TryParse(match.Groups[2].Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var longitude)
            || Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180) return null;
        return new Coordinates(latitude, longitude);
    }

    [GeneratedRegex(@"!3d(-?(?:\d+(?:\.\d+)?|\.\d+))!4d(-?(?:\d+(?:\.\d+)?|\.\d+))(?=!|$)", RegexOptions.CultureInvariant)]
    private static partial Regex PlacePair();
    [GeneratedRegex(@"^(-?(?:\d+(?:\.\d+)?|\.\d+)),(-?(?:\d+(?:\.\d+)?|\.\d+))$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericPair();
    [GeneratedRegex(@"![34]d", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceMarker();
    [GeneratedRegex(@"%(?![0-9a-fA-F]{2})", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidEscape();
}
