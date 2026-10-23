namespace Roamly.Api.Common.Identity;

/// <summary>Marca interna assegnata solo dal job locale di provisioning.</summary>
internal static class ProvisionedAccountClaims
{
    public const string UnconfirmedLogin = "urn:roamly:identity:provisioned-unconfirmed-login";
}
