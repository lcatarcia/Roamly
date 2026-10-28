using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Data.SqlClient;

internal static partial class Program
{
    private const string SubscriptionId = "5b24089b-bf06-44ab-9486-c4d9cd326d80";
    private const string ResourceGroup = "rg-roamly-dev";
    private const string SqlServerName = "sql-roamly-prod-wse7wit7fzpja";
    private const string DatabaseName = "roamly";
    private const string VaultName = "kv-roamlywse7wit7fzpja";
    private const string RuntimeIdentityName = "id-roamly-runtime-prod";
    private const string AdminLogin = "roamly_bootstrap_admin";
    private const string RuntimeRole = "RoamlyRuntime";
    private const string OperatorKeyVaultRole = "Key Vault Administrator";
    private const string AdminPasswordSecretName = "sql-admin-password";
    private const string SqlConnectionSecretName = "roamly-sql-connection";
    private const string LoginHmacSecretName = "roamly-login-hmac";

    private static readonly string[] ExpectedTables =
    [
        "AspNetUsers",
        "AspNetRoles",
        "AspNetRoleClaims",
        "AspNetUserClaims",
        "AspNetUserLogins",
        "AspNetUserRoles",
        "AspNetUserTokens",
        "Campers",
        "Equipment",
        "MaintenanceItems",
        "MaintenanceLogs",
        "OdometerReadings",
        "SavedPlaces",
        "Trips",
        "TripStops",
        "Checklists",
        "ChecklistItems",
        "JournalEntries",
        "Expenses",
        "JournalEntryAttachments",
        "JournalAttachmentDeletionOutbox",
        "DataProtectionKeys",
        "LoginAttemptCounters",
        "ErasureReceipts"
    ];

