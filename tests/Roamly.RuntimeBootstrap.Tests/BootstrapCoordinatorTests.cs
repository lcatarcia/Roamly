using Azure.Core;
using Azure.Identity;

namespace Roamly.RuntimeBootstrap.Tests;

public sealed class BootstrapCoordinatorTests
{
    [Fact]
    public async Task Rejecting_admin_secret_consent_does_not_read_secret_or_start_sql_or_mutations()
    {
        var effects = new FakeBootstrapEffects(["ReadOnlyPreflight"]);
        var interaction = new FakeBootstrapInteraction(effects.Events, new Dictionary<string, bool>
        {
            ["READ ADMIN SECRET"] = false
        });

        int exitCode = await CreateCoordinator(interaction, effects).RunAsync();

        Assert.Equal(2, exitCode);
        Assert.Equal(["ReadOnlyPreflight"], effects.Calls);
        Assert.DoesNotContain("ReadAdminSecret", effects.Calls);
        Assert.DoesNotContain("VerifySqlPreflight", effects.Calls);
        Assert.DoesNotContain("ProvisionRuntimeSql", effects.Calls);
        Assert.DoesNotContain("WriteRuntimeSecrets", effects.Calls);
    }

    [Fact]
    public async Task Rejecting_connect_consent_does_not_start_sql_or_mutations()
    {
        var effects = new FakeBootstrapEffects(["ReadOnlyPreflight", "ReadAdminSecret"]);
        var interaction = new FakeBootstrapInteraction(effects.Events, new Dictionary<string, bool>
        {
            ["READ ADMIN SECRET"] = true,
            ["CONNECT"] = false
        });

        int exitCode = await CreateCoordinator(interaction, effects).RunAsync();

        Assert.Equal(2, exitCode);
        Assert.Equal(["ReadOnlyPreflight", "ReadAdminSecret"], effects.Calls);
        Assert.DoesNotContain("VerifySqlPreflight", effects.Calls);
        Assert.DoesNotContain("ProvisionRuntimeSql", effects.Calls);
        Assert.DoesNotContain("WriteRuntimeSecrets", effects.Calls);
    }

    [Fact]
    public async Task Rejecting_sql_consent_does_not_run_the_second_preflight_or_ddl()
    {
        var effects = new FakeBootstrapEffects(
        [
            "ReadOnlyPreflight",
            "ReadAdminSecret",
            "VerifySqlPreflight",
            "PersistRecoveryManifest",
            "DeleteRecoveryManifest"
        ]);
        var interaction = new FakeBootstrapInteraction(
            effects.Events,
            ConfirmAllBut("ACCEPT SQL TELEMETRY RISK"));

        int exitCode = await CreateCoordinator(interaction, effects).RunAsync();

        Assert.Equal(2, exitCode);
        Assert.Single(effects.Calls, call => call == "VerifySqlPreflight");
        Assert.DoesNotContain("ProvisionRuntimeSql", effects.Calls);
        Assert.DoesNotContain("WriteRuntimeSecrets", effects.Calls);
    }

    [Fact]
    public async Task Rejecting_sql_creation_does_not_run_ddl_or_write_secrets()
    {
        var effects = new FakeBootstrapEffects(
        [
            "ReadOnlyPreflight",
            "ReadAdminSecret",
            "VerifySqlPreflight",
            "PersistRecoveryManifest",
            "DeleteRecoveryManifest"
        ]);
        var interaction = new FakeBootstrapInteraction(
            effects.Events,
            new Dictionary<string, bool>
            {
                ["READ ADMIN SECRET"] = true,
                ["CONNECT"] = true,
                ["ACCEPT SQL TELEMETRY RISK"] = true,
                ["CREATE SQL"] = false
            });

        int exitCode = await CreateCoordinator(interaction, effects).RunAsync();

        Assert.Equal(2, exitCode);
        Assert.Single(effects.Calls, call => call == "VerifySqlPreflight");
        Assert.DoesNotContain("ProvisionRuntimeSql", effects.Calls);
        Assert.DoesNotContain("WriteRuntimeSecrets", effects.Calls);
    }

