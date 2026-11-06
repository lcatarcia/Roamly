# Provisioning Azure

## Preparazione locale Maps — 6 novembre 2026

Il wiring candidato modifica solo `container-app.bicep` e il workflow
applicativo con la sua copia in `workflows/`. Non crea risorse Maps, SQL,
registry, identita o assegnazioni RBAC; non applica migration.
`main.bicep` e il bootstrap hardcoded sul server SQL storico **non sono
percorsi di questo rilascio**. Le altre modifiche gia presenti nel checkout
dirty non sono automaticamente autorizzate alla pubblicazione.

### Configurazione non segreta

Il template mantiene `mapsEnabled=false` e `mapsAccountClientId=''` per
default. Il workflow acquisisce una volta dal GitHub Environment
`azure-release` le variabili non segrete e passa gli stessi valori ai due
what-if e al deployment:

| Variabile Environment | Valore per il prossimo rilascio Maps autorizzato | Configurazione ACA |
| --- | --- | --- |
| `MAPS_ENABLED` | `true` (assente = `false`) | `Maps__Enabled` |
| `MAPS_ACCOUNT_CLIENT_ID` | `4eb100cc-1466-484e-a78e-aa6ae42734b8` | `Maps__AccountClientId` |
| Nessuna nuova variabile MI | client ID letto dall'identita runtime esistente: `3533c727-acac-4061-a82d-39550a6788fa` | `Maps__ManagedIdentityClientId` |

I valori sono stati verificati con GET ARM, senza token Maps o lettura di
chiavi. L'API ARM Maps `2023-06-01` restituisce il client ID pubblico
dell'account in **`properties.uniqueId`**; `properties.clientId` e assente.
Non usare il client ID della managed identity come ID account Maps.
Il preflight rifiuta flag diversi da `true`/`false`, GUID vuoti/non validi
quando abilitato e l'uguaglianza fra ID account e ID MI. Non aggiunge
permessi al principal OIDC per leggere l'account Maps.

L'identita `id-roamly-runtime-prod` e gia associata a `ca-roamly-prod`.
Gli scope runtime esistenti includono Render Reader e Metrics Reader
sul solo account `maps-roamly-dev`, e Cost Query Reader su `rg-roamly-dev`.
Questo wiring non modifica RBAC, non abilita Search e non attiva
l'observer economico: il codice personale usa il consenso e legge il latch
di sospensione persistito.

### Migration e grants prima del rilascio del codice

Il percorso supportato resta lo script SQL EF **idempotente revisionato**,
con applicazione amministrativa separata e autorizzata al database
`roamly` su `sql-roamly-free-we-wse7wit7fzpja`. Non passare credenziali al
tooling e non eseguire `database update`, bootstrap o `Down()` automatici.
Generazione offline dalla factory design-time (`Server=none;Database=none`),
usando un build aggiornato che contenga tutte le migration:

```powershell
dotnet ef migrations script 20261028134346_AddCamperManualSpecifications 20261106105835_AddMapsEconomicGate --idempotent --no-build --project src/Roamly.Infrastructure --startup-project src/Roamly.Infrastructure --context RoamlyDbContext --output "$env:TEMP/roamly-maps-release-20261106.sql"
```

Le tre migration attese sono:

- `20261104084728_AddSavedPlaceGoogleMapsUrl`;
- `20261106091448_AddIdentityConsentRegistry`;
- `20261106105835_AddMapsEconomicGate`.

Lo script generato e revisionato offline aggiunge la colonna nullable
`SavedPlaces.GoogleMapsUrl`, `ConsentTransitions` (PK owned e indice
univoco per revisione) e `MapsEconomicStates` con i relativi vincoli.
Non contiene drop, copie dati, credenziali o cambi di principal.
La generazione non attesta lo schema live: verificare separatamente
destinazione e history prima dell'applicazione. Nessuna connessione SQL
e stata aperta per questa preparazione.

Aggiornamento minimo dell'allowlist **esistente** `RoamlyRuntime`, dopo
verifica di membership e permessi sul database corrente:

