# Roamly — CI/CD e deploy

Stato: **target selezionato — fondamenta Azure create; runtime e release non distribuiti** · Ultimo aggiornamento: 2026-10-20
Origine: `docs/archive/Roamly_Planning_2026-09-24.md` §20 — **rivisto** secondo review I10, aggiornato al Blocco 5 (passo 18: `ci.yml`)
Owner: **@vulcan**

---

## 1. Pipeline

```text
Push -> CI
Pull Request -> Frontend E2E
        │
        ▼
   Checkout
        │
   ┌────┴────────────┐
   ▼                 ▼
Backend           Frontend
Restore           npm ci
Build             Lint/design
Test              Build
   └────┬────────────┘
        ▼
 Nightly separato: suite, E2E, drift SQL,
 migration bundle e audit vulnerabilita
```

### Workflow

Il planning originale proponeva 5 workflow (`ci.yml`, `backend.yml`, `frontend.yml`, `docker.yml`, `deploy.yml`). All'inizio è costo di manutenzione senza beneficio.

**Struttura attuale:**

- **`ci.yml`** — trigger `push` + `workflow_dispatch`, con tre job paralleli: `backend-fast` (L0: build + test senza database, **nessun Docker**), `backend-integration` (L1: Testcontainers + test dello schema completo) e `frontend` (lint, verificatori di design non-browser, i18n e build);
- **`frontend-e2e.yml`** — trigger `pull_request`; installa Chromium ed esegue lo script axe sui temi chiaro e scuro. Il trigger PR resta separato da `ci.yml`;
- **`nightly.yml`** — pianificato ogni giorno alle 02:00 UTC; suite backend, axe, prova di drift col CU SQL Server successivo quando il manifest esiste (altrimenti il solo drift e' esplicitamente saltato su HTTP 404), bundle migration su database vuoto e audit vulnerabilita;
- **workflow di bootstrap e `deploy.yml`** — separati e manuali, ma **non creati**: nomi/protezioni degli environment, ruoli OIDC e altri blocker HIGH sono ancora aperti. Le bozze di preparazione sono in `infra/workflows/README.md`; nessun workflow di rilascio è attivo.

Si splitta solo quando la CI diventa effettivamente lenta o eterogenea.

> **Stato:** il frontend esiste. I comandi effettivi sono i package script presenti in `frontend/roamly-web/package.json`; i workflow non invocano script assenti. In particolare `design:check` include axe e quindi non viene usato nel job push non-browser.
>
> **Trigger CI:** `push` su ogni branch + `workflow_dispatch`, senza `pull_request`. Il controllo browser su PR e' in `frontend-e2e.yml`; questo evita di cambiare i trigger esistenti della CI backend/frontend.

> **Perché `fast` e `integration` sono job distinti** ([`ADR-0009`](../adr/0009-test-strategy.md)): **17 dei 25 verificatori non richiedono alcun database**. Tenerli dietro l'avvio di un container SQL Server (60-100 s in CI) significherebbe pagare un minuto per sapere qualcosa che si può sapere in cinque secondi. Il primo segnale utile deve arrivare presto, altrimenti si smette di aspettarlo.

> ❌ **Nessun retry sui test** (**R37**). Un test che passa al secondo tentativo non è un test che passa: è un test che nasconde una race. I retry vanno vietati nella configurazione, non solo scoraggiati a parole.

**Vincolo da [`ADR-0003`](../adr/0003-auth-and-ownership.md):** il job backend deve compilare con `TreatWarningsAsErrors` — o almeno promuovere a errore le diagnostiche di `BannedApiAnalyzers`. Un analyzer che produce solo warning **non è un verificatore**: le API bandite (`ARCHITECTURE.md` §4) devono far fallire la CI, altrimenti il terzo livello di difesa è decorativo.

---

## 2. Requisiti mancanti dal planning originale

### 2.1 Strategia di migration in deploy

> **Regola: mai `Database.Migrate()` all'avvio in produzione.**

Usare `dotnet ef migrations bundle` come **step controllato** della pipeline, con:

- backup prima dell'esecuzione;
- piano di rollback esplicito;
- esecuzione disaccoppiata dal deploy dell'applicazione.

Finché vale [`ADR-0001`](../adr/0001-use-sql-server.md) nessuna migration contiene SQL manuale, quindi il bundle copre tutti i casi. **All'eventuale upgrade ai tipi spaziali questo cambia**: l'indice spaziale va creato con `migrationBuilder.Sql`, con `Down()` verificato e testato prima del deploy. Annotato ora perché è il genere di dettaglio che si dimentica.

> ⚠️ La regola del bundle non significa che esista una pipeline release: approvazioni, backup/rollback, credenziali e integrazione del bundle **non sono configurati** e restano parte della consultazione IaC/deploy.

> ✅ **Strategia IaC/release scelta dall'utente dopo consultazione Vulcan (2026-10-06):** Azure Bicep + GitHub Actions; la pipeline userà autenticazione federata OIDC, senza credenziali Azure statiche, e l'ambiente Production richiederà approvazione. La scelta non ha creato workflow o risorse e non autorizza il deploy: provisioning e rilascio restano bloccati finché non sono chiusi i gate di hardening, same-site, migrazioni, segreti/permessi e runtime.
>
> ✅ **Segreti runtime scelti dall'utente (2026-10-06):** Azure Key Vault con managed identity ACA e permessi minimi di sola lettura; l'identità OIDC della pipeline non deve leggere i valori. La scelta non configura né crea il vault. La chiave HMAC usata dal rate limiter deve restare stabile; una futura rotazione richiede una strategia esplicita.
>
> ✅ **Preparazione registry scelta dall'utente (2026-10-22):** Azure Container Registry SKU Basic, push da GitHub Actions via OIDC e pull ACA con la managed identity runtime (`AcrPull`). L'account admin ACR resta disabilitato. La scelta autorizza la preparazione locale Bicep, non la creazione del registry; costo ricorrente e prezzo aggiornato vanno verificati prima del provisioning.
>
> ✅ **Identità CI scelte dall'utente (2026-10-22):** due identità OIDC distinte per bootstrap infrastruttura e rilascio applicativo; nessuna delle due coincide con la managed identity runtime. Questa scelta non crea identità, federated credential, ruoli o ambienti GitHub: scope RBAC, soggetti OIDC e protezioni restano da configurare prima di usare la pipeline.

### 2.2 Target di deploy

> ✅ **Decisione #9 — target minimo scelto dall'utente (2026-10-06):** Azure Container Apps **Consumption**, API e job con **scale-to-zero**, Azure SQL **Serverless**, regione **Italy North**; cold-start accettati. È previsto un solo deployment Azure, usato per il servizio reale con `ASPNETCORE_ENVIRONMENT=Production` (nonostante l’eventuale nome “dev” dato all’ambiente Azure); `Development` resta locale. I job amministrativi, incluso `--job account-erasure`, riusano lo stesso eseguibile API e la stessa OCI image, sono one-shot e vengono avviati da uno scheduler ACA esterno all'API. Fa eccezione il drainer dell'outbox Blob del Journal, ospitato nell'API secondo la decisione esplicita descritta sotto.
>
> ⚠️ Queste scelte sono di strategia e non implicano il rilascio dell'applicazione: il template Bicep è presente e il 2026-10-20 sono state create le risorse fondamentali (Log Analytics, Key Vault, managed identity, ambiente ACA, database SQL e storage Blob) in `rg-roamly-dev`. Il primo tentativo di creare SQL è fallito durante la configurazione dell'amministratore Entra; la soluzione approvata è SQL Authentication con password amministrativa in Key Vault. L'allowlist ACA non è disponibile dall'API dell'ambiente; è stata approvata una regola SQL "Allow Azure services" e la rete pubblica Key Vault con autenticazione RBAC. Runtime e workflow di rilascio restano da completare.
>
> ✅ **Perimetro di accesso scelto dall'utente (2026-10-06):** ingress pubblico ACA diretto, senza WAF o gateway gestito, con accesso tramite login Roamly; registrazione Production chiusa. Pagina di login e API saranno raggiungibili da Internet. Il limiter di login a finestre fisse su Azure SQL è implementato con profilo Balanced e fail-closed (dettagli in `SECURITY.md` §1.2); il cap IP, il bucket account per soli utenti esistenti e la pulizia a batch nel job `account-erasure` sono implementati. Il limite account restituisce `401` generico e usa l'hash fittizio; il `429` è riservato al limite IP. Il limiter non fornisce una barriera contro attacchi volumetrici. Restano da configurare gli alert e l'ambiente Production; ACA e le risorse di base Azure sono state create, ma non è stata pubblicata alcuna app.
>
> ✅ **Azure SQL Serverless per uso personale low-cost (2026-10-07):** configurare l'auto-pause dopo **15 minuti** di inattività. La connection string `ConnectionStrings:Roamly` di ACA deve includere `Connect Timeout=60` (minimo 60 secondi): al primo accesso dopo la pausa il database deve riprendere e il resume può richiedere **30–60 secondi**; durante il resume Azure SQL può restituire l'errore transitorio **40613**. Il `SqlConnection.RetryLogicProvider` applicativo usa retry esponenziale bounded **solo nell'apertura della connessione** (`NumberOfTries=5`, `DeltaTime=1s`, `MaxTimeInterval=20s`). Non abilitare `EnableRetryOnFailure` né retry su `SqlCommand`: comandi e transazioni esplicite non vengono ripetuti; gli errori in quelle fasi devono propagarsi senza replay dell'operazione. Questa è una configurazione richiesta al provisioning, non una risorsa già configurata.
>
> ✅ **Compute e storage approvati dall'utente (2026-10-07):** Azure SQL Serverless General Purpose Gen5, minimo **0,5 vCore**, massimo **1 vCore**, dimensione massima dati **1 GB**. Il limite minimo è scelto per l'uso personale; i documenti conservano in SQL una `StorageKey`, non i byte dei file. Per General Purpose lo storage è fatturato in base alla dimensione massima configurata, non solo allo spazio usato, e si aggiunge il costo del log (30% del massimo dati). Il limite va monitorato: al suo esaurimento le scritture possono fallire finché non viene aumentato. La feature file resta fuori dall'MVP; se in futuro i binari fossero memorizzati in SQL, la scelta va rivalutata. Minimo/massimo vCore e storage sono configurazioni approvate, **non risorse Azure già create**.
>
> ✅ **Autenticazione SQL approvata dall'utente (2026-10-20):** usare SQL Authentication per non richiedere un account Entra amministratore separato. Una password amministrativa forte è conservata nel Key Vault e non deve essere usata dal runtime; l'applicazione userà un principal SQL distinto e limitato. L'identità managed runtime non riceve accesso al secret amministrativo; assegnare accessi Key Vault solo ai singoli segreti runtime necessari. La modalità di autenticazione non aggiunge costi Azure, ma richiede gestione e rotazione sicura delle credenziali. Entra-only authentication non è attiva.
>
> ✅ **Autenticazione SQL del runtime scelta dall'utente (2026-10-22):** utente SQL contenuto nel database e connection string completa (`ConnectionStrings:Roamly`, incluse le credenziali) conservata in un secret Key Vault separato, referenziato dalla Container App. Il runtime non userà il login amministrativo; l'identità runtime potrà leggere solo il secret necessario e GitHub Actions non avrà accesso al suo valore. Principal, secret e referenza ACA non sono ancora creati/configurati; il bootstrap deve restare separato dalle migration dell'applicazione.

### Stato operativo: preparazione locale release — 2026-10-22

La preparazione autorizzata è **solo locale**. Non è stato eseguito alcun comando di provisioning/deploy Azure, non sono state create identità/assegnazioni/risorse e non sono stati letti o scritti valori di segreti. Il gruppo `rg-roamly-dev` è stato inventariato in sola lettura: esistono ACA Environment, SQL server/database, Key Vault, Blob, Log Analytics e managed identity runtime; non esistono Container App, ACR o workflow di deploy. Il database `roamly` risulta Paused e il server SQL Ready. I nomi delle risorse contengono `prod` nel gruppo `dev`; verificare sempre gli ID effettivi e il `what-if`, senza rinominare o ricreare risorse per deduzione.

Scelte esplicite:

- IaC **Bicep**; GitHub Actions con federazione **OIDC**, senza credenziali Azure statiche.
- Due principal OIDC distinti: bootstrap infrastruttura e release app. Il principal runtime ACA resta separato.
- ACR **Basic**, admin account disabilitato; push CI via OIDC, pull ACA tramite managed identity runtime (`AcrPull`).
- SQL Authentication con contained user nel database. La Container App riceverà una referenza Key Vault al secret runtime; GitHub non riceverà né leggerà il valore. L'applicazione espone la configurazione `ConnectionStrings__Roamly`, che deve risolversi a una connection string completa (inclusi principal/password) con `Connect Timeout=60`.
- ACA Consumption, scale-to-zero, Italy North, un solo deployment `ASPNETCORE_ENVIRONMENT=Production`, ingress pubblico diretto e registrazione Production chiusa.
- Per l'alpha privato approvato il 2026-10-22: massimo iniziale di 3 account totali incluso il titolare, nominativi e provisionati manualmente; la registrazione resta chiusa. Dati camper/viaggio/Journal sintetici per tutti; email reale ammessa solo come identificativo di login e da proteggere come dato personale. La decisione non autorizza provisioning o rilascio.
- Maps resta il mock dev esistente: nessuna risorsa, SDK, endpoint, adapter o chiamata Maps viene aggiunta.

Artefatti locali preparati:

- `infra/container-app.bicep` descrive **solo** la Container App da aggiungere successivamente, fa riferimento a environment, registry, Key Vault e managed identity preesistenti, non crea ruoli, identità, vault o registry e non legge valori Key Vault. I parametri senza default rendono esplicite le scelte ancora aperte; minReplicas è fissato a 0 secondo la decisione scale-to-zero. Sono mappati `ASPNETCORE_ENVIRONMENT=Production`, le referenze Key Vault per `ConnectionStrings__Roamly` e HMAC login, configurazione Blob e probe `/health/live` + `/health/ready`.
- `infra/main.bicep` è un file locale preesistente dell'utente, con ACR Basic, admin disabilitato e ruolo AcrPull per la MI runtime; è stato lasciato intatto. Non implica che tali risorse siano state create in Azure.
- `infra/workflows/README.md` distingue i futuri workflow di bootstrap e release e documenta i gate. Non è stato aggiunto alcun file workflow attivo in `.github/workflows/`.

**Gate HIGH — nessun workflow eseguibile finché non risolti e approvati:**

| Blocco | Da decidere/verificare prima di rendere attivabili i workflow |
|---|---|
| GitHub Environments | Nome per bootstrap e release, reviewer/approvazione manuale, regole di branch/ref e configurazione protetta effettiva. Il nome da solo non garantisce la protezione. |
| OIDC e RBAC | Client ID/tenant/subscription come variabili non segrete; subject federati esatti e ruoli/scope minimi distinti per le due identità. Nessun ruolo ampio viene assegnato per supposizione. |
| Provisioning IaC | `main.bicep` richiede un parametro SQL admin `@secure()`. Definire come usare/aggiornare risorse esistenti senza trasferire la password a GitHub; il workflow bootstrap non può passare credenziali SQL o leggere segreti. |
| Principal SQL e Key Vault | Procedura controllata per creare il contained user e comporre/conservare la connection string completa nel secret runtime SQL; la forma del secret è stata approvata dall'utente e corrisponde al contratto attuale dell'app. Concedere alla sola MI runtime accesso ai singoli secret necessari. Anche il secret HMAC, obbligatorio per il limiter, deve essere referenziato senza mostrarne il valore. |
| Migration | Ordine e procedura di bootstrap, migration da applicare, identità/ruolo SQL esecutore, backup e rollback. Le migration non devono essere applicate dal workflow di release né all'avvio API; verificato il codice attuale: non invoca `Database.Migrate()`. `AddDataProtectionKeys` e `AddLoginAttemptCounters` restano prerequisiti database/runtime. |
| Capacità/costi ACA e ACR | CPU, memoria, massimo repliche e timing/failure threshold dei probe non sono scelti e sono parametri obbligatori del template; il probe readiness interroga SQL e va valutato sul cold resume di 30–60 s. Verificare anche il costo corrente ACR Basic prima di qualsiasi provisioning. |
| Configurazione effettiva | Verificare resource ID/naming nel gruppo esistente, secret versionless e RBAC, impostazioni Blob verificate, image/tag, ingress e readiness con un `what-if`/review separato. Nessuna verifica remota è stata fatta durante questa preparazione. |

Il `Dockerfile` attuale pubblica API .NET 10 e frontend in un'unica immagine; il runtime ASP.NET Core ascolta sulla porta 8080 e `Program.cs` espone `/health/live` (senza DB) e `/health/ready` (con verifica SQL). Gli endpoint di registrazione e resend conferma sono mappati solo in Development. La configurazione di readiness deve tenere conto del database Paused e del suo resume, senza assumere soglie non approvate.

Gli ID OIDC (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`) saranno variabili GitHub, non secret. Nessun PAT, SQL password o valore Key Vault va inserito in GitHub o nel repository. Quando i gate saranno chiusi, bootstrap e release avranno solo trigger manuale `workflow_dispatch`, senza `push`/PR/schedule e con l'environment protetto sul job che accede ad Azure. Fino ad allora non va creato o spostato alcun workflow sotto `.github/workflows/`.
>
> ✅ **Rete Azure approvata dall'utente (2026-10-20):** per evitare costi fissi di NAT/Private Endpoint, Azure SQL usa il firewall `0.0.0.0` ("Allow Azure services") e Key Vault consente l'accesso da rete pubblica con Entra/RBAC. La regola SQL comprende risorse Azure non limitate alla subscription o all'ambiente Roamly; Key Vault resta autenticato e autorizzato tramite RBAC. La decisione amplia il perimetro di rete e va rivalutata prima di aggiungere utenti o dati sensibili. Nessuna allowlist specifica ACA è configurata.
>
> Riferimenti: [auto-pause/resume Azure SQL](https://learn.microsoft.com/en-us/azure/azure-sql/database/serverless-tier-auto-pause-resume?view=azuresql), [limiti di storage e tier vCore](https://learn.microsoft.com/en-us/azure/azure-sql/database/service-tiers-sql-database-vcore?view=azuresql) e [retry configurabile SqlClient](https://learn.microsoft.com/en-us/sql/connect/ado-net/configurable-retry-logic-sqlclient-introduction?view=sql-server-ver17).
>
> Vincoli derivati:
> - il target deve supportare il runtime **.NET 10** (vedi `ARCHITECTURE.md` §1);
> - deve offrire **SQL Server o Azure SQL**. Azure SQL Database supporta i tipi spaziali, quindi l'upgrade previsto da `ADR-0001` resta possibile;
> - **Azure SQL Edge è escluso**: ritirato a settembre 2025;
> - ✅ **Vincolo same-site di [`ADR-0003`](../adr/0003-auth-and-ownership.md) soddisfatto dalla topologia approvata (2026-10-07):** la stessa app ASP.NET Core serve il bundle React/Vite al root e mantiene le API sotto `/api`. I cookie restano `Secure` + `SameSite=Lax`; il fallback SPA non intercetta percorsi `/api` sconosciuti. Il `Dockerfile` multi-stage e lo static hosting sono implementati e verificabili localmente.
> - ✅ **Politica delle immagini base approvata dall'utente (2026-10-07):** mantenere i tag mobili `node:22-alpine`, `mcr.microsoft.com/dotnet/sdk:10.0` e `mcr.microsoft.com/dotnet/aspnet:10.0`, ricostruendo e rilasciando almeno mensilmente e prima in caso di avviso di sicurezza rilevante. Il rebuild può usare immagini diverse anche per lo stesso commit; un tag aggiornato non aggiorna un container già distribuito. `global.json` resta pinning per lo sviluppo locale; solo lo stage Docker rimuove il file perché l'esatto SDK `10.0.204` non è disponibile nei feed ufficiali. La cadenza resta manuale finché la pipeline release non sarà implementata; nessuna automazione è stata aggiunta.
> **Stato:** le risorse fondamentali Azure, il database SQL e lo storage Blob sono stati creati in `rg-roamly-dev`; sono configurati SQL Authentication, la regola Azure-services e gli endpoint pubblici SQL, Key Vault e Blob con autenticazione/RBAC. L'account Blob `storoamlywse7wit7fzpja` e il container privato sono stati verificati il 2026-10-20; non è stato ancora eseguito alcun upload. Non è stata pubblicata alcuna app né applicata alcuna migration; principal SQL runtime, permessi secret-scoped e workflow restano da configurare.

### 2.3 Gestione segreti

Da definire: dove vivono i segreti per ambiente e come avviene il mapping environment → configurazione. Nessun segreto nel repository, in nessuna forma.

Due elementi nuovi introdotti da [`ADR-0003`](../adr/0003-auth-and-ownership.md):

- ✅ **Key ring di Data Protection condiviso e persistito — strategia approvata 2026-10-06.** `RoamlyDbContext` usa `PersistKeysToDbContext` e `ApplicationName("Roamly")` per conservare il key ring nella tabella `DataProtectionKeys` dello stesso Azure SQL, senza introdurre un servizio storage separato. In Azure SQL i dati sono protetti at-rest dalla TDE gestita da Microsoft; TDE non protegge da principal autorizzati a leggere il database. La migration `AddDataProtectionKeys` deve essere applicata dal bundle controllato **prima** di attivare il runtime che usa questo repository. L’unico deployment Azure previsto ora usa `ASPNETCORE_ENVIRONMENT=Production` anche se l’ambiente Azure viene chiamato “dev”; lo sviluppo locale resta `Development`. Prima di aggiungere un secondo ambiente Azure, separare database o application discriminator: i key ring condivisi permettono di decifrare i cookie emessi dall’altra installazione.
- **Provider email** (conferma account e reset password): ancora da scegliere e configurare. Finché non esiste un mittente reale, `POST /api/v1/auth/register` è disponibile solo in `Development`; non riabilitarlo in `Production` con `LoggingEmailSender`, che non consegna i messaggi e registra il token di conferma nel body.
- ✅ **Retention del sink di logging approvata dall'utente:** Log Analytics Analytics con **30 giorni configurati**, raccogliendo log console/sistema ACA e lasciando disattivati gli HTTP logs al baseline. La disponibilità effettiva può arrivare a 31 giorni; la configurazione di purge immediato non è stata scelta. Il sink e il workspace **non sono ancora configurati**. I log di sicurezza contengono identificatori utente e **non vivono su database**; questa retention resta l'unico requisito dell'impianto privacy non verificabile da un test.
- ✅ **Backup Azure SQL approvati 2026-10-06:** PITR di **7 giorni**, senza Long-Term Retention (LTR), con ridondanza **LRS** per minimizzare costi e mantenere le copie nella regione primaria. Azure SQL gestisce i backup automaticamente; un restore a un punto precedente può ripristinare dati già cancellati e richiede di rieseguire l'erasure prima di rimettere il database in servizio. LRS non offre geo-restore: in caso di perdita regionale non c'è ripristino dei backup in un'altra regione. La scelta va verificata in creazione/configurazione Azure, perché il default per nuovi database è GRS. Per i dettagli di PITR, storage backup e restore vedere [Microsoft Learn](https://learn.microsoft.com/en-us/azure/azure-sql/database/automated-backups-overview?view=azuresql) e [recovery using backups](https://learn.microsoft.com/en-us/azure/azure-sql/database/recovery-using-backups?view=azuresql).

### 2.5 Requisiti di runtime introdotti da ADR-0004

- scheduler ACA esterno per `AccountErasureJob`: **strategia scelta, configurazione non presente**; l'utente ha approvato il riuso di questa esecuzione giornaliera per ripulire i contatori login scaduti (integrazione e schedule non configurati);
- la transazione e il lock `UPDLOCK, HOLDLOCK` sulla stessa riga `AspNetUsers` serializzano revoca/job e invocazioni concorrenti sul medesimo utente;
- **health check/allerta** su richieste di cancellazione scadute non processate: manca ancora la soglia da concordare e la configurazione;
- key ring Data Protection: **decisione e implementazione completate**, ma migration/release non eseguite su Azure; **migration release pipeline** e configurazione del sink di logging restano da eseguire. La retention Log Analytics a 30 giorni è approvata. Backup automatici: PITR 7 giorni, niente LTR, storage LRS; dopo un restore occorre rieseguire le erasure successive al punto ripristinato.

### 2.6 Requisiti di frontend introdotti da ADR-0007

- 🔴 **Nessuna CDN di terzi** (R19). I **font sono self-hosted**, serviti dallo stesso origin del bundle. È un vincolo di **privacy**: una CDN esterna espone l'IP di ogni utente a un terzo a ogni caricamento. Va **verificato in CI**, non solo scritto.
- **Cache header**: bundle statico con hash nel nome e `Cache-Control: immutable`; font `immutable`; `index.html` **mai** cacheable, altrimenti un deploy non raggiunge i client.
- **Budget dei font ≤ 120 KB** totali (R22), con subset latino e solo l'asse `wght` di Fraunces. È un **vincolo di build**, non un'aspirazione: la pipeline deve misurarlo e fallire se sforato.
- **Tre verificatori di design system in pipeline**, accanto a quelli di ownership e privacy: matrice di contrasto sulle **47 coppie** su **entrambi i temi** (R14), copertura del manifesto dei token, `axe` eseguito due volte (tema chiaro e scuro).

> La dark mode è in Phase 1 per scelta esplicita: senza il verificatore di contrasto in CI, la matrice a due temi **degrada in silenzio**. Nessuno rilegge 47 coppie a mano a ogni modifica di token.

### 2.4 Caching

Caching di NuGet e npm nella CI. Senza, la pipeline diventa lenta e si smette di guardarla — che equivale a non averla.

> ⚠️ **La cache dell'immagine SQL Server è da misurare prima di introdurla.** Il pull dell'immagine (~1,5 GB) è la voce dominante nei 60-100 s del job `integration`, ma una cache Docker su GitHub Actions ha un costo di ripristino proprio che può annullare il beneficio. È una misura, non un'ovvietà: si introduce se i numeri la giustificano.

---

## 3. Environment

| Ambiente | Scopo | DB |
|---|---|---|
| local | sviluppo | `mcr.microsoft.com/mssql/server` (Linux) in Docker, servizio `mssql` |
| **test** | **effimero**, creato e distrutto da Testcontainers a ogni run | immagine **pinnata** `2022-CU<xx>-ubuntu-22.04`, mai `latest` ([`ADR-0009`](../adr/0009-test-strategy.md), **R36**) |
| _(staging?)_ | da decidere con #9 | |
| production | | SQL Server o Azure SQL (vincolo da `ADR-0001`) |

Requisiti del container SQL Server in locale:

- **minimo 2 GB di RAM** per container;
- **healthcheck obbligatorio**: il cold start non è istantaneo (~10-45 s) e l'API non deve partire prima che il DB accetti connessioni;
- il supporto spaziale è già incluso nell'immagine, nessun componente aggiuntivo da installare;
- 🔴 **ARM64 non ha oggi alcun percorso pulito.** L'affermazione "irrilevante, si sviluppa su Windows x64" è vera ma incompleta: **Azure SQL Edge era l'unica via ARM64 ufficiale, ed è stato ritirato il 30/09/2025.** Non è più un vincolo da tenere presente in futuro — è un **vincolo hardware attuale**: una macchina di sviluppo Apple Silicon o Windows-on-ARM non può eseguire la suite di integration test. Va detto prima di comprare la macchina, non dopo (trigger **T4** di ADR-0009).

---

## 5. Job pianificati

| Job | Cadenza | Note |
|---|---|---|
| **Provisioning iniziale utente** | manuale, una tantum | Eseguire localmente `dotnet run --project src/Roamly.Api -- --job provision-user` con `ConnectionStrings:Roamly` configurata. Prompt interattivi; password nascosta, ripetuta per conferma e mai passata negli argomenti. Non è un endpoint né un job schedulato/deployato; il comando rifiuta input non interattivo. L'utente resta `EmailConfirmed=false`, ma il login è ammesso solo grazie al marker Identity assegnato da questo job. |
| **`AccountErasureJob`** | giornaliero, da configurare | `--job account-erasure`, stesso eseguibile e stessa OCI image dell'API; chiamata one-shot dallo scheduler ACA esterno. `RunAsSystem` per polling candidati, receipt e delete Identity; `RunAsUser(userId)` esclusivamente per lo sweep owner-scoped. Una transazione EF contiene lock, sweep, verifica, receipt e delete finale; ordine figli→padri derivato da tutte le FK owned del modello. `DbUpdateException`, `DbException` e `InvalidOperationException` vengono registrate per candidato senza bloccare i successivi; al termine il processo segnala allo scheduler un errore aggregato. Ripulisce inoltre i contatori login scaduti a batch di 1.000 fino a esaurimento; un errore viene aggregato dopo il processing delle erasure. Implementato e testato su SQL Server migrato Phase 1; non schedulato/deployato |
| **`JournalAttachmentDeletionOutboxJob`** | all'avvio API e ogni 5 minuti mentre è attiva | `BackgroundService` minimo nell'host HTTP: drena subito gli intenti SQL e poi a intervalli di 5 minuti, creando uno scope per esecuzione. Un errore viene registrato e lascia gli intenti nell'outbox per il tentativo successivo; l'arresto dell'host cancella l'esecuzione corrente. Il comando one-shot `--job journal-attachment-deletion-outbox` resta disponibile e non registra il servizio periodico. Se l'API è ferma o in scale-to-zero, il retry attende la successiva attivazione dell'API; non è previsto uno scheduler o un worker separato per questo drainer. |
| **`MaintenanceReminderJob`** | giornaliero | Anch'esso sotto `RunAsUser` |

**Requisito di allerta:** un health check deve segnalare l'esistenza di richieste di cancellazione **scadute e non processate** oltre una soglia. Un job fermo senza allerta è indistinguibile da un job che funziona.

> **Non esiste** un `SecurityLogPurgeJob`: gli eventi di sicurezza non stanno su database, la loro retention è configurata nel sink (§2.3).
