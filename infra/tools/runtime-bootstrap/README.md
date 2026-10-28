# Bootstrap SQL e Key Vault runtime

Questo strumento locale prepara il principal SQL runtime e i due secret
Key Vault usati dall'applicazione. Non applica migration, non modifica il
firewall, non crea Container App o workflow e non avvia il deploy.

## Requisiti

- Windows x64 e PowerShell 7 (`pwsh`);
- .NET SDK selezionato da `global.json`;
- Azure CLI autenticata come `lorenzo.catarcia@outlook.com` sulla subscription
  prevista dal codice;
- assegnazione diretta `Key Vault Administrator` dell'operatore sul vault e
  secret `sql-admin-password` valorizzato con la password SQL amministrativa.

Il bootstrap usa l'identità di Azure CLI dell'operatore anche per scrivere i
due secret runtime tramite `AzureCliCredential` e `SecretClient`. Non crea
app registration, service principal, credential o ruolo RBAC temporanei e non
rimuove né ripristina l'assegnazione KVA dell'operatore. L'identità managed
runtime e i suoi permessi restano invariati.

## Esecuzione

Avviare `roamly-runtime-bootstrap.ps1` da PowerShell oppure
`roamly-runtime-bootstrap.sh` da Git Bash. Il bridge esegue il preflight e
richiede conferme separate prima delle operazioni che possono leggere secret,
connettersi a SQL o scrivere i secret runtime. Una connessione SQL può
riattivare il database e generare costi.

Le conferme esatte restano:

- `READ ADMIN SECRET` prima di leggere `sql-admin-password`;
- `CONNECT` prima del preflight SQL iniziale;
- `ACCEPT SQL TELEMETRY RISK` e `CREATE SQL` prima del nuovo preflight e
  provisioning SQL;
- `CREATE RUNTIME SECRETS` prima di scrivere i due secret.

Il bridge non mostra né persiste password, connection string o valori Key
Vault, e non passa valori secret come argomenti CLI. Se SQL è stato modificato
e una fase successiva fallisce, non esegue rollback o rotazioni automatiche:
fermarsi e riconciliare lo stato SQL e dei secret in modo controllato.

Su Windows il bridge invoca Azure CLI tramite PowerShell 7 e passa gli
argomenti come array JSON, senza comporre una command line `cmd.exe`.

## Recovery

Prima del provisioning SQL il bridge aggiorna atomicamente un manifest privo
di segreti in `%LOCALAPPDATA%\Roamly\RuntimeBootstrap\pending-recovery.json`.
Il manifest registra solo contesto risorse e stato booleano del provisioning
SQL e delle due scritture Key Vault (`SqlTouched`,
`RuntimeSecretsWriteStarted`, `SqlConnectionSecretWritten`,
`LoginHmacSecretWritten`, `BootstrapCompleted`).

Se SQL non è stato toccato, `Recover-RoamlyRuntimeBootstrap.ps1` può rimuovere
il manifest locale. Se SQL o le scritture dei secret possono essere parziali,
il manifest viene mantenuto e il bootstrap ordinario resta bloccato. Recovery
non esegue Azure CLI, non si connette a SQL, non legge o scrive secret e non
tenta rollback. Dopo aver riconciliato manualmente SQL e i metadata dei due
secret, rimuovere il manifest pendente per autorizzare un nuovo bootstrap.
Non cancellarlo mentre lo stato è incerto.

Un manifest della vecchia procedura che contiene identificatori di risorse
temporanee viene conservato e rifiutato dal recovery aggiornato: va
riconciliato manualmente, senza avviare operazioni Azure automatiche.

## Test offline

Da `C:\dev\Roamly` eseguire:

```powershell
dotnet test --project tests/Roamly.RuntimeBootstrap.Tests --configuration Release
dotnet build infra/tools/runtime-bootstrap/bridge/Roamly.RuntimeBootstrap.csproj --configuration Release
```

I test del coordinatore usano effetti fake in-memory fail-closed e verificano
che il flusso non chieda consensi né invochi operazioni di identità/ruolo
temporanei, che richieda i gate prima di leggere la password/connettersi/
scrivere, e che richieda entrambi i secret runtime dopo il provisioning SQL.
I test di recovery verificano lo stato SQL/secret del manifest e non invocano
Azure CLI o SQL. La suite è offline: non legge secret reali e non si connette
ad Azure o SQL. Il bootstrap live non è stato eseguito.
