using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

internal static partial class Program
{
    internal interface IBootstrapInteraction
    {
        bool ConfirmExact(string expected);
        string PromptRuntimeUser();
    }

    internal interface IBootstrapEffects
    {
        Task<AzureResources> ReadOnlyPreflightAsync();
        Task<string> ReadAdminSecretAsync(AzureResources resources);
        Task<string> VerifySqlPreflightAsync(AzureResources resources, string runtimeUser, string adminPassword);
        Task PersistRecoveryManifestAsync(BootstrapState state);
        Task ProvisionRuntimeSqlAsync(AzureResources resources, string runtimeUser, string runtimePassword, string adminPassword);
        Task WriteRuntimeSecretsAsync(BootstrapState state, AzureResources resources, string runtimeConnectionString, string hmacSecret);
        Task VerifyRuntimeConfigurationAsync(BootstrapState state, AzureResources resources, string runtimeUser, string runtimePassword);
        Task DeleteRecoveryManifestAsync(string manifestPath);
    }

    internal sealed class BootstrapCoordinator(
        IBootstrapInteraction interaction,
        IBootstrapEffects effects,
        string manifestPath)
    {
        internal async Task<int> RunAsync()
        {
            var state = new BootstrapState();
            string phase = "preflight read-only";
            string? adminPassword = null;
            string? runtimePassword = null;
            string? runtimeConnectionString = null;
            string? hmacSecret = null;
            int exitCode = 1;

            try
            {
                AzureResources resources = await ReadPreflightAsync(state);
                phase = "lettura secret SQL amministrativo";
                adminPassword = await ReadAdminPasswordAsync(resources);
                string runtimeUser = interaction.PromptRuntimeUser();
                phase = "SQL preflight";
                string queryStoreState = await ConnectAndVerifySqlAsync(resources, runtimeUser, adminPassword);

                await effects.PersistRecoveryManifestAsync(state);
                phase = "SQL provisioning";
                queryStoreState = await ConfirmAndPrepareSqlAsync(resources, runtimeUser, adminPassword);
                runtimePassword = CreateRandomSecret(48);
                hmacSecret = CreateRandomSecret(32);
                runtimeConnectionString = BuildConnectionString(
                    resources.SqlFqdn,
                    DatabaseName,
                    runtimeUser,
                    runtimePassword);
                state.SqlTouched = true;
                await effects.PersistRecoveryManifestAsync(state);
                await effects.ProvisionRuntimeSqlAsync(resources, runtimeUser, runtimePassword, adminPassword);
                Console.WriteLine($"Provisioning SQL completato. Query Store actual state: {queryStoreState}.");

                phase = "creazione secret runtime";
                await CreateRuntimeSecretsAsync(state, resources, runtimeConnectionString, hmacSecret);

                phase = "verifica SQL e metadata secret";
                PrintStage(4, "Verifica SQL e metadata secret, mai i valori");
                await effects.VerifyRuntimeConfigurationAsync(state, resources, runtimeUser, runtimePassword);
                Console.WriteLine($"Metadata confermati per {SqlConnectionSecretName} e {LoginHmacSecretName}; nessun valore e' stato letto.");
                Console.WriteLine("Non eseguire migration o modifiche firewall da questo helper.");
                state.BootstrapCompleted = true;
                await effects.PersistRecoveryManifestAsync(state);
                exitCode = 0;
            }
            catch (BootstrapException exception)
            {
                Console.Error.WriteLine(exception.SafeMessage);
            }
            catch (BootstrapCancelledException)
            {
                exitCode = 2;
            }
            catch
            {
                Console.Error.WriteLine($"Operazione non completata nella fase '{phase}'. Dettagli ed eccezioni sono stati soppressi.");
            }
            finally
            {
                exitCode = await CleanupAsync(state, exitCode);
                adminPassword = null;
                runtimePassword = null;
                runtimeConnectionString = null;
                hmacSecret = null;
            }

            if (state.SqlTouched && !state.BootstrapCompleted)
            {
                Console.Error.WriteLine("Lo stato SQL/Key Vault puo' essere parziale: non rilanciare il helper; riconciliare SQL e secret separatamente.");
            }

            return exitCode;
        }

        private async Task<AzureResources> ReadPreflightAsync(BootstrapState state)
        {
            PrintStage(1, "Preflight read-only: Azure, RBAC, metadata e SQL 14/14");
            AzureResources resources = await effects.ReadOnlyPreflightAsync();
            state.Resources = resources;
            PrintResource("Sottoscrizione", SubscriptionId);
            PrintResource("Resource group", ResourceGroup);
            PrintResource("SQL server", resources.SqlServerId);
            PrintResource("SQL endpoint", resources.SqlFqdn);
            PrintResource("Database", $"{resources.DatabaseId} ({resources.DatabaseStatus})");
            PrintResource("Key Vault", resources.VaultId);
            PrintResource("Managed identity runtime", $"{resources.IdentityId} ({resources.IdentityPrincipalId})");
            if (string.Equals(resources.DatabaseStatus, "Paused", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Il database e' in pausa: una connessione puo' riprenderlo e generare costi.");
            }

            Console.WriteLine("La password SQL amministrativa verra' letta dal secret sql-admin-password solo dopo conferma; il valore restera' in memoria.");
            return resources;
        }

        private async Task<string> ReadAdminPasswordAsync(AzureResources resources)
        {
            if (!interaction.ConfirmExact("READ ADMIN SECRET"))
            {
                Console.WriteLine("Annullato prima di leggere il secret SQL amministrativo.");
                throw new BootstrapCancelledException();
            }

            string adminPassword = await effects.ReadAdminSecretAsync(resources);
            if (string.IsNullOrEmpty(adminPassword))
            {
                throw new BootstrapException("Metadata o valore sql-admin-password non validi; nessuna modifica e' stata avviata.");
            }

            return adminPassword;
        }

        private async Task<string> ConnectAndVerifySqlAsync(
            AzureResources resources,
            string runtimeUser,
            string adminPassword)
        {
            if (!interaction.ConfirmExact("CONNECT"))
            {
                Console.WriteLine("Annullato prima di aprire la connessione SQL.");
                throw new BootstrapCancelledException();
            }

            string queryStoreState = await effects.VerifySqlPreflightAsync(resources, runtimeUser, adminPassword);
            Console.WriteLine("Preflight SQL superato: schema e cronologia migration sono 14/14; ruolo e principal non esistono.");
            Console.WriteLine($"Query Store actual state: {queryStoreState}.");
            Console.WriteLine("I due secret runtime sono assenti; sql-admin-password e' stato letto solo in memoria.");
            return queryStoreState;
        }

        private async Task<string> ConfirmAndPrepareSqlAsync(
            AzureResources resources,
            string runtimeUser,
            string adminPassword)
        {
            PrintStage(2, "Preflight SQL ripetuto e provisioning SQL esplicito");
            Console.WriteLine("La password viene inclusa nel DDL inviato al server; la telemetria della piattaforma potrebbe acquisirla.");
            Console.WriteLine("Dopo una scrittura SQL non vengono eseguiti rollback o rotazioni automatiche: in errore serve riconciliazione.");
            if (!interaction.ConfirmExact("ACCEPT SQL TELEMETRY RISK") ||
                !interaction.ConfirmExact("CREATE SQL"))
            {
                Console.WriteLine("Annullato prima delle scritture SQL.");
                throw new BootstrapCancelledException();
            }

            return await effects.VerifySqlPreflightAsync(resources, runtimeUser, adminPassword);
        }

        private async Task CreateRuntimeSecretsAsync(
            BootstrapState state,
            AzureResources resources,
            string runtimeConnectionString,
            string hmacSecret)
        {
            PrintStage(3, "Conferma e scrittura dei due secret con l'identita' Azure dell'operatore");
            Console.WriteLine("Verranno creati solo i due secret; nessun valore sara' letto, mostrato o salvato localmente.");
            Console.WriteLine("Prima di confermare, verificare che nessun altro operatore o processo possa modificare i secret durante questa operazione.");
            if (!interaction.ConfirmExact("CREATE RUNTIME SECRETS"))
            {
                throw new BootstrapException("Conferma secret rifiutata dopo il provisioning SQL; fermarsi per riconciliazione controllata.");
            }

            await effects.WriteRuntimeSecretsAsync(state, resources, runtimeConnectionString, hmacSecret);
        }

        private async Task<int> CleanupAsync(BootstrapState state, int exitCode)
        {
            if (state.ManifestVersion == 0)
            {
                return exitCode;
            }

            if (state.SqlTouched && !state.BootstrapCompleted)
            {
                Console.Error.WriteLine("Manifest recovery mantenuto: SQL o secret runtime potrebbero essere parziali.");
                return 1;
            }

            try
            {
                await effects.DeleteRecoveryManifestAsync(manifestPath);
                return exitCode;
            }
            catch
            {
                Console.Error.WriteLine($"Impossibile rimuovere il manifest recovery; verificare {manifestPath}.");
                return 1;
            }
        }
    }

    private sealed class ConsoleBootstrapInteraction : IBootstrapInteraction
    {
        public bool ConfirmExact(string expected) => Program.ConfirmExact(expected);
        public string PromptRuntimeUser() => Program.PromptRuntimeUser();
    }

    internal sealed class ProductionBootstrapEffects : IBootstrapEffects
    {
        private readonly Func<Uri, TokenCredential, SecretClient> _secretClientFactory;

        internal ProductionBootstrapEffects(
            Func<Uri, TokenCredential, SecretClient>? secretClientFactory = null)
        {
            _secretClientFactory = secretClientFactory ??
                ((vaultUri, credential) => new SecretClient(vaultUri, credential));
        }

        public async Task<AzureResources> ReadOnlyPreflightAsync()
        {
            AzureResources resources = await ReadAzureResourcesAsync();
            SecretClient client = CreateOperatorSecretClient(resources);
            await EnsureRuntimeSecretsAbsentAsync(client);
            return resources;
        }

        public async Task<string> ReadAdminSecretAsync(AzureResources resources)
        {
            SecretClient client = CreateOperatorSecretClient(resources);
            KeyVaultSecret secret = (await client.GetSecretAsync(AdminPasswordSecretName)).Value;
            return secret.Value;
        }

        public Task<string> VerifySqlPreflightAsync(
            AzureResources resources,
            string runtimeUser,
            string adminPassword) =>
            Program.VerifySqlPreflightAsync(resources, runtimeUser, adminPassword);

        public Task PersistRecoveryManifestAsync(BootstrapState state)
        {
            PersistRecoveryManifest(state);
            return Task.CompletedTask;
        }

        public Task ProvisionRuntimeSqlAsync(
            AzureResources resources,
            string runtimeUser,
            string runtimePassword,
            string adminPassword) =>
            Program.ProvisionRuntimeSqlAsync(resources, runtimeUser, runtimePassword, adminPassword);

        public async Task WriteRuntimeSecretsAsync(
            BootstrapState state,
            AzureResources resources,
            string runtimeConnectionString,
            string hmacSecret)
        {
            SecretClient secretClient = CreateOperatorSecretClient(resources);
            await EnsureRuntimeSecretsAbsentAsync(secretClient);
            state.RuntimeSecretsWriteStarted = true;
            PersistRecoveryManifest(state);
            await secretClient.SetSecretAsync(new KeyVaultSecret(SqlConnectionSecretName, runtimeConnectionString));
            state.SqlConnectionSecretWritten = true;
            PersistRecoveryManifest(state);
            await EnsureRuntimeSecretAbsentAsync(secretClient, LoginHmacSecretName);
            await secretClient.SetSecretAsync(new KeyVaultSecret(LoginHmacSecretName, hmacSecret));
            state.LoginHmacSecretWritten = true;
            PersistRecoveryManifest(state);
        }

        public async Task VerifyRuntimeConfigurationAsync(
            BootstrapState state,
            AzureResources resources,
            string runtimeUser,
            string runtimePassword)
        {
            await Program.VerifyRuntimeSqlAsync(resources, runtimeUser, runtimePassword);
            SecretClient secretClient = CreateOperatorSecretClient(resources);
            HashSet<string> existing = await FindExistingRuntimeSecretsAsync(secretClient);
            if (!existing.Contains(SqlConnectionSecretName) ||
                !existing.Contains(LoginHmacSecretName))
            {
                throw new BootstrapException("La verifica metadata dei due secret e' incompleta; fermarsi per riconciliazione.");
            }
        }

        public Task DeleteRecoveryManifestAsync(string manifestPath)
        {
            DeleteRecoveryManifest(manifestPath);
            return Task.CompletedTask;
        }

        private SecretClient CreateOperatorSecretClient(AzureResources resources) =>
            _secretClientFactory(new Uri(resources.VaultUri), new AzureCliCredential());
    }
}
