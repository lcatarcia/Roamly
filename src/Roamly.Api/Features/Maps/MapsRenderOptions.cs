namespace Roamly.Api.Features.Maps;

/// <summary>Rendering personale, senza Search o fallback a chiavi permanenti.</summary>
public sealed class MapsRenderOptions
{
    public bool Enabled { get; set; }
    public string? AccountClientId { get; set; }
    public string? ManagedIdentityClientId { get; set; }

    public bool IsConfigured => Enabled && Guid.TryParse(AccountClientId, out var account) && account != Guid.Empty
        && (string.IsNullOrWhiteSpace(ManagedIdentityClientId)
            || Guid.TryParse(ManagedIdentityClientId, out var identity) && identity != Guid.Empty);
}
