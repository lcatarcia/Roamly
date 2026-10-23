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

L'identità runtime non riceve accesso a livello di vault: così non può leggere la password SQL amministrativa. Quando saranno definiti i segreti runtime, assegnare `Key Vault Secrets User` ai soli secret scope necessari. Il runtime dovrà inoltre usare un principal SQL distinto con privilegi minimi.

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

Il primo provisioning non crea Container App o scheduler e non applica migration SQL. La definizione ACR Basic è presente nel `main.bicep` locale, ma l'ACR non è stato creato in Azure.

## Preparazione Container App e release

`container-app.bicep` è un template separato dal provisioning delle fondamenta. Riferisce risorse esistenti tramite ID/nome, non assegna ruoli e non crea identità, ACR o Key Vault. Parametri di compute, numero massimo di repliche e probe non hanno default intenzionalmente: vanno approvati prima di un deployment. La configurazione fissa gli elementi già scelti (Italy North, Consumption, zero repliche minime, `Production`, ingress pubblico HTTPS) e referenzia i secret Key Vault senza leggere i valori.

Il template mappa la connection string completa del contained SQL user in `ConnectionStrings__Roamly` e la chiave HMAC del login in un riferimento Key Vault separato. L'utente ha approvato che il secret SQL contenga la connection string completa; l'app non costruisce una stringa a partire dalla sola password. Il workflow non deve poter leggere quei secret.

I futuri workflow di bootstrap OIDC e release OIDC sono bloccati, non installati in `.github/workflows/`; vedere `infra/workflows/README.md` e la sezione "Stato operativo: preparazione locale release" in `docs/architecture/DEVOPS.md` per le decisioni HIGH ancora mancanti. Non eseguire questo template, `az deployment`, comandi Container Apps o workflow finché non sono chiusi i gate, verificati gli ID reali e approvato il piano di release. Nessuna migration viene applicata automaticamente all'avvio dell'API.

## Passi operativi successivi

1. Le impostazioni firewall SQL, le ACL di rete Key Vault e la configurazione effettiva Blob sono state verificate il 2026-10-20; ripetere la verifica dopo ogni modifica al template o alle risorse.
2. Creare nel database un principal SQL distinto per il runtime e concedergli permessi minimi; non configurare l'applicazione con il login amministrativo.
3. Assegnare all'identità runtime `Key Vault Secrets User` solo sui singoli segreti runtime necessari; non concedere accesso al secret SQL amministrativo.
4. Prima di abilitare il runtime, applicare il bundle di migration con il principal amministrativo e verificare backup LRS, TDE, retention e alert.
5. Scegliere il registry e completare la pipeline OIDC/deploy in una fase successiva.

Non inserire password, connection string, client secret o valori di segreti nei parametri o nel repository. Il template non configura l'accesso OIDC del workflow GitHub e non legge i valori dei segreti da Key Vault.