    [Fact]
    public async Task Rejecting_runtime_secret_consent_preserves_incomplete_sql_recovery_manifest()
    {
        var effects = new FakeBootstrapEffects(
        [
            "ReadOnlyPreflight",
            "ReadAdminSecret",
            "VerifySqlPreflight",
            "PersistRecoveryManifest",
            "ProvisionRuntimeSql"
        ]);
        var interaction = new FakeBootstrapInteraction(
            effects.Events,
            ConfirmAllBut("CREATE RUNTIME SECRETS"));

        int exitCode = await CreateCoordinator(interaction, effects).RunAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("ProvisionRuntimeSql", effects.Calls);
        Assert.DoesNotContain("WriteRuntimeSecrets", effects.Calls);
        Assert.DoesNotContain("DeleteRecoveryManifest", effects.Calls);
        Assert.DoesNotContain(effects.Events, item => item.StartsWith("Confirm:CREATE TEMP", StringComparison.Ordinal));
        Assert.DoesNotContain("Confirm:REMOVE OPERATOR KVA", effects.Events);
    }

    [Fact]
    public async Task Valid_bootstrap_writes_both_runtime_secrets_without_temporary_identity_or_role_consents()
    {
        var effects = new FakeBootstrapEffects(
        [
            "ReadOnlyPreflight",
            "ReadAdminSecret",
            "VerifySqlPreflight",
            "PersistRecoveryManifest",
            "ProvisionRuntimeSql",
            "WriteRuntimeSecrets",
            "VerifyRuntimeConfiguration",
            "DeleteRecoveryManifest"
        ]);
        var interaction = new FakeBootstrapInteraction(effects.Events, ConfirmAllBut());

        int exitCode = await CreateCoordinator(interaction, effects).RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal(
            [
                "ReadOnlyPreflight",
                "ReadAdminSecret",
                "VerifySqlPreflight",
                "PersistRecoveryManifest",
                "VerifySqlPreflight",
                "PersistRecoveryManifest",
                "ProvisionRuntimeSql",
                "WriteRuntimeSecrets",
                "WriteRuntimeSecret:roamly-sql-connection",
                "WriteRuntimeSecret:roamly-login-hmac",
                "VerifyRuntimeConfiguration",
                "PersistRecoveryManifest",
                "DeleteRecoveryManifest"
            ],
            effects.Events.Where(item => !item.StartsWith("Confirm:", StringComparison.Ordinal) &&
                item != "PromptRuntimeUser"));
        AssertBefore("Confirm:READ ADMIN SECRET", "ReadAdminSecret");
        AssertBefore("Confirm:CONNECT", "VerifySqlPreflight");
        AssertBefore("Confirm:CREATE SQL", "ProvisionRuntimeSql");
        AssertBefore("Confirm:CREATE RUNTIME SECRETS", "WriteRuntimeSecret:roamly-sql-connection");
        AssertBefore("WriteRuntimeSecret:roamly-sql-connection", "WriteRuntimeSecret:roamly-login-hmac");
        AssertBefore("WriteRuntimeSecret:roamly-login-hmac", "VerifyRuntimeConfiguration");
        Assert.DoesNotContain(effects.Events, item => item.StartsWith("Confirm:CREATE TEMP", StringComparison.Ordinal));
        Assert.DoesNotContain("Confirm:REMOVE OPERATOR KVA", effects.Events);
        Assert.DoesNotContain(effects.Calls, call =>
            call.Contains("Temporary", StringComparison.Ordinal) ||
            call.Contains("OperatorKeyVaultRole", StringComparison.Ordinal));

        void AssertBefore(string first, string second) =>
            Assert.True(
                effects.Events.IndexOf(first) < effects.Events.IndexOf(second),
                $"Expected '{first}' before '{second}'. Actual order: {string.Join(", ", effects.Events)}");
    }

    [Fact]
    public async Task Production_runtime_secret_writer_constructs_SecretClient_with_operator_AzureCliCredential()
    {
        TokenCredential? capturedCredential = null;
        var productionEffects = new Program.ProductionBootstrapEffects((vaultUri, credential) =>
        {
            Assert.Equal("https://kv-test.vault.azure.net/", vaultUri.AbsoluteUri);
            capturedCredential = credential;
            throw new SecretClientFactoryStoppedException();
        });
        var resources = new Program.AzureResources(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "/subscriptions/test/resourceGroups/test/providers/Microsoft.Sql/servers/sql-test",
            "sql-test.database.windows.net",
            "/subscriptions/test/resourceGroups/test/providers/Microsoft.Sql/servers/sql-test/databases/roamly",
            "Online",
            "/subscriptions/test/resourceGroups/test/providers/Microsoft.KeyVault/vaults/kv-test",
            "https://kv-test.vault.azure.net/",
            "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-test",
            "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        await Assert.ThrowsAsync<SecretClientFactoryStoppedException>(() =>
            productionEffects.WriteRuntimeSecretsAsync(
                new Program.BootstrapState(),
                resources,
                "synthetic-connection-string",
                "synthetic-hmac-secret"));

        Assert.IsType<AzureCliCredential>(capturedCredential);
    }

