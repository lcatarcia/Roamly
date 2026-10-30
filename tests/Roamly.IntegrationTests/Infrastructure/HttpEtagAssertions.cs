using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Roamly.IntegrationTests.Infrastructure;

internal static class HttpEtagAssertions
{
    public static string StrongRowVersion(string? etag)
    {
        var value = Assert.IsType<string>(etag);
        Assert.StartsWith("\"", value, StringComparison.Ordinal);
        Assert.EndsWith("\"", value, StringComparison.Ordinal);
        Assert.False(value.StartsWith("W/", StringComparison.Ordinal));
        Assert.Equal(8, WebEncoders.Base64UrlDecode(value[1..^1]).Length);
        return value;
    }

    public static string AsWeak(string strongEtag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strongEtag);
        return $"W/{strongEtag}";
    }
}