```sql
GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[ConsentTransitions] TO [RoamlyRuntime];
GRANT SELECT ON OBJECT::[dbo].[MapsEconomicStates] TO [RoamlyRuntime];
```

`DELETE` sul consenso serve all'account-erasure; non serve `UPDATE`.
Il token issuer personale legge soltanto lo stato economico. `INSERT,
UPDATE` su `MapsEconomicStates` saranno necessari solo a un futuro gate
economico attivo, non a questo rilascio. `SavedPlaces` ha gia i grants
allowlist `SELECT, INSERT, UPDATE, DELETE`: aggiungere una colonna non
richiede nuovi grants. Non assegnare ruoli database ampi, non creare un
nuovo principal e non rilanciare il vecchio bootstrap, che punta al server
storico e richiede un principal non ancora esistente.

Le migration sono prerequisito anche con Maps disabilitato: il nuovo
modello Places/consenso e usato dalle query e dai flussi privacy.

### Runtime verificato e limite della precedente release

La run `41854167437` del 4 novembre, SHA
`8f6a514e8ea2ed8d6051cf76275d1594f9111e78`, ha completato build/push,
entrambi i what-if e il deployment ARM. Il deployment
`roamly-release-41854167437-1` e `Succeeded`. Oggi la sola revisione
attiva e `ca-roamly-prod--0000009`, Healthy/ScaledToZero, con immagine
`ghcr.io/lcatarcia/roamly@sha256:f5a0b70cf0e199cd9840e922ff7f0eec2990b094d2fcbbf50747d6f7f62f2f43`.
Non ripristinare il digest del 2 novembre come se fosse ancora corrente.

La run e fallita **dopo** ARM: il gate sulle revisioni non ha confermato
il digest atteso nei sei tentativi; non ha raggiunto i probe HTTPS.
I log scartano stderr della CLI e trattano gli errori come risposta vuota:
non consentono di distinguere errore CLI/RBAC, timeout o stato transitorio
al momento della run. Nessuna di queste cause viene dichiarata provata
e nessuna correzione di permessi/polling e introdotta in questa patch.
Il digest live oggi coincide con quello atteso dalla run: il fallimento
workflow non implica che il deployment non sia avvenuto.

Preservare Single, min 0/max 1, GHCR pubblico senza credenziali, environment,
MI, Blob, risorse/probe e riferimenti Key Vault correnti. Il preflight
conserva la versione SQL pinned e l'HMAC esistente. SQL e West Europe,
Free/AutoPause, 0,5-1 vCore, 32 GiB e pausa inattivita 60 minuti; lo stato
ARM letto e `Paused`. Nessun rilascio locale o modifica cloud e stato
eseguito. Prima della pubblicazione servono autorizzazione esplicita
all'insieme di file selezionato, PR su `main` con i quattro check,
migration/grants autorizzati e configurazione Environment; dispatch e
verifica operativa restano azioni successive distinte.

`main.bicep` descrive le fondamenta del singolo ambiente Roamly in Italy North:

- Log Analytics con retention di 30 giorni;
- Azure SQL Serverless General Purpose Gen5, 0,5–1 vCore, auto-pause dopo 15 minuti e limite dati di 1 GB;
- SQL Authentication con password amministrativa conservata in Key Vault e non usata dal runtime;
- endpoint SQL pubblico, TLS minimo 1.2 e regola firewall `0.0.0.0` per consentire connessioni dai servizi Azure;
- Azure Blob Storage Standard LRS Hot in Italy North, con container Journal privato, accesso OAuth e hard delete;
- ambiente Container Apps Consumption collegato a Log Analytics;
- Key Vault Standard con soft delete a 90 giorni, endpoint pubblico e autenticazione Entra/RBAC.

Il template è a scope resource group e presuppone che `rg-roamly-dev` esista già. Il nome del gruppo non cambia il runtime previsto: questo è l'unico ambiente del servizio reale, quindi l'applicazione dovrà usare `ASPNETCORE_ENVIRONMENT=Production`.