    private static Dictionary<string, bool> ConfirmAllBut(string? refusedToken = null) =>
        new()
        {
            ["READ ADMIN SECRET"] = refusedToken != "READ ADMIN SECRET",
            ["CONNECT"] = refusedToken != "CONNECT",
            ["ACCEPT SQL TELEMETRY RISK"] = refusedToken != "ACCEPT SQL TELEMETRY RISK",
            ["CREATE SQL"] = refusedToken != "CREATE SQL",
            ["CREATE RUNTIME SECRETS"] = refusedToken != "CREATE RUNTIME SECRETS"
        };

    private static Program.BootstrapCoordinator CreateCoordinator(
        FakeBootstrapInteraction interaction,
        FakeBootstrapEffects effects) =>
        new(interaction, effects, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

    private sealed class FakeBootstrapInteraction(
        List<string> events,
        Dictionary<string, bool> confirmations) : Program.IBootstrapInteraction
    {
        public bool ConfirmExact(string expected)
        {
            events.Add($"Confirm:{expected}");
            return confirmations.TryGetValue(expected, out bool confirmed) && confirmed;
        }

        public string PromptRuntimeUser()
        {
            events.Add("PromptRuntimeUser");
            return "roamly_test_runtime";
        }
    }

    private sealed class FakeBootstrapEffects(IEnumerable<string> configuredOperations) : Program.IBootstrapEffects
    {
        private readonly HashSet<string> _configuredOperations = configuredOperations.ToHashSet(StringComparer.Ordinal);

        internal List<string> Events { get; } = [];
        internal List<string> Calls { get; } = [];

        public Task<Program.AzureResources> ReadOnlyPreflightAsync()
        {
            Record("ReadOnlyPreflight");
            return Task.FromResult(new Program.AzureResources(
                "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                "/subscriptions/test/resourceGroups/test/providers/Microsoft.Sql/servers/sql-test",
                "sql-test.database.windows.net",
                "/subscriptions/test/resourceGroups/test/providers/Microsoft.Sql/servers/sql-test/databases/roamly",
                "Online",
                "/subscriptions/test/resourceGroups/test/providers/Microsoft.KeyVault/vaults/kv-test",
                "https://kv-test.vault.azure.net/",
                "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-test",
                "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));
        }

        public Task<string> ReadAdminSecretAsync(Program.AzureResources resources)
        {
            Record("ReadAdminSecret");
            return Task.FromResult("synthetic-admin-password");
        }

        public Task<string> VerifySqlPreflightAsync(
            Program.AzureResources resources,
            string runtimeUser,
            string adminPassword)
        {
            Record("VerifySqlPreflight");
            return Task.FromResult("READ_WRITE");
        }

        public Task PersistRecoveryManifestAsync(Program.BootstrapState state)
        {
            Record("PersistRecoveryManifest");
            state.ManifestVersion = 1;
            return Task.CompletedTask;
        }

        public Task ProvisionRuntimeSqlAsync(
            Program.AzureResources resources,
            string runtimeUser,
            string runtimePassword,
            string adminPassword)
        {
            Record("ProvisionRuntimeSql");
            return Task.CompletedTask;
        }

        public Task WriteRuntimeSecretsAsync(
            Program.BootstrapState state,
            Program.AzureResources resources,
            string runtimeConnectionString,
            string hmacSecret)
        {
            Record("WriteRuntimeSecrets");
            Events.Add("WriteRuntimeSecret:roamly-sql-connection");
            Events.Add("WriteRuntimeSecret:roamly-login-hmac");
            return Task.CompletedTask;
        }

        public Task VerifyRuntimeConfigurationAsync(
            Program.BootstrapState state,
            Program.AzureResources resources,
            string runtimeUser,
            string runtimePassword)
        {
            Record("VerifyRuntimeConfiguration");
            return Task.CompletedTask;
        }

        public Task DeleteRecoveryManifestAsync(string manifestPath)
        {
            Record("DeleteRecoveryManifest");
            return Task.CompletedTask;
        }

        private void Record(string operation)
        {
            if (!_configuredOperations.Contains(operation))
            {
                throw new InvalidOperationException($"Unexpected bootstrap effect: {operation}.");
            }

            Calls.Add(operation);
            Events.Add(operation);
        }
    }

    private sealed class SecretClientFactoryStoppedException : Exception
    {
    }
}
