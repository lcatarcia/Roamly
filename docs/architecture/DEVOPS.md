# Roamly — CI/CD e deploy

Stato: **bozza — target di deploy non dichiarato** · Ultimo aggiornamento: 2026-09-24
Origine: `docs/archive/Roamly_Planning_2026-09-24.md` §20 — **rivisto** secondo review I10
Owner: **@vulcan**

---

## 1. Pipeline

```text
Push / Pull Request
        │
        ▼
   Checkout
        │
   ┌────┴────┬──────────┐
   ▼         ▼          ▼
Backend   Frontend   Docker
Restore   Install    Build
Build     Lint
Test      Test
          Build
   └────┬────┴──────────┘
        ▼
      Deploy (workflow separato)
```

### Workflow

Il planning originale proponeva 5 workflow (`ci.yml`, `backend.yml`, `frontend.yml`, `docker.yml`, `deploy.yml`). All'inizio è costo di manutenzione senza beneficio.

**Struttura adottata:**

- **`ci.yml`** — un workflow con **quattro job**: `fast` (L0: build + test senza database, **< 60 s, nessun Docker**), `integration` (L1: Testcontainers + test dello schema completo), `frontend`, `docker`;
- **`nightly.yml`** — suite completa, bundle di migration, `axe` di accessibilità;
- **`deploy.yml`** — separato, con approvazione.

Si splitta solo quando la CI diventa effettivamente lenta o eterogenea.

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

### 2.2 Target di deploy

> ⚠️ **Decisione aperta #9, severità MEDIUM.**
>
> Nessun target è dichiarato. Senza questo, Docker ed "environment separation" sono decorativi.
> Opzioni: Azure App Service, Azure Container Apps, VPS con Docker.
> Vincoli derivati:
> - il target deve supportare il runtime **.NET 10** (vedi `ARCHITECTURE.md` §1);
> - deve offrire **SQL Server o Azure SQL**. Azure SQL Database supporta i tipi spaziali, quindi l'upgrade previsto da `ADR-0001` resta possibile;
> - **Azure SQL Edge è escluso**: ritirato a settembre 2025;
> - 🔴 **vincolo bloccante da [`ADR-0003`](../adr/0003-auth-and-ownership.md): frontend e API devono stare sullo stesso sito** (stesso dominio, API sotto `/api`, oppure reverse proxy). L'autenticazione a cookie con `SameSite=Lax` **non funziona** con frontend e API su domini separati: richiederebbe `SameSite=None`, che riapre il CSRF e dipende dalle policy dei browser sui cookie di terze parti.
>   Se la #9 dovesse imporre domini separati, **la Parte A dell'ADR-0003 va rinegoziata prima del deploy**, non dopo.

### 2.3 Gestione segreti

Da definire: dove vivono i segreti per ambiente e come avviene il mapping environment → configurazione. Nessun segreto nel repository, in nessuna forma.

Due elementi nuovi introdotti da [`ADR-0003`](../adr/0003-auth-and-ownership.md):

- 🔴 **Key ring di Data Protection condiviso e persistito.** Se il deploy è multi-istanza (o anche solo ricreato a ogni deploy), senza un key ring condiviso — Azure Blob, filesystem persistente, Redis — **i cookie di autenticazione si invalidano a ogni deploy e a ogni scale-out**. Si manifesta come "logout casuali" e viene diagnosticato malissimo. Va configurato **insieme** alla #9, non dopo la prima segnalazione.
- **Credenziali del provider email** (conferma account e reset password). La scelta del provider è un gap aperto tracciato in `OPEN-DECISIONS.md`.
- 🔴 **Retention del sink di logging** ([`ADR-0004`](../adr/0004-privacy-and-erasure.md)): gli eventi di sicurezza contengono identificatori utente e **non vivono su database**, quindi la loro retention è configurazione di deploy. È **l'unico requisito dell'impianto privacy non verificabile da un test**: dichiarato come debolezza, non nascosto.
- **Finestra di backup** del database e del provider: sopravvive alla cancellazione dell'account, va **misurata** e allineata ai 30 giorni di grazia (vincolo F5).

### 2.5 Requisiti di runtime introdotti da ADR-0004

- **scheduler affidabile** per `AccountErasureJob` — vedi §5;
- comportamento del job in scenario **multi-istanza**: non deve girare due volte in parallelo sullo stesso utente (il job è idempotente, ma va comunque deciso);
- **health check** su "richieste di cancellazione scadute non processate". Oggi non esiste: senza, un job fermo è invisibile finché non se ne accorge l'utente.

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
| **`AccountErasureJob`** | giornaliero | Seleziona gli utenti con `DeletionScheduledForUtc` scaduta e cancella in **ordine topologico derivato dal modello** (`DATA.md` §6). Gira sotto **`RunAsUser(userId)`**, non `RunAsSystem`: così un bug produce una cancellazione incompleta — che R9 rileva — non la cancellazione dei dati di un altro utente. Deve essere **idempotente** (F7) |
| **`MaintenanceReminderJob`** | giornaliero | Anch'esso sotto `RunAsUser` |

**Requisito di allerta:** un health check deve segnalare l'esistenza di richieste di cancellazione **scadute e non processate** oltre una soglia. Un job fermo senza allerta è indistinguibile da un job che funziona.

> **Non esiste** un `SecurityLogPurgeJob`: gli eventi di sicurezza non stanno su database, la loro retention è configurata nel sink (§2.3).
