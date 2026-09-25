namespace Roamly.Model.Tests;

/// <summary>
/// Verifica che l'impianto di test sia effettivamente in esecuzione: xUnit v3
/// su Microsoft.Testing.Platform, senza alcuna dipendenza da un database.
/// </summary>
public sealed class ToolchainSmokeTests
{
    [Fact]
    public void Il_runner_esegue_i_test()
    {
        Assert.True(true);
    }
}