    private static readonly JsonSerializerOptions RecoveryManifestJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly TableGrant[] RuntimeGrants =
    [
        new("Campers", "SELECT, INSERT, UPDATE, DELETE"),
        new("Equipment", "SELECT, INSERT, UPDATE, DELETE"),
        new("MaintenanceItems", "SELECT, INSERT, UPDATE, DELETE"),
        new("MaintenanceLogs", "SELECT, INSERT, UPDATE, DELETE"),
        new("OdometerReadings", "SELECT, INSERT, UPDATE, DELETE"),
        new("SavedPlaces", "SELECT, INSERT, UPDATE, DELETE"),
        new("Trips", "SELECT, INSERT, UPDATE, DELETE"),
        new("TripStops", "SELECT, INSERT, UPDATE, DELETE"),
        new("Checklists", "SELECT, INSERT, UPDATE, DELETE"),
        new("ChecklistItems", "SELECT, INSERT, UPDATE, DELETE"),
        new("Expenses", "SELECT, INSERT, UPDATE, DELETE"),
        new("JournalEntries", "SELECT, INSERT, UPDATE, DELETE"),
        new("JournalEntryAttachments", "SELECT, INSERT, UPDATE, DELETE"),
        new("JournalAttachmentDeletionOutbox", "SELECT, INSERT, UPDATE, DELETE"),
        new("AspNetUsers", "SELECT, INSERT, UPDATE, DELETE"),
        new("AspNetUserClaims", "SELECT, INSERT"),
        new("AspNetRoles", "SELECT"),
        new("AspNetUserRoles", "SELECT"),
        new("AspNetRoleClaims", "SELECT"),
        new("DataProtectionKeys", "SELECT, INSERT"),
        new("LoginAttemptCounters", "SELECT, INSERT, UPDATE, DELETE"),
        new("ErasureReceipts", "SELECT, INSERT")
    ];

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "--recover", StringComparison.Ordinal))
        {
            return await RecoverAsync();
        }

        if (args.Length != 0)
        {
            Console.Error.WriteLine("Argomenti non supportati.");
            return 2;
        }

        string? manifestPath = null;
        FileStream? bootstrapLock = null;
        try
        {
            manifestPath = GetRecoveryManifestPath();
            bootstrapLock = AcquireBootstrapLock(manifestPath);
            if (File.Exists(manifestPath))
            {
                throw new BootstrapException($"Recovery pendente: eseguire {Path.Combine(Path.GetDirectoryName(manifestPath)!, "Recover-RoamlyRuntimeBootstrap.ps1")} prima di un nuovo bootstrap.");
            }

            return await new BootstrapCoordinator(
                new ConsoleBootstrapInteraction(),
                new ProductionBootstrapEffects(),
                manifestPath).RunAsync();
        }
        catch (BootstrapException exception)
        {
            Console.Error.WriteLine(exception.SafeMessage);
            return 1;
        }
        catch (BootstrapCancelledException)
        {
            return 2;
        }
        catch
        {
            Console.Error.WriteLine("Operazione bootstrap non completata prima dell'avvio del coordinatore.");
            return 1;
        }
        finally
        {
            bootstrapLock?.Dispose();
        }
    }

    private static async Task<int> RecoverAsync()
    {
        return await RecoverAsyncCore(null);
    }

    internal static Task<int> RecoverAsyncForTests(string localAppData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localAppData);
        return RecoverAsyncCore(localAppData);
    }

    private static async Task<int> RecoverAsyncCore(string? localAppDataOverrideForTests)
    {
        string manifestPath;
        try
        {
            manifestPath = GetRecoveryManifestPath(localAppDataOverrideForTests);
        }
        catch (BootstrapException exception)
        {
            Console.Error.WriteLine(exception.SafeMessage);
            return 2;
        }

        FileStream? recoveryLock = null;
        try
        {
            recoveryLock = AcquireBootstrapLock(manifestPath);
            if (!File.Exists(manifestPath))
            {
                Console.WriteLine("Nessun recovery bootstrap pendente.");
                return 0;
            }

            string manifestJson = await File.ReadAllTextAsync(manifestPath);
            BootstrapState state = JsonSerializer.Deserialize<BootstrapState>(
                manifestJson,
                RecoveryManifestJsonOptions)
                ?? throw new BootstrapException("Manifest recovery non valido.");
            RejectLegacyTemporaryState(manifestJson);
            ValidateRecoveryManifest(state);
            if (state.SqlTouched && !state.BootstrapCompleted)
            {
                Console.Error.WriteLine("Recovery mantenuto: SQL o secret runtime potrebbero essere parziali.");
                Console.Error.WriteLine($"Riconciliare SQL e i secret senza rilanciare il bootstrap. Manifest: {manifestPath}");
                return 1;
            }

            DeleteRecoveryManifest(manifestPath);
            Console.WriteLine("Recovery locale completato; manifest non più necessario rimosso. Nessuna operazione Azure o SQL eseguita.");
            return 0;
        }
        catch (BootstrapException exception)
        {
            Console.Error.WriteLine(exception.SafeMessage);
            Console.Error.WriteLine($"Il manifest e' stato mantenuto per un nuovo tentativo: {manifestPath}");
            return 1;
        }
        catch
        {
            Console.Error.WriteLine("Recovery non completato; dettagli ed eccezioni sono stati soppressi.");
            Console.Error.WriteLine($"Il manifest e' stato mantenuto per un nuovo tentativo: {manifestPath}");
            return 1;
        }
        finally
        {
            recoveryLock?.Dispose();
        }
    }

    private static void ValidateRecoveryManifest(BootstrapState state)
    {
        AzureResources resources = state.Resources
            ?? throw new BootstrapException("Manifest recovery privo del contesto risorse necessario.");
        if (state.ManifestVersion != 1 ||
            !string.Equals(state.SubscriptionId, SubscriptionId, StringComparison.OrdinalIgnoreCase))
        {
            throw new BootstrapException("Versione o subscription del manifest recovery non corrispondente.");
        }

        EnsureResourceId(resources.SqlServerId, $"providers/Microsoft.Sql/servers/{SqlServerName}");
        EnsureResourceId(resources.DatabaseId, $"providers/Microsoft.Sql/servers/{SqlServerName}/databases/{DatabaseName}");
        EnsureResourceId(resources.VaultId, $"providers/Microsoft.KeyVault/vaults/{VaultName}");
        EnsureResourceId(
            resources.IdentityId,
            $"providers/Microsoft.ManagedIdentity/userAssignedIdentities/{RuntimeIdentityName}");
        if (!Guid.TryParse(resources.IdentityPrincipalId, out _) ||
            !Uri.TryCreate(resources.VaultUri, UriKind.Absolute, out Uri? vaultUri) ||
            vaultUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new BootstrapException("Contesto risorse del manifest recovery non valido.");
        }

        if (string.IsNullOrWhiteSpace(resources.SqlFqdn))
        {
            throw new BootstrapException("Metadati risorse del manifest recovery non validi.");
        }

        if (state.RuntimeSecretsWriteStarted && !state.SqlTouched ||
            state.SqlConnectionSecretWritten && !state.RuntimeSecretsWriteStarted ||
            state.LoginHmacSecretWritten && !state.RuntimeSecretsWriteStarted ||
            state.LoginHmacSecretWritten && !state.SqlConnectionSecretWritten ||
            (state.SqlConnectionSecretWritten || state.LoginHmacSecretWritten) && !state.SqlTouched ||
            state.BootstrapCompleted && (!state.SqlTouched || !state.SqlConnectionSecretWritten || !state.LoginHmacSecretWritten))
        {
            throw new BootstrapException("Stato SQL/secret del manifest recovery incoerente.");
        }
    }

    private static void RejectLegacyTemporaryState(string manifestJson)
    {
        using JsonDocument manifest = JsonDocument.Parse(manifestJson);
        string[] legacyProperties =
        {
            "applicationDisplayName",
            "applicationId",
            "applicationObjectId",
            "servicePrincipalObjectId",
            "credentialKeyId",
            "roleName",
            "roleDefinitionId",
            "roleAssignmentIds",
            "operatorRoleRestoreRequired"
        };
        if (manifest.RootElement.ValueKind == JsonValueKind.Object &&
            legacyProperties.Any(property => manifest.RootElement.TryGetProperty(property, out _)))
        {
            throw new BootstrapException("Manifest legacy con risorse temporanee: recovery automatico non disponibile; conservarlo e riconciliare manualmente.");
        }
    }

    private static string GetRecoveryManifestPath(string? localAppDataOverrideForTests = null)
    {
        string localAppData = localAppDataOverrideForTests
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new BootstrapException("Local AppData non disponibile; manifest recovery non sicuro.");
        }

        return Path.Combine(localAppData, "Roamly", "RuntimeBootstrap", "pending-recovery.json");
    }

    private static FileStream AcquireBootstrapLock(string manifestPath)
    {
        string directory = Path.GetDirectoryName(manifestPath)!;
        Directory.CreateDirectory(directory);
        string lockPath = Path.Combine(directory, "bootstrap.lock");
        try
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception) when ((exception.HResult & 0xFFFF) is 32 or 33)
        {
            throw new BootstrapException("Un'altra istanza del bootstrap/recovery e' attiva; attendere prima di riprovare.");
        }
    }

    private static void PersistRecoveryManifest(BootstrapState state)
    {
        string manifestPath = GetRecoveryManifestPath();
        string directory = Path.GetDirectoryName(manifestPath)!;
        Directory.CreateDirectory(directory);
        state.ManifestVersion = 1;
        state.SubscriptionId = SubscriptionId;
        string temporaryPath = $"{manifestPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(
                    stream,
                    state,
                    RecoveryManifestJsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void DeleteRecoveryManifest(string? manifestPath)
    {
        if (manifestPath is not null)
        {
            File.Delete(manifestPath);
        }
    }

    private static async Task<AzureResources> ReadAzureResourcesAsync()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            throw new BootstrapException("Eseguire il bridge da un terminale interattivo; nessuna operazione e' stata avviata.");
        }

        string accountId = await RunAzureCliAsync("account", "show", "--query", "id", "--output", "tsv");
        string accountName = await RunAzureCliAsync("account", "show", "--query", "user.name", "--output", "tsv");
        string tenantId = await RunAzureCliAsync("account", "show", "--query", "tenantId", "--output", "tsv");
        if (!string.Equals(accountId, SubscriptionId, StringComparison.OrdinalIgnoreCase))
        {
            throw new BootstrapException("La subscription Azure attiva non corrisponde a quella Roamly attesa.");
        }

        if (!string.Equals(accountName, "lorenzo.catarcia@outlook.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new BootstrapException("L'account Azure CLI non e' l'account operatore atteso; nessuna operazione e' stata avviata.");
        }

        if (!Guid.TryParse(tenantId, out _))
        {
            throw new BootstrapException("Tenant Azure non verificabile; nessuna operazione e' stata avviata.");
        }

        using JsonDocument server = await RunAzureJsonAsync(
            "sql", "server", "show",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroup,
            "--name", SqlServerName,
            "--query", "{id:id,fqdn:fullyQualifiedDomainName}",
            "--output", "json");
        string sqlServerId = GetRequiredString(server.RootElement, "id");
        string sqlFqdn = GetRequiredString(server.RootElement, "fqdn");
        EnsureResourceId(sqlServerId, $"providers/Microsoft.Sql/servers/{SqlServerName}");

        using JsonDocument database = await RunAzureJsonAsync(
            "sql", "db", "show",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroup,
            "--server", SqlServerName,
            "--name", DatabaseName,
            "--query", "{id:id,status:status}",
            "--output", "json");
        string databaseId = GetRequiredString(database.RootElement, "id");
        string databaseStatus = GetRequiredString(database.RootElement, "status");
        EnsureResourceId(databaseId, $"providers/Microsoft.Sql/servers/{SqlServerName}/databases/{DatabaseName}");

        using JsonDocument vault = await RunAzureJsonAsync(
            "keyvault", "show",
            "--subscription", SubscriptionId,
            "--name", VaultName,
            "--query", "{id:id,vaultUri:properties.vaultUri}",
            "--output", "json");
        string vaultId = GetRequiredString(vault.RootElement, "id");
        string vaultUri = GetRequiredString(vault.RootElement, "vaultUri");
        EnsureResourceId(vaultId, $"providers/Microsoft.KeyVault/vaults/{VaultName}");
        await VerifyOperatorKeyVaultRoleAsync(vaultId);
        if (!Uri.TryCreate(vaultUri, UriKind.Absolute, out Uri? parsedVaultUri) ||
            parsedVaultUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new BootstrapException("L'URI Key Vault non e' valida o non usa HTTPS.");
        }

        using JsonDocument identity = await RunAzureJsonAsync(
            "identity", "show",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroup,
            "--name", RuntimeIdentityName,
            "--query", "{id:id,principalId:principalId}",
            "--output", "json");
        string identityId = GetRequiredString(identity.RootElement, "id");
        string identityPrincipalId = GetRequiredString(identity.RootElement, "principalId");
        EnsureResourceId(identityId, $"providers/Microsoft.ManagedIdentity/userAssignedIdentities/{RuntimeIdentityName}");

        await VerifySqlAuditingAndDiagnosticsAsync(sqlServerId, databaseId);

        return new AzureResources(
            tenantId,
            sqlServerId,
            sqlFqdn,
            databaseId,
            databaseStatus,
            vaultId,
            vaultUri,
            identityId,
            identityPrincipalId);
    }

    private static async Task VerifyOperatorKeyVaultRoleAsync(string vaultId)
    {
        string operatorObjectId = await RunAzureCliAsync(
            "ad", "signed-in-user", "show",
            "--query", "id",
            "--output", "tsv");
        if (!Guid.TryParse(operatorObjectId, out _))
        {
            throw new BootstrapException("Identita' operatore non verificabile; nessuna scrittura verra' eseguita.");
        }

        using JsonDocument assignments = await RunAzureJsonAsync(
            "role", "assignment", "list",
            "--subscription", SubscriptionId,
            "--scope", vaultId,
            "--include-inherited",
            "--query", $"[?principalId=='{operatorObjectId}' && roleDefinitionName=='{OperatorKeyVaultRole}'].{{id:id,scope:scope,role:roleDefinitionName,roleDefinitionId:roleDefinitionId}}",
            "--output", "json");

        JsonElement[] matchingAssignments = assignments.RootElement.ValueKind == JsonValueKind.Array
            ? assignments.RootElement.EnumerateArray().Where(assignment =>
                assignment.TryGetProperty("scope", out JsonElement scope) &&
                string.Equals(scope.GetString(), vaultId, StringComparison.OrdinalIgnoreCase) &&
                assignment.TryGetProperty("role", out JsonElement role) &&
                string.Equals(role.GetString(), OperatorKeyVaultRole, StringComparison.OrdinalIgnoreCase)).ToArray()
            : [];
        if (matchingAssignments.Length != 1)
        {
            throw new BootstrapException("Assegnazione diretta Key Vault Administrator non univoca o non verificata; nessuna modifica verra' eseguita.");
        }

        JsonElement assignment = matchingAssignments[0];
        _ = GetRequiredString(assignment, "id");
        _ = GetRequiredString(assignment, "roleDefinitionId");
    }

    private static async Task<string> VerifySqlPreflightAsync(
        AzureResources resources,
        string runtimeUser,
        string adminPassword)
    {
        string connectionString = BuildConnectionString(
            resources.SqlFqdn,
            DatabaseName,
            AdminLogin,
            adminPassword);
        await using var connection = new SqlConnection(connectionString);
        connectionString = string.Empty;
        await connection.OpenAsync();
        return await VerifySqlPreconditionsAsync(connection, runtimeUser);
    }

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string CreateRandomSecret(int byteCount)
    {
        byte[] generatedBytes = RandomNumberGenerator.GetBytes(byteCount);
        try
        {
            return Convert.ToBase64String(generatedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(generatedBytes);
        }
    }

    private static async Task<HashSet<string>> FindExistingRuntimeSecretsAsync(SecretClient client)
    {
        var targetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SqlConnectionSecretName,
            LoginHmacSecretName
        };
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await foreach (SecretProperties properties in client.GetPropertiesOfSecretsAsync())
        {
            if (targetNames.Contains(properties.Name))
            {
                found.Add(properties.Name);
            }
        }

        await foreach (DeletedSecret deletedSecret in client.GetDeletedSecretsAsync())
        {
            if (targetNames.Contains(deletedSecret.Name))
            {
                found.Add(deletedSecret.Name);
            }
        }

        return found;
    }

    private static async Task EnsureRuntimeSecretAbsentAsync(SecretClient client, string secretName)
    {
        HashSet<string> existing = await FindExistingRuntimeSecretsAsync(client);
        if (existing.Contains(secretName))
        {
            throw new BootstrapException(
                $"Metadata per {secretName} gia' esistenti; nessuna nuova versione verra' creata.");
        }
    }

    private static async Task EnsureRuntimeSecretsAbsentAsync(
        SecretClient client,
        string? allowedExistingName = null)
    {
        HashSet<string> existing = await FindExistingRuntimeSecretsAsync(client);
        existing.Remove(allowedExistingName ?? string.Empty);
        if (existing.Count > 0)
        {
            throw new BootstrapException(
                $"Trovati metadata per secret runtime gia' esistenti ({string.Join(", ", existing)}). Nessun secret verra' sovrascritto.");
        }
    }

    private static async Task ProvisionRuntimeSqlAsync(
        AzureResources resources,
        string runtimeUser,
        string runtimePassword,
        string adminPassword)
    {
        string connectionString = BuildConnectionString(
            resources.SqlFqdn,
            DatabaseName,
            AdminLogin,
            adminPassword);
        await using var connection = new SqlConnection(connectionString);
        connectionString = string.Empty;
        await connection.OpenAsync();
        await ProvisionRuntimeSqlAsync(connection, runtimeUser, runtimePassword);
    }

    private static async Task VerifyRuntimeSqlAsync(
        AzureResources resources,
        string runtimeUser,
        string runtimePassword)
    {
        string connectionString = BuildConnectionString(
            resources.SqlFqdn,
            DatabaseName,
            runtimeUser,
            runtimePassword);
        await using var connection = new SqlConnection(connectionString);
        connectionString = string.Empty;
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT USER_NAME(), IS_ROLEMEMBER(@role);",
            connection);
        command.Parameters.AddWithValue("@role", RuntimeRole);
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() ||
            !string.Equals(reader.GetString(0), runtimeUser, StringComparison.OrdinalIgnoreCase) ||
            reader.IsDBNull(1) ||
            reader.GetInt32(1) != 1)
        {
            throw new BootstrapException("Verifica del principal o dell'appartenenza al ruolo SQL non riuscita; riconciliare manualmente.");
        }
    }

    private static async Task<string> VerifySqlPreconditionsAsync(SqlConnection connection, string runtimeUser)
    {
        await using (var command = new SqlCommand("SELECT DB_NAME();", connection))
        {
            string? currentDatabase = (string?)await command.ExecuteScalarAsync();
            if (!string.Equals(currentDatabase, DatabaseName, StringComparison.OrdinalIgnoreCase))
            {
                throw new BootstrapException("La connessione SQL non punta al database atteso.");
            }
        }

        var parameters = ExpectedTables.Select((_, index) => $"@table{index}").ToArray();
        await using (var command = new SqlCommand(
            $"SELECT [t].[name] FROM [sys].[tables] AS [t] INNER JOIN [sys].[schemas] AS [s] ON [s].[schema_id] = [t].[schema_id] WHERE [s].[name] = N'dbo' AND [t].[name] IN ({string.Join(", ", parameters)});",
            connection))
        {
            for (int index = 0; index < ExpectedTables.Length; index++)
            {
                command.Parameters.AddWithValue(parameters[index], ExpectedTables[index]);
            }

            var actualTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                actualTables.Add(reader.GetString(0));
            }

            string[] missingTables = ExpectedTables.Where(table => !actualTables.Contains(table)).ToArray();
            if (missingTables.Length > 0)
            {
                throw new BootstrapException(
                    $"Schema dbo incompleto ({string.Join(", ", missingTables)}). Applicare e revisionare le migration separatamente; il helper non le esegue.");
            }
        }

        await VerifyMigrationsAsync(connection);

        await using (var command = new SqlCommand(
            "SELECT [name] FROM [sys].[database_principals] WHERE [name] IN (@role, @user);",
            connection))
        {
            command.Parameters.AddWithValue("@role", RuntimeRole);
            command.Parameters.AddWithValue("@user", runtimeUser);
            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            var existingPrincipals = new List<string>();
            while (await reader.ReadAsync())
            {
                existingPrincipals.Add(reader.GetString(0));
            }

            if (existingPrincipals.Count > 0)
            {
                throw new BootstrapException(
                    $"Esiste gia' un principal/ruolo runtime ({string.Join(", ", existingPrincipals)}); nessuna modifica e' stata eseguita.");
            }
        }

        await using (var command = new SqlCommand(
            "SELECT [actual_state_desc] FROM [sys].[database_query_store_options];",
            connection))
        {
            string? queryStoreState = (string?)await command.ExecuteScalarAsync();
            if (string.IsNullOrWhiteSpace(queryStoreState))
            {
                throw new BootstrapException("Stato Query Store non verificabile; nessuna scrittura verra' eseguita.");
            }

            return queryStoreState;
        }
    }

    private static async Task VerifySqlAuditingAndDiagnosticsAsync(string sqlServerId, string databaseId)
    {
        using JsonDocument serverAudit = await RunAzureJsonAsync(
            "sql", "server", "audit-policy", "show",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroup,
            "--name", SqlServerName,
            "--output", "json");
        EnsureAuditDisabled(serverAudit.RootElement, "server");

        using JsonDocument databaseAudit = await RunAzureJsonAsync(
            "sql", "db", "audit-policy", "show",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroup,
            "--server", SqlServerName,
            "--name", DatabaseName,
            "--output", "json");
        EnsureAuditDisabled(databaseAudit.RootElement, "database");

        await EnsureNoSqlLogDiagnosticsAsync(sqlServerId);
        await EnsureNoSqlLogDiagnosticsAsync(databaseId);
    }

    private static void EnsureAuditDisabled(JsonElement policy, string scope)
    {
        string state = GetRequiredString(policy, "state");
        if (!string.Equals(state, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            throw new BootstrapException($"Auditing SQL {scope} e' attivo; il helper rifiuta di inviare il DDL con la password.");
        }
    }

    private static async Task EnsureNoSqlLogDiagnosticsAsync(string resourceId)
    {
        using JsonDocument diagnostics = await RunAzureJsonAsync(
            "monitor", "diagnostic-settings", "list",
            "--subscription", SubscriptionId,
            "--resource", resourceId,
            "--output", "json");

        if (diagnostics.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new BootstrapException("Diagnostic settings SQL non verificabili; nessuna scrittura verra' eseguita.");
        }

        var enabledCategories = new List<string>();
        foreach (JsonElement setting in diagnostics.RootElement.EnumerateArray())
        {
            if (!setting.TryGetProperty("logs", out JsonElement logs) || logs.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement log in logs.EnumerateArray())
            {
                if (log.TryGetProperty("enabled", out JsonElement enabled) &&
                    enabled.ValueKind == JsonValueKind.True)
                {
                    string category = log.TryGetProperty("category", out JsonElement categoryElement)
                        ? categoryElement.GetString() ?? "unknown"
                        : "unknown";
                    enabledCategories.Add(category);
                }
            }
        }

        if (enabledCategories.Count > 0)
        {
            throw new BootstrapException(
                $"Diagnostic log SQL attivi ({string.Join(", ", enabledCategories)}); il helper rifiuta di inviare il DDL con la password.");
        }
    }

    private static async Task VerifyMigrationsAsync(SqlConnection connection)
    {
        await using (var existsCommand = new SqlCommand(
            "SELECT OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U');",
            connection))
        {
            object? historyTable = await existsCommand.ExecuteScalarAsync();
            if (historyTable is null or DBNull)
            {
                throw new BootstrapException("La cronologia EF non esiste; applicare e revisionare le migration separatamente.");
            }
        }

        await using var command = new SqlCommand(
            "SELECT [MigrationId] FROM [dbo].[__EFMigrationsHistory];",
            connection);
        var appliedMigrations = new List<string>();
        await using (SqlDataReader reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                appliedMigrations.Add(reader.GetString(0));
            }
        }

        MigrationHistoryDifference difference =
            BootstrapMigrationHistory.Compare(
                appliedMigrations,
                BootstrapMigrationHistory.ExpectedMigrationIds);
        if (!difference.IsExact)
        {
            throw new BootstrapException(
                $"Cronologia EF diversa da quella attesa; mancanti: {string.Join(", ", difference.Missing)}; inattese: {string.Join(", ", difference.Unexpected)}. Il helper non applica migration.");
        }
    }

    private static async Task ProvisionRuntimeSqlAsync(
        SqlConnection connection,
        string runtimeUser,
        string runtimePassword)
    {
        await ExecuteSqlAsync(
            connection,
            $"CREATE ROLE {QuoteIdentifier(RuntimeRole)} AUTHORIZATION [dbo];");

        await ExecuteSqlAsync(
            connection,
            $"CREATE USER {QuoteIdentifier(runtimeUser)} WITH PASSWORD = {QuoteLiteral(runtimePassword)};");

        foreach (TableGrant grant in RuntimeGrants)
        {
            await ExecuteSqlAsync(
                connection,
                $"GRANT {grant.Permissions} ON OBJECT::[dbo].{QuoteIdentifier(grant.Table)} TO {QuoteIdentifier(RuntimeRole)};");
        }

        await ExecuteSqlAsync(
            connection,
            $"ALTER ROLE {QuoteIdentifier(RuntimeRole)} ADD MEMBER {QuoteIdentifier(runtimeUser)};");
    }

    private static async Task ExecuteSqlAsync(SqlConnection connection, string commandText)
    {
        await using var command = new SqlCommand(commandText, connection)
        {
            CommandTimeout = 30
        };
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildConnectionString(string server, string database, string user, string password)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"tcp:{server},1433",
            InitialCatalog = database,
            UserID = user,
            Password = password,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 60,
            Pooling = false,
            ApplicationName = "RoamlyRuntimeBootstrap"
        };
        return builder.ConnectionString;
    }

    private static string PromptRuntimeUser()
    {
        Console.Write("Nome principal SQL runtime [roamly_runtime]: ");
        string value = Console.ReadLine()?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            value = "roamly_runtime";
        }

        if (!Regex.IsMatch(value, "^[A-Za-z][A-Za-z0-9_]{0,79}$", RegexOptions.CultureInvariant))
        {
            throw new BootstrapException("Il nome del principal deve iniziare con una lettera e contenere solo lettere, numeri e underscore.");
        }

        return value;
    }

    private static string ReadHidden(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            throw new BootstrapException("Input non interattivo rifiutato; nessuna password verra' letta da stdin.");
        }

        Console.Write(prompt);
        var characters = new List<char>();
        try
        {
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (characters.Count > 0)
                    {
                        characters.RemoveAt(characters.Count - 1);
                    }

                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    characters.Add(key.KeyChar);
                }
            }

            if (characters.Count == 0)
            {
                throw new BootstrapException("Password SQL vuota; nessuna connessione e' stata aperta.");
            }

            char[] copiedCharacters = characters.ToArray();
            try
            {
                return new string(copiedCharacters);
            }
            finally
            {
                Array.Clear(copiedCharacters, 0, copiedCharacters.Length);
            }
        }
        finally
        {
            characters.Clear();
        }
    }

    private static bool ConfirmExact(string expected)
    {
        Console.Write($"Per confermare, digitare esattamente {expected}: ");
        string? response = Console.ReadLine();
        return string.Equals(response, expected, StringComparison.Ordinal);
    }

    private static async Task<JsonDocument> RunAzureJsonAsync(params string[] arguments)
    {
        string output = await RunAzureCliAsync(arguments);
        try
        {
            return JsonDocument.Parse(output);
        }
        catch
        {
            throw new BootstrapException("Azure CLI ha restituito metadata non validi; operazione interrotta.");
        }
    }

    private static async Task<string> RunAzureCliAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "pwsh";
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Invoke-RoamlyAzureCli.ps1"));
            startInfo.ArgumentList.Add("-ArgumentsJson");
            startInfo.ArgumentList.Add(JsonSerializer.Serialize(arguments));
        }
        else
        {
            startInfo.FileName = "az";
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch
        {
            throw new BootstrapException("Azure CLI non disponibile; nessuna operazione Azure e' stata avviata.");
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        string output = await stdoutTask;
        _ = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new BootstrapException("Lettura metadata Azure non riuscita; verificare l'accesso senza riportare valori secret.");
        }

        return output.Trim();
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new BootstrapException($"Metadata Azure mancante: {propertyName}.");
        }

        return value.GetString()!;
    }

    private static void EnsureResourceId(string actual, string expectedSuffix)
    {
        string expectedPrefix =
            $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroup}/{expectedSuffix}";
        if (!string.Equals(actual.TrimEnd('/'), expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new BootstrapException("Un resource ID Azure non corrisponde al contesto approvato; nessuna scrittura e' stata avviata.");
        }
    }

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string QuoteLiteral(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static void PrintStage(int number, string title)
    {
        Console.WriteLine();
        Console.WriteLine($"Fase {number}/4 - {title}");
    }

    private static void PrintResource(string label, string value) => Console.WriteLine($"{label}: {value}");

    internal sealed record AzureResources(
        string TenantId,
        string SqlServerId,
        string SqlFqdn,
        string DatabaseId,
        string DatabaseStatus,
        string VaultId,
        string VaultUri,
        string IdentityId,
        string IdentityPrincipalId);

    internal sealed class BootstrapState
    {
        public int ManifestVersion { get; set; }
        public string? SubscriptionId { get; set; }
        public AzureResources? Resources { get; set; }
        public bool SqlTouched { get; set; }
        public bool RuntimeSecretsWriteStarted { get; set; }
        public bool SqlConnectionSecretWritten { get; set; }
        public bool LoginHmacSecretWritten { get; set; }
        public bool BootstrapCompleted { get; set; }
    }

    private sealed record TableGrant(string Table, string Permissions);

    internal sealed class BootstrapCancelledException : Exception
    {
    }

    internal sealed class BootstrapException(string safeMessage) : Exception
    {
        public string SafeMessage { get; } = safeMessage;
    }

}
