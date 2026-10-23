using System.Text;

namespace Roamly.Api.Common.Identity;

/// <summary>Console interattiva: i segreti vengono letti senza eco.</summary>
public sealed class ProvisioningConsole : IProvisioningConsole
{
    public bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public string? ReadEmail() => Console.ReadLine();

    public string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        var secret = new StringBuilder();
        ReadSecretCharacters(secret);
        Console.WriteLine();
        return secret.ToString();
    }

    private static void ReadSecretCharacters(StringBuilder secret)
    {
        while (TryReadSecretCharacter(secret))
        {
        }
    }

    private static bool TryReadSecretCharacter(StringBuilder secret)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            return false;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            RemoveLastCharacter(secret);
        }
        else if (!char.IsControl(key.KeyChar))
        {
            secret.Append(key.KeyChar);
        }

        return true;
    }

    private static void RemoveLastCharacter(StringBuilder secret)
    {
        if (secret.Length > 0)
        {
            secret.Length--;
        }
    }

    public void WriteLine(string message) => Console.WriteLine(message);
}
