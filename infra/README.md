# Provisioning Azure

`main.bicep` descrive le fondamenta del singolo ambiente Roamly in Italy North:

- Log Analytics con retention di 30 giorni;
- Azure SQL Serverless General Purpose Gen5, 0,5–1 vCore, auto-pause dopo 15 minuti e limite dati di 1 GB;
- SQL Authentication con password amministrativa conservata in Key Vault e non usata dal runtime;
- endpoint SQL pubblico, TLS minimo 1.2 e regola firewall `0.0.0.0` per consentire connessioni dai servizi Azure;
- Azure Blob Storage Standard LRS Hot in Italy North, con container Journal privato, accesso OAuth e hard delete;
- ambiente Container Apps Consumption collegato a Log Analytics;
- Key Vault Standard con soft delete a 90 giorni, endpoint pubblico e autenticazione Entra/RBAC.

Il template è a scope resource group e presuppone che `rg-roamly-dev` esista già. Il nome del gruppo non cambia il runtime previsto: questo è l'unico ambiente del servizio reale, quindi l'applicazione dovrà usare `ASPNETCORE_ENVIRONMENT=Production`.

L'identità runtime non riceve accesso a livello di vault: così non può leggere la password SQL amministrativa. I due secret runtime sono stati creati il 2026-10-27; assegnare `Key Vault Secrets User` alla managed identity solo sui singoli scope `roamly-sql-connection` e `roamly-login-hmac`. Il runtime usa il principal SQL distinto `roamly_runtime` con il ruolo/grant minimo preparato dal bootstrap.

## Accesso amministrativo umano al Key Vault

Dal 2026-10-26 l'account `lorenzo.catarcia@outlook.com` ha `Key Vault Administrator` sul solo vault `kv-roamlywse7wit7fzpja`. L'account ha inoltre un'assegnazione `Owner` ereditata a livello subscription, che consente già di gestire le risorse e le assegnazioni RBAC. Il vault usa Azure RBAC (`enableRbacAuthorization: true`): il menu legacy **Access policies** non governa l'accesso ai dati. La nuova assegnazione è live e manuale, non dichiarata in `main.bicep`; lo stato va mantenuto e verificato separatamente. L'accesso ai metadata dei secret è stato verificato senza leggere valori.

## Bootstrap SQL e secret runtime

Lo strumento locale per creare il contained user SQL e i secret runtime è
versionato in [`tools/runtime-bootstrap`](tools/runtime-bootstrap/README.md).
Non fa parte dei workflow GitHub Actions e non applica migration. Il bridge
semplificato è stato eseguito sulle risorse Azure il 2026-10-27: il principal
`roamly_runtime`, i relativi grant e i due secret runtime sono stati creati e
verificati. La regola firewall temporanea `/32` usata per il collegamento SQL
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

Il primo provisioning non crea Container App o scheduler e non applica migration SQL. L'ACR Basic `acrroamlywse7wit7fzpja` è stato creato in Italy North il 2026-10-27 con admin user disabilitato; `AcrPull` è assegnato alla managed identity runtime sul solo scope del registry. Non sono ancora state pubblicate immagini né create Container App o workflow.

## Preparazione Container App e release

`container-app.bicep` è un template separato dal provisioning delle fondamenta. Riferisce risorse esistenti tramite ID/nome, non assegna ruoli e non crea identità, ACR o Key Vault. Il profilo ACA già approvato è esplicito nel template: 0,25 vCPU, 0,5 GiB, zero repliche minime e massimo una; include probe startup, liveness process-only e readiness SQL con le soglie documentate in `docs/architecture/DEVOPS.md`. La configurazione fissa inoltre Italy North, Consumption, `Production` e ingress pubblico HTTPS, e referenzia i secret Key Vault senza leggerne i valori. Sono parametri di un template locale, non risorse live.

Il template mappa la connection string completa del contained SQL user in `ConnectionStrings__Roamly` e la chiave HMAC del login in un riferimento Key Vault separato. L'utente ha approvato che il secret SQL contenga la connection string completa; l'app non costruisce una stringa a partire dalla sola password. Il workflow non deve poter leggere quei secret.

La app registration OIDC `roamly-github-oidc` e il trust per l'Environment `azure-release` sono stati creati e verificati il 2026-10-27; il bootstrap resta manuale e il trust `azure-bootstrap` è stato rimosso prima di assegnare ruoli. Sono stati assegnati al principal release il custom role deployments sul RG, il custom role ACA temporaneamente sul RG, `AcrPush` solo sull'ACR e `Managed Identity Operator` solo sulla managed identity runtime. Le variabili GitHub non segrete per OIDC sono configurate in `azure-release`; i workflow in `.github/workflows/` non sono ancora presenti. Vedere `infra/workflows/README.md` e la sezione "Stato operativo: preparazione locale release" in `docs/architecture/DEVOPS.md` per i gate HIGH aperti. Non eseguire questo template, `az deployment`, comandi Container Apps o workflow finché non sono chiusi i gate, verificati gli ID reali e approvato il piano di release. Nessuna migration viene applicata automaticamente all'avvio dell'API.

## Passi operativi successivi

1. Le impostazioni firewall SQL, le ACL di rete Key Vault e la configurazione effettiva Blob sono state verificate il 2026-10-20; ripetere la verifica dopo ogni modifica al template o alle risorse.
2. ✅ Principal SQL `roamly_runtime` e grant runtime creati e verificati il 2026-10-27; non usare il login amministrativo nell'applicazione.
3. ✅ `Key Vault Secrets User` assegnato il 2026-10-27 alla managed identity runtime solo sui due scope secret runtime; nessun accesso al secret SQL amministrativo.
4. Il registro operativo riporta tutte le 14 migration applicate il 2026-10-26; la cronologia live non è stata riverificata in questa sessione. Verificati: backup LRS, TDE attivo, retention short-term 7 giorni e LTR non configurato. Budget alert da 10 EUR/mese verificato sul RG: 80% Actual e 100% Forecasted, senza cap; gli altri alert restano da configurare.
5. ✅ ACR Basic creato e `AcrPull` assegnato il 2026-10-27; OIDC release, variabili GitHub non secret e ruoli minimi sono configurati. Restano workflow, immagine e Container App; dopo il primo deploy va ristretto lo scope ACA dalla RG alla singola app.

Non inserire password, connection string, client secret o valori di segreti nei parametri o nel repository. Il template non configura l'accesso OIDC del workflow GitHub e non legge i valori dei segreti da Key Vault.
