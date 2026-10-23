namespace Roamly.Api.Common.Identity;

/// <summary>Input/output interattivo ristretto al job locale di provisioning.</summary>
public interface IProvisioningConsole
{
    bool IsInteractive { get; }

    string? ReadEmail();

    string ReadSecret(string prompt);

    void WriteLine(string message);
}