L'identità runtime non riceve accesso a livello di vault: così non può leggere la password SQL amministrativa. I due secret runtime (`roamly-sql-connection` e `roamly-login-hmac`) sono stati creati il 2026-10-27; `Key Vault Secrets User` è assegnato alla managed identity sui soli scope dei rispettivi secret. Il runtime usa il principal SQL distinto `roamly_runtime`; principal e relativi grant sono stati creati e verificati dal bootstrap.

## Accesso amministrativo umano al Key Vault

Dal 2026-10-26 l'account `lorenzo.catarcia@outlook.com` ha `Key Vault Administrator` sul solo vault `kv-roamlywse7wit7fzpja`. L'account ha inoltre un'assegnazione `Owner` ereditata a livello subscription, che consente già di gestire le risorse e le assegnazioni RBAC. Il vault usa Azure RBAC (`enableRbacAuthorization: true`): il menu legacy **Access policies** non governa l'accesso ai dati. La nuova assegnazione è live e manuale, non dichiarata in `main.bicep`; lo stato va mantenuto e verificato separatamente. L'accesso ai metadata dei secret è stato verificato senza leggere valori.

## Bootstrap SQL e secret runtime

Lo strumento locale per creare il contained user SQL e i secret runtime è
versionato in [`tools/runtime-bootstrap`](tools/runtime-bootstrap/README.md).
Non fa parte dei workflow GitHub Actions e non applica migration. Il bridge
semplificato è stato eseguito sulle risorse Azure il 2026-10-27: il principal
`roamly_runtime`, i relativi grant e i due secret runtime sono stati creati e
verificati. Entrambi i riferimenti Key Vault della Container App e l'accesso
della managed identity ai rispettivi secret scope sono configurati. La regola
firewall temporanea `/32` usata per il collegamento SQL
è stata rimossa e verificata assente. Ogni operazione che modifica SQL o Key
Vault richiede una conferma esplicita; un manifest locale di recovery blocca
nuovi avvii se lo stato SQL o dei secret può essere parziale. La password
amministrativa viene letta da `sql-admin-password` solo dopo la conferma
`READ ADMIN SECRET`; non viene chiesta al prompt di Azure CLI.

## Accesso di rete e deployment

La proprietà `properties.outboundIpAddresses` non è esposta dall'ambiente ACA Consumption creato in questo resource group: la query CLI restituisce `null`. Per evitare NAT Gateway o Private Endpoint, l'utente ha approvato il 2026-10-20 un perimetro di rete più ampio e senza costi fissi aggiuntivi:

- Azure SQL usa la regola firewall `0.0.0.0`, che abilita l'accesso dai servizi Azure e **non è limitata a questa ACA o a questa subscription**;
- Key Vault ha endpoint pubblico con accesso rete consentito; l'autenticazione e i permessi restano governati da Entra/RBAC.

Questa scelta non equivale a un'allowlist dell'app e amplia il perimetro rispetto al deny-by-default. L'accesso SQL richiede comunque credenziali; Key Vault richiede un'identità e un ruolo RBAC. Rivalutare la rete prima di aggiungere utenti, dati sensibili o un servizio pubblico più ampio.

Per validare e revisionare il template senza creare risorse:

```powershell
az bicep build --file infra/main.bicep
```

La regione è vincolata a Italy North e la purge protection del Key Vault è abilitata nel template, secondo le decisioni approvate. La password SQL amministrativa è un parametro `@secure()` e viene conservata nel secret `sql-admin-password`. Non salvarla in file di parametri versionati, passarla direttamente nella riga di comando o includerla in output condivisi. Esaminare il `what-if` prima di ogni applicazione.

Con RBAC abilitato, l'identità che esegue il deployment deve poter scrivere i secret nel vault (ad esempio con `Key Vault Secrets Officer` allo scope del vault), altrimenti la creazione o l'aggiornamento di `sql-admin-password` può fallire. Non assegnare questo ruolo all'identità runtime; verificare anche che il `what-if` non proponga modifiche non volute alla password amministrativa.

Per applicare `journal-storage.bicep`, il principal di deployment deve inoltre poter creare role assignment (`Microsoft.Authorization/roleAssignments/write`) sugli scope dell'account e del container, ad esempio tramite `Owner`, `User Access Administrator` o `Role Based Access Control Administrator`. Il what-if non verifica questo permesso.

