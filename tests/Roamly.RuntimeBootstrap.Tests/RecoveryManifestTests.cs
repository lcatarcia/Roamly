using System.Text.Json;

namespace Roamly.RuntimeBootstrap.Tests;

public sealed class RecoveryManifestTests
{
    private const string SubscriptionId = "5b24089b-bf06-44ab-9486-c4d9cd326d80";
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task Recovery_without_manifest_does_not_need_azure_or_sql()
    {
        using var fixture = new RecoveryFixture();

        int exitCode = await Program.RecoverAsyncForTests(fixture.LocalAppData);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(fixture.ManifestPath));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Recovery_keeps_manifest_for_incomplete_sql_or_secret_state(
        bool runtimeSecretWriteStarted,
        bool sqlConnectionSecretWritten)
    {
        using var fixture = new RecoveryFixture();
        fixture.WriteManifest(new Program.BootstrapState
        {
            ManifestVersion = 1,
            SubscriptionId = SubscriptionId,
            Resources = CreateResources(),
            SqlTouched = true,
            RuntimeSecretsWriteStarted = runtimeSecretWriteStarted,
            SqlConnectionSecretWritten = sqlConnectionSecretWritten
        });

        int exitCode = await Program.RecoverAsyncForTests(fixture.LocalAppData);

        Assert.Equal(1, exitCode);
        Assert.True(File.Exists(fixture.ManifestPath));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(fixture.ManifestPath));
        JsonElement state = manifest.RootElement;
        Assert.True(state.GetProperty("sqlTouched").GetBoolean());
        Assert.Equal(runtimeSecretWriteStarted, state.GetProperty("runtimeSecretsWriteStarted").GetBoolean());
        Assert.Equal(sqlConnectionSecretWritten, state.GetProperty("sqlConnectionSecretWritten").GetBoolean());
        Assert.False(state.GetProperty("loginHmacSecretWritten").GetBoolean());
        Assert.DoesNotContain("secret-value", File.ReadAllText(fixture.ManifestPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Recovery_removes_manifest_when_no_sql_or_secret_changes_were_started()
    {
        using var fixture = new RecoveryFixture();
        fixture.WriteManifest(new Program.BootstrapState
        {
            ManifestVersion = 1,
            SubscriptionId = SubscriptionId,
            Resources = CreateResources()
        });

        int exitCode = await Program.RecoverAsyncForTests(fixture.LocalAppData);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(fixture.ManifestPath));
    }

    [Fact]
    public async Task Recovery_removes_manifest_for_a_fully_completed_bootstrap()
    {
        using var fixture = new RecoveryFixture();
        fixture.WriteManifest(new Program.BootstrapState
        {
            ManifestVersion = 1,
            SubscriptionId = SubscriptionId,
            Resources = CreateResources(),
            SqlTouched = true,
            RuntimeSecretsWriteStarted = true,
            SqlConnectionSecretWritten = true,
            LoginHmacSecretWritten = true,
            BootstrapCompleted = true
        });

        int exitCode = await Program.RecoverAsyncForTests(fixture.LocalAppData);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(fixture.ManifestPath));
    }

    [Fact]
    public async Task Recovery_preserves_legacy_manifest_that_may_contain_temporary_azure_resources()
    {
        using var fixture = new RecoveryFixture();
        fixture.WriteManifest(new
        {
            manifestVersion = 1,
            subscriptionId = SubscriptionId,
            resources = JsonSerializer.SerializeToElement(
                CreateResources(),
                ManifestJsonOptions),
            applicationId = "22222222-2222-2222-2222-222222222222"
        });

        int exitCode = await Program.RecoverAsyncForTests(fixture.LocalAppData);

        Assert.Equal(1, exitCode);
        Assert.True(File.Exists(fixture.ManifestPath));
    }

    private static Program.AzureResources CreateResources() =>
        new(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            $"/subscriptions/{SubscriptionId}/resourceGroups/rg-roamly-dev/providers/Microsoft.Sql/servers/sql-roamly-prod-wse7wit7fzpja",
            "sql-roamly-prod-wse7wit7fzpja.database.windows.net",
            $"/subscriptions/{SubscriptionId}/resourceGroups/rg-roamly-dev/providers/Microsoft.Sql/servers/sql-roamly-prod-wse7wit7fzpja/databases/roamly",
            "Online",
            $"/subscriptions/{SubscriptionId}/resourceGroups/rg-roamly-dev/providers/Microsoft.KeyVault/vaults/kv-roamlywse7wit7fzpja",
            "https://kv-roamlywse7wit7fzpja.vault.azure.net/",
            $"/subscriptions/{SubscriptionId}/resourceGroups/rg-roamly-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-roamly-runtime-prod",
            "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private sealed class RecoveryFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "Roamly.RuntimeBootstrap.Tests",
            Guid.NewGuid().ToString("N"));

        internal string LocalAppData => Path.Combine(_root, "localappdata");
        internal string ManifestPath => Path.Combine(LocalAppData, "Roamly", "RuntimeBootstrap", "pending-recovery.json");

        internal void WriteManifest<T>(T state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
            File.WriteAllText(
                ManifestPath,
                JsonSerializer.Serialize(state, ManifestJsonOptions));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
