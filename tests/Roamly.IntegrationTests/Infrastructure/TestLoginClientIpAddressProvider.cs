using System.Net;
using Microsoft.AspNetCore.Http;
using Roamly.Api.Common.Security;

namespace Roamly.IntegrationTests.Infrastructure;

/// <summary>IP controllabile dai test HTTP, senza fidarsi di header inviati dal client.</summary>
public sealed class TestLoginClientIpAddressProvider(IPAddress address) : ILoginClientIpAddressProvider
{
    /// <inheritdoc />
    public IPAddress? GetClientIpAddress(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return address;
    }
}