Il modulo `journal-storage.bicep` può essere validato, sottoposto a `what-if` e applicato in modo isolato senza ridistribuire SQL o passare la password amministrativa:

```powershell
$runtimePrincipalId = az identity show --resource-group rg-roamly-dev --name id-roamly-runtime-prod --query principalId --output tsv
az deployment group validate --resource-group rg-roamly-dev --template-file infra/journal-storage.bicep --parameters runtimePrincipalId=$runtimePrincipalId
az deployment group what-if --resource-group rg-roamly-dev --template-file infra/journal-storage.bicep --parameters runtimePrincipalId=$runtimePrincipalId
az deployment group create --resource-group rg-roamly-dev --name roamly-journal-storage --template-file infra/journal-storage.bicep --parameters runtimePrincipalId=$runtimePrincipalId
```

Il template crea una Storage Account `StorageV2` con replica LRS, tier Hot, HTTPS obbligatorio, TLS 1.2, endpoint di rete pubblico autenticato, accesso anonimo e shared key disabilitati. Su Blob disabilita soft delete, container soft delete, versioning e change feed; non abilita backup o snapshot. Il container `roamly-journal-images` ha `publicAccess: None`.

L'identità runtime riceve `Storage Blob Delegator` allo scope dell'account, necessario per generare User Delegation SAS, e `Storage Blob Data Contributor` soltanto sul container. Non assegnare ruoli Storage all'utente finale o al workflow OIDC. Dopo il deployment verificare i valori effettivi dell'account e del Blob service; solo allora impostare `JournalImages__Azure__AccountConfigurationVerified=true` nell'eventuale runtime.

Il template di provisioning non crea Container App o scheduler e non applica migration SQL. L'ACR Basic `acrroamlywse7wit7fzpja` è stato creato in Italy North il 2026-10-27 con admin user disabilitato; `AcrPull` è assegnato alla managed identity runtime sul solo scope del registry. La Container App `ca-roamly-prod` è **Running** sull'ambiente `cae-roamly-live`, con revisione pronta `ca-roamly-prod--0000007`. **Storico distinto, verificato tramite lettura read-only dei metadati GitHub Actions:** due run del workflow legacy `Release Azure` sono terminati con successo in `workflow_dispatch`, ai commit `686ed34` (2026-10-29 08:58 UTC) e `b31e3a8` (2026-10-29 10:53 UTC); ciascuno ha eseguito un job con build/push Docker, deploy Bicep e controlli `/health/live` e `/health/ready`. Questi run non sono esecuzioni del candidato attuale. Il candidato manuale in `.github/workflows/release.yml` è ancora locale e non eseguito; in questa sessione non sono stati effettuati nuovi dispatch, pubblicate nuove immagini o eseguiti nuovi deployment.

## Preparazione Container App e release

`container-app.bicep` è un template separato dal provisioning delle fondamenta. Riferisce risorse esistenti tramite ID/nome, non assegna ruoli e non crea identità, ACR o Key Vault. Il profilo ACA già approvato è esplicito nel template: 0,25 vCPU, 0,5 GiB, zero repliche minime e massimo una; include probe startup, liveness process-only e readiness SQL con le soglie documentate in `docs/architecture/DEVOPS.md`. La configurazione fissa inoltre Italy North, Consumption, `Production` e ingress pubblico HTTPS, e referenzia i secret Key Vault senza leggerne i valori. Sono parametri di un template locale, non risorse live.

Il template mappa la connection string completa del contained SQL user in `ConnectionStrings__Roamly` e la chiave HMAC del login in un riferimento Key Vault separato. L'utente ha approvato che il secret SQL contenga la connection string completa; l'app non costruisce una stringa a partire dalla sola password. Il workflow non deve poter leggere quei secret.

Il template passa inoltre il client ID della managed identity user-assigned runtime come `AZURE_CLIENT_ID`: `DefaultAzureCredential` usa questo valore per selezionare l'identità associata alla Container App durante l'accesso a Blob Storage. I workflow risolvono il client ID dall'identità esistente e si fermano se il valore non è disponibile.

La app registration OIDC `roamly-github-oidc`, l'Environment `azure-release` e le protezioni sono stati configurati e verificati; per l'Environment sono presenti le federated credential `repo:lcatarcia/Roamly:environment:azure-release` e `repo:lcatarcia@70380170/Roamly@1387274893:environment:azure-release`. Il bootstrap resta manuale e il trust `azure-bootstrap` è stato rimosso. Sono configurati per il principal release il custom role deployments sul RG, il custom role ACA sul RG, `AcrPush` solo sull'ACR e `Managed Identity Operator` solo sulla managed identity runtime. **Il ruolo ACA a scope RG è un blocker prima del dispatch:** `ca-roamly-prod` esiste già, mentre il ruolo consente operazioni ampie sulle Container Apps nel gruppo e include wildcard; restringere lo scope ai permessi necessari e riverificare l'accesso effettivo, incluso il diniego di `listSecrets`. Non qualificarlo come set di permessi minimo. Le variabili GitHub non segrete per OIDC sono configurate in `azure-release`; il candidato workflow è locale e non è stato eseguito, non ha pubblicato una nuova immagine né eseguito un deployment. Vedere `infra/workflows/README.md` e la sezione "Stato operativo: preparazione locale release" in `docs/architecture/DEVOPS.md` per i gate ancora aperti. Non eseguire questo template, `az deployment`, comandi Container Apps o workflow finché non sono chiusi i gate, verificati gli ID reali e approvato il piano di release. Nessuna migration viene applicata automaticamente all'avvio dell'API.

## Passi operativi successivi

1. Le impostazioni firewall SQL, le ACL di rete Key Vault e la configurazione effettiva Blob sono state verificate il 2026-10-20; ripetere la verifica dopo ogni modifica al template o alle risorse.
2. ✅ Principal SQL `roamly_runtime` e relativi grant runtime creati e verificati il 2026-10-27; non usare il login amministrativo nell'applicazione.
3. ✅ `Key Vault Secrets User` assegnato il 2026-10-27 alla managed identity runtime solo sugli scope dei secret `roamly-sql-connection` e `roamly-login-hmac`; nessun accesso al secret SQL amministrativo.
4. Il registro live `__EFMigrationsHistory` è stato verificato in sola lettura il 2026-10-30: risultano applicate tutte le 15 migration fino a `20261028134346_AddCamperManualSpecifications`; nessuna migration è stata applicata in questa sessione. Il confronto successivo al merge `ca591a5` conferma la corrispondenza esatta dei 15 ID con il repository, l'assenza dell'aggregata `20261023083431_AddAuthenticationInfrastructure` e la coerenza di colonne, tipi, default, chiavi e indici degli oggetti di autenticazione coinvolti: nessuna riconciliazione SQL è necessaria. Verificati: backup LRS, TDE attivo, retention short-term 7 giorni e LTR non configurato. Budget alert da 10 EUR/mese verificato sul RG: 80% Actual e 100% Forecasted, senza cap; gli altri alert restano da configurare.
5. ✅ ACR Basic creato e `AcrPull` assegnato il 2026-10-27; OIDC release e variabili GitHub non secret configurati. Dopo autorizzazione esplicita il 2026-10-30, il ruolo ACA è stato ridotto a read/write/revisions-read sulla sola `ca-roamly-prod`; aggiunto un ruolo read/join sul solo environment `cae-roamly-live` e rimossa l'assegnazione ACA ampia sul RG. I tre ruoli deployments, AcrPush e Managed Identity Operator sono invariati. Definizioni e assegnazioni dirette sono state rilette dopo ogni modifica; questa verifica supera la descrizione storica dei quattro ruoli sopra. Restano da verificare con token fresco del principal release accesso effettivo, eredità/gruppi e percorso write/join tramite what-if/deploy autorizzati. Il candidato workflow non è stato eseguito; nessun nuovo deploy.

Non inserire password, connection string, client secret o valori di segreti nei parametri o nel repository. Il template non configura l'accesso OIDC del workflow GitHub e non legge i valori dei segreti da Key Vault.
