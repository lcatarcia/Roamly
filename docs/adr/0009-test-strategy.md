# ADR-0009 — Strategia di test: SQL Server vero come unico motore, e 17 verificatori su 25 che non lo toccano

- **Stato:** Accettato
- **Data:** 2026-09-25
- **Owner:** @argus
- **Decisore:** utente
- **Severità:** MEDIUM (con conseguenze HIGH su CI/CD e sulla sequenza di lavoro)
- **Chiude:** decisione **#6** — provisioning del database di test
- **Dipende da:** [`ADR-0001`](0001-use-sql-server.md) (SQL Server), [`ADR-0003`](0003-auth-and-ownership.md) (R1-R7), [`ADR-0004`](0004-privacy-and-erasure.md) (R8-R10), [`ADR-0007`](0007-design-system.md) (R11-R25), [`ADR-0008`](0008-primary-key-strategy.md) (PK composita, R26-R29)

---

## Contesto

### Perché la #6 è diventata un prerequisito tecnico e non una MEDIUM da chiudere "prima della Phase 1"

La #6 nasce come una domanda di comodità — *come si ottiene un database per gli integration test?* — e in quella forma sarebbe rimandabile. È stata promossa a **prima attività tecnica del progetto** il 2026-09-24, per una ragione che non ha nulla a che vedere con la comodità:

> **La topologia delle 21 foreign key di `CONTEXT.md` §5 è corretta sulla carta e non è dimostrata.** Entrambi i casi noti di errore SQL Server **1785** (*multiple cascade paths*) coinvolgono entità di **Phase 2/3/4**. Nessuno dei due emerge dalla prima migration. Senza un SQL Server reale su cui costruire lo schema **completo**, si scoprirebbero fra mesi — con codice applicativo già scritto sopra la topologia sbagliata.

A questo si sono sommati, nell'ordine, tre vincoli che hanno reso la decisione non più rinviabile:

| Origine | Vincolo posto sulla #6 |
|---|---|
| **ADR-0003** | R3 (scrittura cross-owner rifiutata) e R4 (FK composite che impediscono la divergenza di `OwnerId`) richiedono un motore che **crei davvero** quelle constraint |
| **ADR-0004** | R9 *"erase and sweep"* è verificabile **solo** con FK attive e `DELETE` reali; e i **test data builder** devono coprire ogni entità owned, altrimenti R8 torna a dipendere dalla memoria |
| **ADR-0008** | la PK composita `(OwnerId, Id)` CLUSTERED e l'**assenza totale di chiavi alternate** sono un'affermazione sullo schema fisico: o è verificata su `sys.indexes` e `sys.key_constraints`, o è un'intenzione |

Lo stato del repository alla data di questo ADR è: **`docs/`, `AGENTS.md`, `README.md`**. Zero righe di codice, nessuna `.sln`. Questa decisione non ottimizza una suite esistente: **decide la forma della prima riga di codice di test che verrà scritta**, e quindi la sequenza del primo giorno.

### Vincoli reali

Sviluppatore singolo, nessun team, nessun budget per infrastruttura di test dedicata, hardware x64 Windows con Docker Desktop, CI su GitHub Actions con repository privato (minuti contati, runner Windows a costo doppio). Il vincolo che ha pesato più di tutti:

> **Una sola persona non può mantenere due percorsi di provisioning.** Qualunque soluzione che richieda "questo in locale, quest'altro in CI" degrada entro settimane in *"funziona sulla mia macchina"*, e il tempo speso a riallinearla è tempo sottratto al prodotto. Fedeltà e velocità sono criteri; **unicità del percorso è un vincolo.**

E il suo corollario: **una CI che supera i 5 minuti si smette di guardare.** Un verificatore che nessuno guarda non è un verificatore.

---

## Fatti verificati, non verificati e smentiti

### ✅ Verificati

| # | Fatto | Fonte |
|---|---|---|
| F-1 | **Testcontainers per .NET è vivo e maturo**: modulo `Testcontainers.MsSql` alla **4.15.0** (set. 2026), .NET 6/7/8 + .NET Standard 2.0 | NuGet `Testcontainers.MsSql`; releases `testcontainers/testcontainers-dotnet` |
| F-2 | **Azure SQL Edge è stata ritirata il 30 settembre 2025.** Nessun percorso di migrazione diretto; Microsoft indirizza a SQL Server Express/Standard o SQL MI | Azure Updates, *Azure SQL Edge retirement* |
| F-3 | **Non esiste immagine SQL Server ufficiale per ARM64**, né 2022 né 2025. Solo AMD64. Emulazione QEMU possibile, lenta, sconsigliata | Docker Hub `microsoft/mssql-server` |
| F-4 | **SQL Server 2025 è GA dal 18 novembre 2025**, supporto mainstream fino al 7 gennaio 2031; Developer edition gratuita per sviluppo e test | Microsoft Lifecycle; SQL Server downloads |
| F-5 | **Respawn 7.0.0**: adapter SQL Server, ordine di `DELETE` **derivato dalle FK**, `TablesToIgnore` per `__EFMigrationsHistory` | GitHub `jbogard/Respawn`; NuGet |
| F-6 | I runner **`ubuntu-latest`** eseguono SQL Server in container senza attriti (Docker preinstallato). I runner **Windows non eseguono container Linux** in modo affidabile | Docs GitHub Actions, service containers |
| F-7 | Readiness di SQL Server in container: **20-45 s** tipici, fino a 2 min; serve wait strategy o healthcheck — Testcontainers lo fa nativamente | `DEVOPS.md` §3 + misure pubbliche |
| F-8 | **xUnit v3 è stabile** (Core Framework 3.4.0, ago. 2026): **Assembly Fixtures**, parallelismo a grana fine, integrazione nativa **Microsoft.Testing.Platform**. **Su .NET 10 MTP è il default e VSTest non è più supportato ufficialmente** | xunit.net releases v3; docs MTP |
| F-9 | **EF Core in-memory è ufficialmente sconsigliato per i test**: niente FK, niente unique, niente cascade, niente transazioni, niente SQL raw → falso verde | Microsoft Learn, *In-memory Database Provider* |
| F-10 | **SQLite**: `decimal` su affinità NUMERIC (testo/float, arrotondamento silenzioso); nessun `rowversion`; uno schema solo; FK da abilitare con `PRAGMA foreign_keys = ON` | Microsoft Learn |

### ⚠️ Non verificati — dichiarati come stime

| # | Stima | Stato |
|---|---|---|
| F-11 | **SQLite non produce l'errore 1785** né alcun equivalente | **deduzione**, non fonte in negativo. La conclusione operativa non cambia: anche se esistesse un controllo analogo, non sarebbe lo stesso |
| F-12 | `CREATE DATABASE` vuoto ~150-400 ms · `EnsureCreated` schema completo ~1-3 s · `Respawn.ResetAsync()` ~50-200 ms · migration Phase 1 ~1-2 s | **stime di ordine di grandezza. Da misurare al Blocco 4 del primo giorno e riportare in `TESTING.md` §10.** Un documento che dichiara tempi mai misurati invecchia male |
| F-13 | Cachare l'immagine Docker di SQL Server con `actions/cache` **non conviene** rispetto al pull da MCR | **ipotesi** basata sulla taglia (~700 MB). Da misurare prima di implementare, se e quando T2 scatta |

### ❌ Le tre smentite

Sono il contenuto informativo di questo ADR. Tutte e tre contraddicono documenti già scritti e approvati.

#### Smentita 1 — «R3, R4 e R7 sono verificabili solo con un database reale» (`TESTING.md` §4, nota da ADR-0003)

**Parzialmente falso, e la parte falsa è la maggioranza del costo.**

- **R7 non è una regola di database.** È un **contratto HTTP**: una risorsa di un altro proprietario deve produrre `404` con un `ProblemDetails` **byte-identico** a quello di un id inesistente. Non dipende dal provider, dipende dall'endpoint. Il database serve solo perché l'id dell'altro utente deve esistere davvero — condizione soddisfatta dal container già acceso per altri motivi, non un requisito proprio di R7.
- **R4 è in gran parte una regola di forma del modello.** «Ogni FK figlia è composita e include `OwnerId`, e punta alla PK `(OwnerId, Id)` del padre» si legge su `DbContext.Model` in ~5 ms, senza connessione. Il **morso** della constraint — l'`INSERT` con `OwnerId` divergente che viene rifiutato — va dimostrato su SQL Server **una volta sola**, non a ogni slice.
- **Resta R3**, che richiede davvero il database per ogni caso: `SaveChanges` deve arrivare fino in fondo perché l'interceptor di ownership sia esercitato nella sua posizione reale.

> **Conseguenza:** la suite costa **meno** di quanto i documenti attuali lascino credere, e `OPEN-DECISIONS.md` contiene una riga derivata dalla #2 che va corretta. Vedi la checklist di propagazione.

#### Smentita 2 — `CONTEXT.md` §2.6 sottintende che il test dello schema completo sia scrivibile senza le entità Phase 2+

**Falso.** «Topologia decisa, tabella non creata» è stato letto come «nessun codice da scrivere oggi». È vero il contrario: per costruire uno `IModel` che contenga `Trip`, `TripStop`, `Checklist`, `ChecklistItem`, `JournalEntry`, `Expense`, `SavedPlace`, `Document`, **quelle classi C# devono esistere**. EF non può mappare un'entità che non è un tipo.

La scelta "tabelle non create in produzione" resta valida e non viene toccata: si separa **modello** da **migration**. Ma il prerequisito costa ~150 righe di POCO e va dichiarato, non scoperto al Blocco 2 del primo giorno.

#### Smentita 3 — il ritiro di Azure SQL Edge è più grave di come ADR-0001 e `DEVOPS.md` §3 lo registrano

Entrambi i documenti sono **corretti nel merito** e **sottostimati nella portata**. Oggi dicono, in sostanza: *"non esiste immagine ARM64 ufficiale — irrilevante, si sviluppa su x64"*.

Il punto che manca: **Azure SQL Edge era l'unica via ARM64 ufficiale**, e con F-2 quella via **non esiste più**. La combinazione F-2 + F-3 significa che oggi, su un Mac Apple Silicon o su Windows on ARM, **non c'è alcun percorso pulito** per questa strategia di test: restano l'emulazione QEMU (lenta, instabile, sconsigliata da Microsoft) o un SQL Server remoto condiviso (che reintroduce il secondo percorso vietato dal vincolo).

> Va promosso da nota a **vincolo hardware esplicito**: il cambio di architettura della macchina di sviluppo **riapre questo ADR per intero** (trigger T4). Non è un dettaglio operativo, è una dipendenza dell'intera strategia dall'hardware.

---

## Opzioni considerate

### Opzione A — Testcontainers `MsSql`, un container per run di assembly ⭐ **SCELTA**

Il container viene avviato dal codice di test, con wait strategy, e distrutto a fine run (Ryuk).

**Pro:** un **solo** percorso, identico su Windows locale e su `ubuntu-latest`. Fedeltà totale: 1785 emerge, le FK composite esistono, `decimal(19,4)` è `decimal(19,4)`, `rowversion` è `rowversion`, la PK CLUSTERED è leggibile da `sys.indexes`. Versione dell'immagine pinnata in un punto unico, quindi riproducibile e **diffabile in git**. Nessuna dipendenza da cosa è preinstallato sul runner.

**Contro:** **60-100 s fissi in CI** a ogni run (pull + readiness). Richiede Docker Desktop in locale (~2 GB di RAM occupati durante i test). **Inutilizzabile su ARM64** (smentita 3). Docker Desktop ha licenza commerciale sopra certe soglie aziendali — irrilevante per un progetto personale, ma vero.

### Opzione B — Testcontainers + LocalDB come acceleratore locale opt-in

**Pro:** ~1 s in locale a caldo; CI identica all'opzione A.
**Contro:** **due percorsi da tenere allineati**, con connection string e versione diverse. Il "verde in locale, rosso in CI" torna possibile. È precisamente ciò che il vincolo di unicità vieta.

### Opzione C — Container preesistente (docker compose in locale, `services:` in GHA) + Respawn

**Pro:** in CI il `services:` block si avvia **in parallelo** a checkout e restore: risparmio reale di 20-40 s. In locale il DB resta caldo: 0 s di avvio.
**Contro:** due configurazioni divergibili; **stato che sopravvive tra i run** e avvelena silenziosamente quelli successivi. Per una persona sola questo tipo di guasto è il più costoso da diagnosticare.

> Respawn viene comunque adottato **come meccanismo di reset** — è complementare, non alternativo. Ciò che si respinge è il container *preesistente* come modello di provisioning. `services:` resta disponibile come pura ottimizzazione di `ci.yml` quando T2 scatterà: è un cambio locale al workflow, non alla strategia.

### Opzione D — Solo LocalDB, CI su `windows-latest`

**Pro:** la più veloce in locale in assoluto, zero Docker.
**Contro:** raddoppia i minuti GHA su repository privato, runner più lenti ad avviarsi, si perde la fedeltà all'immagine Linux di `DEVOPS.md` §3, e **vincola per sempre lo sviluppo a Windows**. Bassa complessità, scelta strategicamente peggiore.

### Opzione E — SQLite in-memory — ❌ respinta, con motivazione sostituita

`TESTING.md` §4 la sconsiglia oggi per `decimal`, collation e `rowversion`. La conclusione è giusta; **la motivazione è la più debole disponibile** e va sostituita:

> **SQLite non può fallire con l'errore 1785**, perché non ha il concetto di molteplicità dei percorsi di azione referenziale. Il verificatore che giustifica l'urgenza di questa intera decisione, su SQLite, è **strutturalmente incapace di diventare rosso**. Un test che non può fallire non è un test: è una speranza compilata.

A cascata: `DeleteBehavior` non riproduce la semantica SQL Server → R4 e il grafo di `DATA.md` §6 diventerebbero verdi senza essere verificati; `decimal(19,4)` diventa affinità NUMERIC → gli invarianti di `Money` sono falso-verdi; `rowversion` non esiste → gli ETag di `API-CONVENTIONS.md` non sarebbero testati; e si introdurrebbe una **seconda dialettica di schema** da allineare a mano per sempre.

### Opzione F — EF Core in-memory — ❌ respinta, per niente, nemmeno dove sembrerebbe ovvia

Il punto che merita di essere scritto è che **non serve nemmeno per i test di convenzione**:

> Il provider in-memory **produce un `Model` diverso**: ignora gran parte dei metadati relazionali (delete behavior, precisione delle colonne, nomi delle constraint, layout della PK). I verificatori di R1/R2/R4/R8/R10/R26-R29 leggono **proprio quei metadati**. Verificarli su un modello in-memory significa verificare un modello che non va in produzione.

La forma corretta è gratuita e non richiede alcun provider finto: **costruire il modello con il provider SQL Server senza mai aprire una connessione.**

```csharp
// Nessun database, nessun container: ~200 ms per l'intero assembly.
var options = new DbContextOptionsBuilder<FullSchemaDbContext>()
    .UseSqlServer("Server=none;Database=none;")   // mai aperta
    .Options;
using var ctx = new FullSchemaDbContext(options);
IModel model = ctx.Model;                          // costruito offline, fedele
```

**È la scoperta più economica di tutta l'analisi:** la maggioranza dei verificatori di Roamly non ha bisogno di un database, ha bisogno del **modello** — e il modello è gratis.

---

## Decisione

1. **Un solo provider di database nei test: SQL Server vero.** Niente SQLite, niente EF in-memory, a nessun livello. → **R30**
2. **Provisioning: Testcontainers `MsSql`**, un container per run di assembly, posseduto da **un solo** progetto di test. Stesso identico percorso in locale e in CI. **Nessun percorso LocalDB**, nemmeno opt-in.
3. **Immagine pinnata**: `mcr.microsoft.com/mssql/server:2022-CUxx-ubuntu-22.04`, tag in un punto unico, mai `latest`. → **R36**
4. **Isolamento G1**: un solo database, tutti i test DB in una `[Collection]` unica, **Respawn** tra un test e l'altro. G2 (un DB per collection) è **pre-progettato** ma non attivato. Niente `TransactionScope`.
5. **Il test 1785 è due test**: un **analizzatore model-driven** (L0, ~10 ms, ogni push) e una **creazione reale dello schema completo** via `EnsureCreated` su SQL Server (L1, ~3 s, **ogni push**). Il secondo esiste per dimostrare che il primo dice il vero.
6. **I verificatori vivono al livello più economico che può davvero diventare rosso.** Esito della mappatura: **17 delle 25 regole R1-R25 non toccano alcun database.** → **R31**
7. **POCO strutturali delle entità Phase 2/3/4 in `Roamly.Domain`**, con PK composita di ADR-0008, registrati **solo** in `FullSchemaDbContext`, mai in `RoamlyDbContext`. Un meta-verificatore impedisce che il set diverga. → **R32**
8. **Quattro progetti di test**: `Roamly.Domain.Tests`, `Roamly.Model.Tests` (L0, **senza Docker**), `Roamly.IntegrationTests` (unico proprietario del container), `Roamly.ArchitectureTests` (più avanti).
9. **xUnit v3 + Microsoft.Testing.Platform**, con **Assembly Fixtures**.
10. **Test data builder model-driven**: `IEntityBuilder` + `BuilderRegistry` con ordinamento topologico derivato da `IModel`, e un verificatore di completezza bloccante. → **R33**
11. **`TimeProvider` iniettato dal primo commit**, `DateTime.UtcNow` bandito. → **R34**
12. **Antiforgery attivo nei test**, con l'estrazione del token **dentro `RoamlyClient`** e mai nel corpo del test. → **R35**
13. **Benchmark di @oracle**: stessa fixture parametrizzata (RAM, wait strategy, nessun Respawn), progetto `Roamly.Benchmarks`, **esecuzione solo locale, mai in CI**. → **R39**

---

## Regole invarianti — R30-R39

> R1-R7 in `SECURITY.md` §2.2 (ADR-0003) · R8-R10 (ADR-0004) · R11-R25 (ADR-0007) · **R26-R29 (ADR-0008, @oracle)**. Questo ADR numera da **R30**.

Vale il criterio di ADR-0004, invariato: **nessun requisito deve dipendere dalla memoria dello sviluppatore.**

| # | Regola | Verificata da |
|---|---|---|
| **R30** | **Un solo motore di database nei test.** Nessun test, a nessun livello, può usare SQLite, EF Core in-memory o qualunque provider diverso da SQL Server. I test che non necessitano di un database **non ne usano affatto**: costruiscono `IModel` con `UseSqlServer` senza mai aprire una connessione | test di convenzione (**bloccante**): nessun `PackageReference` a `Microsoft.EntityFrameworkCore.Sqlite`/`.InMemory` in alcun `.csproj` della soluzione, verificato leggendo `Directory.Packages.props` e i progetti. `BannedSymbols.txt` banna `UseInMemoryDatabase` e `UseSqlite` |
| **R31** | **Ogni verificatore vive al livello più economico che può diventare rosso.** Promuovere un verificatore a un livello più costoso del necessario si paga a ogni push, per sempre. Un verificatore che **può** essere L0 e viene scritto in L1 è un difetto, non una precauzione | review + la tabella di mappatura §*Mappatura R1-R25*, che è normativa: una regola spostata di livello richiede l'aggiornamento della tabella |
| **R32** | **Ogni entità del dominio è registrata in `FullSchemaDbContext`.** Il test dello schema completo copre solo ciò che il modello contiene: un'entità nuova non registrata lo lascerebbe verde su uno schema incompleto | test di convenzione L0 (**bloccante**): `DomainModelManifest.AllEntityTypes` vs `FullSchemaModel.GetEntityTypes()`; le entità mancanti sono nominate nel messaggio d'errore. **È R8 applicata al test 1785** |
| **R33** | **Ogni entità owned ha un builder registrato, e i test di popolamento usano il registro.** Nessuna lista di entità scritta a mano, da nessuna parte: né nei builder, né in R9, né negli helper | test di convenzione L0 (**bloccante**): entità owned da `IModel` vs `BuilderRegistry.Discover()`. Il messaggio d'errore dichiara la **conseguenza** ("il test R9 sarebbe verde su una cancellazione parziale"), non il sintomo |
| **R34** | **Il tempo è un input iniettato.** Nessun uso di `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow` nel codice di produzione: si usa `TimeProvider`. I test usano `FakeTimeProvider` | `BannedApiAnalyzers` + `TreatWarningsAsErrors` (**errore di compilazione**, non test). Senza questa regola i test di grazia a 30 giorni, di revoca (ADR-0004) e di `currentStale` a 90 giorni (`CONTEXT.md` §3.9) **non sono scrivibili** o sono flaky |
| **R35** | **L'antiforgery resta attivo nei test, e il token non compare mai nel corpo di un test.** L'estrazione vive dentro `RoamlyClient`. È **vietato** disattivare l'antiforgery in ambiente di test | review + test di convenzione: la configurazione di test non registra alcun filtro/stub che disabiliti l'antiforgery. Concilia `TESTING.md` §3.1 ("preferire l'estrazione") con "un test di ownership deve costare una riga": **l'unica conciliazione possibile** |
| **R36** | **Il tag dell'immagine SQL Server è pinnato in un punto unico e versionato.** Mai `latest`, mai duplicato tra fixture e workflow | test di convenzione: la costante esiste, non contiene `latest`, ed è l'unica occorrenza di `mcr.microsoft.com/mssql` nella soluzione |
| **R37** | **Nessun test viene reso verde con un retry.** Non esistono attributi di retry, né loop di ritentativo, né `Thread.Sleep` di attesa speculativa. Un fallimento non riproducibile si **diagnostica** | review + test di convenzione sull'assenza degli attributi di retry. **Motivo:** i retry non risolvono la flakiness, la rendono invisibile — e le tre sorgenti previste (deadlock, stato condiviso, tempo) sono tutte diagnosticabili |
| **R38** | **Ogni verificatore deve essere visto fallire almeno una volta.** Alla prima scrittura di un verificatore bloccante si introduce deliberatamente la violazione che deve intercettare, si osserva il rosso, si ripristina. Il fatto va annotato nel commit | disciplina + review. **Non automatizzabile, e dichiarato tale.** Un verificatore mai visto fallire è una speranza compilata: vale per l'analizzatore 1785 quanto per la matrice di contrasto di R14 |
| **R39** | **Le misure di performance non girano in CI.** I benchmark vivono in `Roamly.Benchmarks`, riusano la fixture del container **senza** Respawn e con RAM dedicata, e si eseguono **solo in locale** | configurazione di `ci.yml`: il progetto è escluso dal `dotnet test` della pipeline. **Motivo:** i runner GHA sono condivisi; un numero assoluto misurato lì è rumore presentato come dato |

---

## Mappatura R1-R25 → livello di test

È il reperto centrale di questo ADR, e la tabella è **normativa** (R31).

### I livelli

| Livello | Motore | Dove vive | Costo |
|---|---|---|---|
| **L0** — convenzione e unit | **nessun database**: `IModel` costruito offline + POCO puri | `Roamly.Model.Tests`, `Roamly.Domain.Tests` | **< 5 s** per l'intero livello |
| **L1** — integration DB | Testcontainers `MsSql` | `Roamly.IntegrationTests` | 60-100 s fissi + 1-3 min |
| **L2** — integration API | `WebApplicationFactory` **sopra lo stesso container** di L1 | `Roamly.IntegrationTests` | incluso in L1 |
| **L3** — frontend unit/convenzione | Vitest + lint + script Node | `frontend/roamly-web` | ~2 min |
| **L4** — E2E | Playwright + `@axe-core/playwright` | `frontend/roamly-web/e2e` | 4-6 min |

### R1-R7 — ownership (ADR-0003)

| Regola | Livello | Come | Costo |
|---|---|---|---|
| **R1** ownership universale | **L0** | itera `Model.GetEntityTypes()`: ogni entità implementa `IOwnedResource` o è in whitelist annotata. I complex type (`Money`, `Coordinates`, …) **non compaiono** in `GetEntityTypes()` per costruzione → nessuna whitelist a mano | ~5 ms |
| **R2** query filter `"OwnerScope"` | **L0** | ogni entità owned ha un filtro **nominato** `"OwnerScope"`; nessun'altra entità ne ha uno | ~5 ms |
| **R3** scrittura cross-owner | **L1** | ⚠️ **due test distinti.** (a) *interceptor*: `RunAsUser(A)`, si modifica una riga di B → `OwnershipViolationException`; (b) *assegnazione*: creando sotto A, `OwnerId` è A anche se il request model dichiarasse B. Richiede che `SaveChanges` arrivi fino in fondo | ~200 ms/test |
| **R4** coerenza gerarchica | **L0** (forma) **+ L1 una volta sola** | 🔴 **smentita 1.** L0: ogni FK figlia è composita, include `OwnerId`, e punta alla **PK `(OwnerId, Id)`** del padre (ADR-0008: nessuna chiave alternata da cercare). L1: un `INSERT` diretto con `OwnerId` divergente viene rifiutato — **una volta**, nel test B, non per slice | L0 ~5 ms · L1 ~200 ms **una tantum** |
| **R5** API bandite | **compilazione** | `BannedApiAnalyzers` + `TreatWarningsAsErrors`. **Non è un test.** Si aggiunge un L0 di secondo livello: `BannedSymbols.txt` contiene l'elenco esatto di `ARCHITECTURE.md` §4 — altrimenti il file si svuota senza che nessuno se ne accorga | 0 s + ~2 ms |
| **R6** identità sempre presente | **L0 + L2** | L0: `ICurrentUser` senza identità lancia. L2: endpoint owner-scoped senza cookie → `401`, mai `200` con lista vuota | ~1 ms + ~150 ms |
| **R7** indistinguibilità | **L2** | 🔴 **smentita 1: non è una regola di database.** `[Theory]` su `(metodo, rotta)` con `AsUserB` sull'`{id}` di A → `404` con `ProblemDetails` **byte-identico** a quello di un id inesistente | ~150 ms/riga |

**Helper di ownership da una riga**, con antiforgery attivo (R35):

```csharp
[Theory]
[OwnershipMatrix<CampersSlice>]   // deriva (metodo, rotta) dagli endpoint registrati
public Task Cross_owner_access_is_indistinguishable_from_absence(HttpMethod m, string route)
    => Ownership.AssertNotFoundForOther(m, route);
```

> ⚠️ **Sorgente di flakiness numero uno, oggi non tracciata da nessun documento.** ADR-0003 richiede la conferma email per il login, e `TESTING.md` §3.1 prevede un `IEmailSender` fake che espone **"l'ultimo token generato"**. Con un database condiviso (G1) quello è **stato globale mutabile**: due test che registrano un utente si rubano il token a vicenda. Il fake deve essere **per indirizzo** (dizionario), non "ultimo generato".

### R8-R10 — privacy (ADR-0004)

| Regola | Livello | Come | Costo |
|---|---|---|---|
| **R8** copertura universale | **L0 — la più importante** | insieme delle entità owned da `IModel` vs insieme coperto dai grafi di export e cancellazione, **entrambi derivati a runtime**. Whitelist annotata con `Reason` non vuota | ~10 ms |
| **R9** erase and sweep | **L1, non negoziabile** | popola un utente **via `BuilderRegistry`** (R33), esegue il job vero sotto `RunAsUser`, poi **itera su `IModel`** con `ctx.Set(clrType).CountAsync()`. Verifica **anche** che il `DELETE` finale di `AspNetUsers` riesca — è la parte che le `NO ACTION` possono far fallire | ~2-5 s (il test più lento della suite) |
| **R10** residuo non personale | **L0** | ogni entità non-owned è in whitelist; `ErasureReceipt` non ha alcuna FK verso `AspNetUsers`, verificabile su `IModel` | ~5 ms |
| §6b isolamento export | **L1** | l'`OwnerId` di B non compare **nei byte** dello ZIP di A | ~1 s |
| §6d idempotenza | **L1** | il job rieseguito non lancia e non altera la `ErasureReceipt` | ~2 s |
| §6e revoca | **L1 + `FakeTimeProvider`** | richiesta → +10 giorni → revoca → account pieno | ~500 ms |
| §6f accesso bloccato in grazia | **L2** | login durante la grazia → fallisce | ~200 ms |

### R11-R25 — design system (ADR-0007): **nessuna tocca il database**

| Regole | Livello | Come |
|---|---|---|
| **R11** correttivi anti-look | L3 | script `design:guard`: ≥4 valori `--radius-*` distinti, `components.json` senza preset default, nessun `--radius: 0.5rem` |
| **R12** nessun colore letterale | L3 | stylelint + ESLint su `src/**`, eccezione `styles/theme.css` |
| **R13** token a due livelli | L3 | lint: token letterale fuori da `theme.css` = errore |
| **R14** matrice 47 coppie × 2 temi | L3 | script che **parsa `theme.css`** (chiaro + `[data-theme="dark"]`) e ricalcola WCAG · **+ test di copertura**: ogni coppia usata nel codice esiste nel manifesto. ⚠️ **È la R8 del design system:** la seconda metà protegge il futuro ed è la più facile da dimenticare |
| **R15** `ui/` senza dominio | L3 | `dependency-cruiser` |
| **R16** niente `Card` generico | L3 | lint su naming di file ed export |
| **R17** tre segnali di distruttività | L3 | render di `DangerButton`/`DangerDialog` + lint sui token `danger-*` |
| **R18 / R22** budget font, Fraunces per eccezione | L3 | somma byte di `public/fonts/**/*.woff2`; lint CSS su `var(--font-display)`; conteggio occorrenze per pagina |
| **R19** nessun asset di terzi | L3 | scan di `index.html`, CSS e **bundle prodotto**. ⚠️ **deve girare sul bundle**: una dipendenza npm può iniettare un `@import` da CDN |
| **R20** reduced motion | **L4** | Playwright con `prefers-reduced-motion: reduce`; nessun elemento con `opacity < 1` computata |
| **R21** registro doppio i18n | L3 | nessun letterale di testo nel JSX; `features/privacy/**` importa solo `neutral.*` |
| **R23** date dal server | **L2 + L3** | L2: la risposta contiene `deletionScheduledForUtc`; L3: lint contro aritmetica su date in `features/privacy/**` |
| **R24** dati da seed distinguibili | L3 | test di render sulla lista |
| **R25** `Ribbon` senza lettura corrente | L3 | render con payload privo di `current`: asse temporale, nessuna cifra |
| §7c `axe` × 2 temi, §7e tastiera | **L4** | Playwright + `@axe-core/playwright` |

### R26-R29 — PK composita (ADR-0008)

Il verificatore di @oracle **vive in `Roamly.Model.Tests`** ed è L0, con una sola controparte L1 nel test B:

| Cosa | Livello | Come |
|---|---|---|
| PK di ogni entità owned = **esattamente 2 colonne**, nell'ordine `(OwnerId, Id)` | **L0** | `entityType.FindPrimaryKey()!.Properties` |
| **Nessuna chiave alternata**, su nessuna entità | **L0** | `entityType.GetKeys().Count() == 1` per ogni entità owned. ADR-0008 le ha eliminate tutte: il verificatore impedisce che rientrino per abitudine |
| Nessuna FK con `DeleteBehavior.SetNull` / `ClientSetNull` | **L0** | attraversa `GetForeignKeys()`. Alimenta anche l'analizzatore 1785 |
| `Id` è `ValueGeneratedNever()` (COMB generato client-side) | **L0** | `property.ValueGenerated == ValueGenerated.Never` |
| La PK è **CLUSTERED** e `CreatedAtUtc` **non** è nella clustering key | **L1**, nel test B | `sys.indexes` + `sys.index_columns`: l'indice `type_desc = 'CLUSTERED'` ha esattamente le due colonne della PK |
| Monotonicità del generatore COMB | **L0** (`Roamly.Domain.Tests`) | N guid generati in sequenza sono crescenti secondo l'ordinamento `uniqueidentifier` di SQL Server |

### Conclusione della mappatura

> **Dei 25 verificatori R1-R25, 17 non toccano alcun database, 5 richiedono SQL Server, 3 richiedono un browser.**
>
> Il budget di tempo della CI è dominato da **5 regole su 25**. È questo — non l'eleganza della piramide — l'argomento per separare il job veloce dal job di integrazione. E i verificatori che proteggono il *futuro* (R8, R14-copertura, R32, R33, l'analizzatore 1785) stanno **tutti** nel livello che costa meno di 5 secondi. È il livello da far crescere per primo e da tenere bloccante ovunque.

---

## Il doppio test 1785

Il test che giustifica l'urgenza della decisione **è due test**, perché uno dei due costa nulla e può proteggere ogni push.

### Test A — analizzatore 1785 model-driven (L0, nessun database, ~10 ms)

Calcola sul modello EF ciò che SQL Server calcolerebbe sulle constraint.

```csharp
[Fact]
public void No_table_pair_has_more_than_one_referential_action_path()
{
    IModel model = FullSchemaModel.Value;            // offline, UseSqlServer mai connesso

    // Un "percorso" esiste solo su CASCADE / SET NULL / SET DEFAULT (CONTEXT.md §5.1).
    static bool IsPath(IForeignKey fk) => fk.DeleteBehavior is
        DeleteBehavior.Cascade or DeleteBehavior.SetNull or DeleteBehavior.ClientSetNull;

    var edges = model.GetEntityTypes()
        .SelectMany(e => e.GetForeignKeys())
        .Where(IsPath)
        .Select(fk => (From: fk.PrincipalEntityType.GetTableName()!,
                       To:   fk.DeclaringEntityType.GetTableName()!,
                       Fk:   fk.GetConstraintName()))
        .ToList();

    var offenders = CascadeGraph.EnumerateAllPaths(edges)
        .GroupBy(p => (p.Source, p.Target))
        .Where(g => g.Count() > 1)
        .Select(g => $"{g.Key.Source} → {g.Key.Target}: {g.Count()} percorsi " +
                     $"[{string.Join(" | ", g.Select(p => string.Join("→", p.Constraints)))}]")
        .ToList();

    Assert.True(offenders.Count == 0,
        "Errore 1785 in arrivo. Percorsi multipli:\n" + string.Join("\n", offenders));
}
```

- **Cosa asserisce:** per ogni coppia ordinata di tabelle esiste **al massimo un** cammino composto solo da azioni referenziali. È la formalizzazione **eseguibile** di `CONTEXT.md` §5.2.
- **Dove gira:** **ogni push**, job veloce, bloccante. **Costo: ~10 ms.**
- **Valore che SQL Server non dà:** l'errore 1785 reale dice *quale FK ha rifiutato*, non *qual è l'altro percorso*. Questo test **nomina entrambi i cammini**. È la differenza tra mezz'ora e cinque minuti di diagnosi.
- ⚠️ **Limite dichiarato:** è un argomento analitico, solo eseguibile. Se la mia lettura della regola di SQL Server fosse sbagliata, il test sarebbe **verde per un motivo sbagliato**. È esattamente per questo che esiste il test B.

### Test B — creazione reale dello schema completo (L1, SQL Server vero, ~3 s)

```csharp
[Fact]
public async Task Full_schema_including_future_phases_is_creatable_on_sql_server()
{
    await using var db = await _sql.CreateEmptyDatabaseAsync();   // ~200-400 ms

    var options = new DbContextOptionsBuilder<FullSchemaDbContext>()
        .UseSqlServer(db.ConnectionString).Options;
    await using var ctx = new FullSchemaDbContext(options);

    var ex = await Record.ExceptionAsync(() => ctx.Database.EnsureCreatedAsync());

    if (ex is not null)
    {
        var sql = ex as SqlException ?? ex.InnerException as SqlException;
        Assert.Fail(sql?.Number == 1785
            ? $"ERRORE 1785 — percorsi di cascade multipli. {sql.Message}\n" +
              "Rileggere CONTEXT.md §5.1 e correggere la DeleteBehavior, NON aggirare con un trigger."
            : $"Lo schema completo non è creabile: {ex}");
    }

    await AssertEveryOwnedEntityHasNoActionFkToUsers(db);   // DATA.md §6
    await AssertEveryPkIsOwnerIdThenIdClustered(db);        // ADR-0008, R26-R29
    await AssertNoAlternateKeysExist(db);                   // ADR-0008: sono state eliminate tutte
    await AssertEveryChildFkIsComposite(db);                // R4, il "morso" una tantum
    await AssertMoneyColumnsAreDecimal19_4(db);             // CONTEXT.md §2.1
}
```

- **Perché non basta `GenerateCreateScript()`:** produce il DDL senza eseguirlo. **1785 non emerge dal DDL, emerge dall'esecuzione.** Deve essere `EnsureCreatedAsync`.
- **Perché `EnsureCreated` e non le migration:** il modello completo **non ha migration e non deve averne** (`CONTEXT.md` §2.6: niente tabelle vuote in produzione). Questo introduce una **divergenza dichiarata** — il test B verifica il *modello*, il test C verifica le *migration*. Sono cose diverse e servono entrambe.
- **Dove gira: ogni push, non nightly.** Una volta acceso il container per gli altri test L1, il costo marginale è di pochi secondi; relegarlo al nightly lo farebbe fallire **quando il contesto mentale della modifica è già svanito** — che è precisamente il fallimento contro cui la decisione è stata istituita.

### Test C — le migration Phase 1 si applicano davvero (L1, ~2 s)

```csharp
[Fact]
public async Task Phase1_migrations_apply_to_an_empty_database()
{
    await using var db = await _sql.CreateEmptyDatabaseAsync();
    await using var ctx = RoamlyDbContextFactory.For(db.ConnectionString);
    await ctx.Database.MigrateAsync();                       // non EnsureCreated
    Assert.Empty(await ctx.Database.GetPendingMigrationsAsync());
}
```

Chiude il requisito di `DATA.md` §5 (*"la prima migration va eseguita su un SQL Server vero prima di essere considerata valida"*) e impedisce la deriva modello ↔ migration.

### Il rapporto tra A e B

**A e B non devono dipendere l'uno dall'altro in CI** (niente `needs:`): devono poter fallire **entrambi**, perché la loro **discordanza è informazione**. Se B fallisce con 1785 mentre A è verde, l'analizzatore è sbagliato e va corretto **prima** di proseguire. Questo è il momento in cui la decisione #6 produce il suo valore.

### Il prerequisito: i POCO strutturali (smentita 2)

`FullSchemaDbContext` richiede le classi C# di `Trip`, `TripStop`, `Checklist`, `ChecklistItem`, `JournalEntry`, `Expense`, `SavedPlace`, `Document`.

| Modo | Descrizione | Giudizio |
|---|---|---|
| **A** ⭐ **scelto** | POCO **strutturali** in `Roamly.Domain` (solo `Id`, `OwnerId`, `CreatedAtUtc`, FK composite, navigazioni) + `IEntityTypeConfiguration` completa, registrati **solo** in `FullSchemaDbContext` | ✅ Il codice che arriverà in Phase 2 è **lo stesso** che il test ha già validato. Nessuna divergenza possibile. ~150 righe, ~2 h |
| **B** | Entità "ombra" dichiarate solo nel progetto di test | ❌ Dimostrerebbe la topologia di un modello **che nessuno implementa**. Quando Phase 2 divergesse dalla copia, il test resterebbe **verde** e la garanzia evaporerebbe: è il fallimento di R8 riprodotto altrove |
| **C** | Rinviare il test finché le entità non esistono | ❌ Annulla la decisione: i casi 1785 riguardano **proprio** quelle entità |

I POCO strutturali adottano la PK composita di **ADR-0008**: `(OwnerId, Id)` CLUSTERED, `Guid` COMB client-side, `ValueGeneratedNever()`, **nessuna chiave alternata**. Le FK figlie puntano direttamente alla PK del padre — che è il motivo per cui le alternate key sono sparite e per cui il test B non le cerca più.

Il modo A si degrada da solo senza **R32** (meta-verificatore di completezza): fra sei mesi qualcuno aggiunge un'entità, non la registra in `FullSchemaDbContext`, e il test 1785 resta verde su uno schema incompleto.

---

## Isolamento e parallelismo

### Perché non la transazione con rollback — ❌ respinta

Aprire una transazione e fare rollback è veloce (~5 ms) e isola perfettamente. **Non funziona qui, per due ragioni indipendenti:**

1. **I test L2 passano da HTTP.** `WebApplicationFactory` + `HttpClient` fa sì che l'handler apra il **proprio** `DbContext`, nel **proprio** scope DI, su una **propria** connessione. La transazione del test non è la sua. Forzarla richiederebbe un `TransactionScope` ambientale — che con più connessioni **promuove a transazione distribuita** — oppure iniettare la stessa connessione ovunque, cioè **alterare il comportamento di produzione dentro il test**.
2. **`AccountErasureJob` (R9) gestisce le proprie transazioni.** Un rollback esterno o le annida o le conflitta: si testerebbe una versione del job diversa da quella che gira.

### Respawn — ✅ adottato come meccanismo di reset

```csharp
_respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
{
    DbAdapter        = DbAdapter.SqlServer,
    SchemasToInclude = ["dbo"],
    TablesToIgnore   = [new Table("__EFMigrationsHistory")],
});
```

Tre avvertenze specifiche di questo schema:

- **`ErasureReceipt` non va ignorata.** Non ha FK ed è non-owned: se sopravvivesse tra i test, il test di idempotenza diventerebbe dipendente dall'ordine di esecuzione.
- **Le FK `OwnerId → AspNetUsers` sono `NO ACTION`**: Respawn deve svuotare le tabelle owned *prima* di `AspNetUsers`, e lo fa derivando l'ordine dalle FK. È **la stessa logica** che `AccountErasureJob` implementa. Quindi **un fallimento di Respawn è un segnale**, non un fastidio: si indaga, non si aggira con `TablesToIgnore`.
- **Respawn non resetta le `IDENTITY`.** ✅ Irrilevante: ADR-0008 ha chiuso la PK su `Guid` COMB generato **client-side**, quindi nessun test può dipendere da un contatore. Questo chiude anche il trigger che l'analisi aveva lasciato aperto in attesa di @oracle.

### Granularità: si parte da G1

| Gradino | Forma | Parallelismo | Costo | Quando |
|---|---|---|---|---|
| **G1** ⭐ | **Un solo database**, tutti i test DB in una `[Collection("Database")]`, Respawn tra i test | nessuno tra i test DB (L0/L3 restano paralleli in altri assembly) | 50-200 ms/test | **dal giorno 1**, fino a ~150 test o ~3 min |
| **G2** | **Un database per collection** (≈ una per slice), Respawn all'interno | pari al numero di collection, `maxParallelThreads: 4` | +300-800 ms per collection | quando G1 supera i 3 min (**T1**) |
| **G3** | **Template + `BACKUP`/`RESTORE`** (o `DBCC CLONEDATABASE`) | massimo | alta | probabilmente **mai** |

> Non si sceglie oggi tra G1 e G2: si sceglie **una forma che permette di passare dall'uno all'altro in un'ora**. Le `[Collection]` esistono già dal giorno 1; cambia soltanto *dove punta la connection string della lease*. Costo di inversione: ~15 righe di `DatabaseLease`.

### Parallelismo xUnit, per assembly

| Assembly | `parallelizeTestCollections` | `maxParallelThreads` | Motivo |
|---|---|---|---|
| `Roamly.Domain.Tests` | `true` | default | nessuno stato condiviso |
| `Roamly.Model.Tests` | `true` | default | `IModel` è immutabile, condiviso via `IAssemblyFixture` |
| `Roamly.IntegrationTests` | **`false`** in G1 / `true` in G2 | **`4`** | un SQL Server con 2 GB va in contesa e in deadlock sotto scritture parallele sulle stesse tabelle |

### Le tre sorgenti di flakiness previste

1. **Deadlock SQL Server** su scritture parallele che toccano `AspNetUsers` e tabelle owned in ordine inverso → prevenuto da G1, o da DB distinti in G2. **Non si risolve con i retry** (R37): i retry lo nascondono.
2. **Ryuk e container orfani.** Con più assembly che avviano ciascuno un container si moltiplicano i SQL Server da 2 GB. **Un solo assembly possiede il container** (`Roamly.IntegrationTests`), via `IAssemblyFixture<SqlServerFixture>` — uno dei motivi per cui è stato scelto xUnit v3.
3. **Data, ora e fuso.** `CreatedAtUtc`, i 30 giorni di grazia e `currentStale` a 90 giorni rendono il tempo un input di dominio → **R34**, `TimeProvider` dal primo commit.

---

## Struttura dei quattro progetti

`ARCHITECTURE.md` §3 ne prevede tre. Ne servono **quattro**, perché L0 ha una caratteristica che gli altri non hanno: **deve poter girare senza Docker, in meno di 5 secondi, anche in un pre-commit hook.**

```text
tests/
├── Roamly.Domain.Tests/            # unit puri, nessuna dipendenza EF
│   ├── Money/ · Geo/ · Trips/
│   └── Identifiers/CombGuidTests.cs        # monotonicità del generatore (ADR-0008)
│
├── Roamly.Model.Tests/             # L0 — convenzioni su IModel. NESSUN database, nessun Docker.
│   ├── ModelFixture.cs                     # IAssemblyFixture: IModel una volta, offline
│   ├── Ownership/                          # R1, R2, R4-forma, R5-manifesto
│   ├── Privacy/                            # R8, R10
│   ├── Keys/                               # R26-R29 — il verificatore di @oracle
│   ├── Schema/
│   │   ├── CascadePathAnalyzerTests.cs     # test A — l'analizzatore 1785
│   │   └── FullSchemaCompletenessTests.cs  # R32
│   └── TestData/BuilderRegistryCoverageTests.cs   # R33
│
├── Roamly.IntegrationTests/        # L1 + L2 — l'UNICO progetto che possiede il container
│   ├── Infrastructure/
│   │   ├── SqlServerFixture.cs             # IAssemblyFixture: Testcontainers, tag pinnato (R36)
│   │   ├── DatabaseLease.cs                # CREATE DATABASE / Respawn / DROP — sblocco G1→G2
│   │   ├── RoamlyApiFactory.cs             # WebApplicationFactory + CookieContainer
│   │   ├── RoamlyClient.cs                 # login vero + antiforgery nascosto (R35)
│   │   └── DatabaseCollection.cs
│   ├── Schema/                             # test B, test C, topologia FK
│   ├── Privacy/                            # R9, isolamento export, idempotenza, revoca
│   ├── Features/                           # ownership per slice, la [Theory] da una riga
│   └── TestData/                           # IEntityBuilder, BuilderRegistry, builder concreti
│
└── Roamly.ArchitectureTests/       # Fase 1+, NetArchTest sulle slice verticali
```

Frontend, in `frontend/roamly-web/`: `src/**/__tests__/` (Vitest, L3) · `scripts/design-guard/` (R11-R14, R18, R19, R22) · `e2e/` (Playwright, L4, matrice sui due temi).

Fuori da `tests/`: **`benchmarks/Roamly.Benchmarks/`**, che riusa `SqlServerFixture` parametrizzata e **non entra in `ci.yml`** (R39).

### Convenzioni di naming

- **File**: `{Soggetto}Tests.cs`; per le regole invarianti si antepone il codice — `R9_EraseAndSweepTests.cs`. Il codice nel nome è deliberato: quando ADR-0004 verrà riletto fra un anno, il percorso dal documento al test deve essere una **ricerca testuale**.
- **Metodi**: frase in inglese, `snake_case`: `Cross_owner_write_is_rejected_by_the_interceptor`. Leggibile nel report di CI senza aprire il codice.
- `// Arrange` / `// Act` / `// Assert` espliciti solo oltre le ~10 righe.
- Niente `Test1`, niente `Should` nel nome: il nome dice il comportamento atteso, non la sintassi dell'assert.

---

## Test data builder model-driven

È lo stesso problema di R8, e si risolve allo stesso modo: **derivandolo dal modello, non dalla memoria.**

```csharp
public interface IEntityBuilder
{
    Type EntityType { get; }
    object Build(BuilderContext ctx);   // istanza valida e minimale, agganciata al padre
}
```

`BuilderRegistry` scopre le implementazioni per riflessione e le ordina **topologicamente sul grafo delle FK derivato da `IModel`** — lo stesso ordinamento che `AccountErasureJob` percorre al contrario. **Nessuna lista scritta a mano da nessuna parte.** Con la PK COMB client-side di ADR-0008 il builder conosce l'`Id` **prima** dell'`INSERT`: aggancia i figli senza round-trip e senza `SaveChanges` intermedi.

Il verificatore di completezza (R33) è L0, ~10 ms, bloccante, e il suo messaggio d'errore dichiara **la conseguenza**:

> *"Entità owned senza builder: `JournalEntry`. Il test R9 'erase and sweep' NON la coprirebbe: sarebbe verde su una cancellazione parziale."*

È la differenza tra un test che insegna e un test che si aggira. La catena resta chiusa: **modello → registro → popolamento → verifica sul modello.**

> ⚠️ **Debolezza dichiarata.** Il verificatore garantisce che il builder **esista**, non che sia **significativo**: un builder che popola i soli campi obbligatori supera il test senza esercitare gli opzionali. Accettato — la completezza semantica non è automatizzabile a costo ragionevole. Mitigazione per convenzione, verificabile solo in review: *"il builder popola tutti i campi non-nullable e almeno un campo nullable per complex type"*.

---

## La pipeline

### `ci.yml` — quattro job paralleli

| Job | Trigger | Contenuto | Tempo atteso |
|---|---|---|---|
| **`backend-fast`** | **ogni push + PR** | restore (cache NuGet) → build con `TreatWarningsAsErrors` + `BannedApiAnalyzers` (R5, R34) → `Domain.Tests` + `Model.Tests` (R1, R2, R4-forma, R8, R10, R26-R29, **analizzatore 1785**, R32, R33) | **90-120 s** |
| **`backend-integration`** | **ogni push + PR** | Testcontainers → **test B** → **test C** → R3, R7, R9, privacy, ownership per slice | **3-4 min** (60-100 s di container + 2-3 min di test) |
| **`frontend`** | **ogni push + PR** | `npm ci` → ESLint + stylelint → `design:guard` → `contrast:check` (47 coppie × 2 temi) → Vitest → `vite build` → `fonts:budget` + `no-external-assets` **sul bundle** | **2-3 min** |
| **`e2e`** | **solo PR** + nightly | Playwright: `axe` su entrambi i temi, reduced-motion (R20), cancellazione account da tastiera | **4-6 min** |

**Tempo di parete:** push ≈ **3-4 min** (domina il job di integrazione) · PR ≈ **5-7 min**. Entrambi sotto la soglia del "si smette di guardare".

`backend-fast` **non** è un gate di `backend-integration` (niente `needs:`): devono poter fallire entrambi.

### `nightly.yml`

| Cosa | Perché |
|---|---|
| Suite completa incluso E2E su entrambi i temi | copertura piena senza pesare sul loop di sviluppo |
| Test B eseguito **anche** contro il tag SQL Server **successivo** a quello pinnato | rilevamento di drift: si scopre che un CU cambia comportamento **prima** di doverci aggiornare |
| `dotnet ef migrations bundle` generato ed eseguito su DB vuoto | `DEVOPS.md` §2.1: il bundle è lo strumento di deploy, va verificato che si generi |
| `dotnet list package --vulnerable --include-transitive` | sicurezza a costo zero |

### Pre-commit locale (facoltativo, raccomandato)

Solo **L0 + lint**: < 10 s, **nessun Docker**. Un pre-commit che avvia un container viene disinstallato entro una settimana.

### Accorgimenti di velocità

1. Cache NuGet e npm: **-30-60 s** per run.
2. **Non** cachare l'immagine Docker con `actions/cache` finché non lo si è misurato (F-13): ripristinare ~700 MB dalla cache GHA può costare quanto il pull da MCR.
3. `services:` in GHA al posto di Testcontainers resta disponibile come ottimizzazione **futura e locale al workflow**, se T2 scatta.

---

## La sequenza operativa del primo giorno (~7,5 h)

Il repository contiene oggi solo `docs/`, `AGENTS.md`, `README.md`.

> **Nota di sequenza.** ADR-0003 indica `Identity` come primo modulo. Non c'è conflitto: i blocchi 1-4 **non richiedono** endpoint di Identity, solo `IdentityUser<Guid>` dal pacchetto EF per chiudere la topologia delle FK `OwnerId`. `Identity` come *modulo applicativo* resta il primo **dopo** questa infrastruttura.

### Blocco 1 — scheletro (~60 min)

```powershell
git init
dotnet new sln -n Roamly
dotnet new classlib -o src/Roamly.Domain
dotnet new classlib -o src/Roamly.Infrastructure
dotnet new web      -o src/Roamly.Api
dotnet new xunit3   -o tests/Roamly.Domain.Tests
dotnet new xunit3   -o tests/Roamly.Model.Tests
dotnet new xunit3   -o tests/Roamly.IntegrationTests
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj)
```

`global.json` (pin dell'SDK), `Directory.Packages.props` (central package management), `Directory.Build.props` (`Nullable`, `TreatWarningsAsErrors`, `LangVersion latest`), `BannedSymbols.txt` con `DateTime.UtcNow` (R34), `UseInMemoryDatabase`, `UseSqlite` (R30).
✅ **Checkpoint 1:** `dotnet build` verde.

### Blocco 2 — il modello, senza database (~150 min)

1. `IOwnedResource` (`Id`, `OwnerId`, `CreatedAtUtc`, `UpdatedAtUtc`) + helper **COMB** di ADR-0008.
2. Complex type `Money`, `Coordinates`, `Dimensions`, `Weights`, `Capacities`, con gli invarianti di `CONTEXT.md` §2.1.
3. POCO **Phase 1**: `Camper`, `Equipment`, `MaintenanceItem`, `MaintenanceLog`, `OdometerReading`, `ErasureReceipt`.
4. POCO **strutturali Phase 2/3/4**: `Trip`, `TripStop`, `Checklist`, `ChecklistItem`, `JournalEntry`, `Expense`, `SavedPlace`, `Document`.
5. `IEntityTypeConfiguration` per **tutte**: PK `(OwnerId, Id)` CLUSTERED + `ValueGeneratedNever()`, **nessuna `HasAlternateKey`**, FK composite verso la PK del padre, `NoAction` su ogni `OwnerId`, `Cascade` sulle 9 gerarchiche.
6. `RoamlyDbContext` (solo Phase 1) e `FullSchemaDbContext` (tutte), che **condividono le stesse configuration**.
7. `DomainModelManifest.AllEntityTypes` — alimenta R32.
✅ **Checkpoint 2:** build verde, modello costruibile offline.

### Blocco 3 — il primo test che conta, senza Docker (~60 min)

8. `ModelFixture` (`IAssemblyFixture`), `IModel` con `UseSqlServer` mai connesso.
9. **`CascadePathAnalyzerTests`** — test A.
10. **Rosso deliberato (R38):** portare temporaneamente `JournalEntry.TripStopId` a `Cascade` e verificare che il test **fallisca nominando `Trip → JournalEntry`**. Ripristinare.
    > **Il passo più importante della giornata.** Un verificatore mai visto fallire non è un verificatore.
11. `FullSchemaCompletenessTests` (R32) + verificatore chiavi R26-R29 + `BuilderRegistryCoverageTests` (R33).
✅ **Checkpoint 3:** `dotnet test tests/Roamly.Model.Tests` verde in **< 5 s**. La topologia di `CONTEXT.md` §5.2 non è più solo analitica.

### Blocco 4 — SQL Server vero (~90 min)

12. `Testcontainers.MsSql` + `Respawn`; `SqlServerFixture` con tag **pinnato** (R36) e risorse parametrizzabili (R39); `DatabaseLease`.
13. **`FullSchemaCreationTests`** — test B con le cinque asserzioni strutturali.
14. **Rieseguire il rosso del passo 10 su SQL Server vero** e verificare che l'eccezione sia davvero `SqlException.Number == 1785`.
    > Se non lo è, **l'analizzatore del passo 9 è sbagliato** e va corretto prima di proseguire. È il momento in cui questa decisione produce il suo valore.
15. **Misurare e annotare** i tempi reali di F-12 in `TESTING.md` §10.
✅ **Checkpoint 4:** test verde. `CONTEXT.md` §3.11 può essere chiusa — **dopo l'esecuzione reale, non dopo l'approvazione di questo ADR**.

### Blocco 5 — migration e CI (~90 min)

16. `dotnet ef migrations add InitialPhase1` su `RoamlyDbContext` (solo Phase 1).
17. `Phase1MigrationTests` — test C.
18. `ci.yml` con `backend-fast` e `backend-integration`. Gli altri due job arrivano col frontend.
✅ **Checkpoint 5:** push su branch, CI verde, tempo di parete misurato e annotato.

| Blocco | Tempo | Risultato |
|---|---|---|
| 1 | ~1 h | build verde |
| 2 | ~2,5 h | modello completo compilabile |
| 3 | ~1 h | **1785 verificato analiticamente, in modo eseguibile** |
| 4 | ~1,5 h | **1785 verificato su SQL Server reale** |
| 5 | ~1,5 h | migration + CI |
| | **~7,5 h** | una giornata piena · **un giorno e mezzo realistico** |

> **Fermarsi qui. Nessuna slice di dominio prima del Checkpoint 3.** Se il Blocco 3 o 4 rivelasse un 1785 non previsto, l'ora di correzione va spesa **prima** che esista codice applicativo appoggiato alla topologia sbagliata. È l'intera ragione per cui la #6 è stata promossa a prerequisito.

---

## Costo di inversione

| Scelta | Costo | Motivo |
|---|---|---|
| **Testcontainers → `services:` / container condiviso** | 🟢 **BASSO** — ~20 righe | la connection string arriva dalla fixture: cambia la *fonte*, non i test. **Vincolo di progettazione da rispettare dal giorno 1: nessun test conosce il container** |
| **G1 → G2 (un DB per collection)** | 🟢 **BASSO** — ~15 righe di `DatabaseLease` | le `[Collection]` esistono già; cambia dove punta la lease. Pre-progettato apposta |
| **G2 → G3 (template/restore)** | 🟠 **MEDIO** | `BACKUP`/`RESTORE` e percorsi dentro il container. Una giornata |
| **Tag immagine 2022 → 2025** | 🟢 **BASSO** | una costante. Il nightly rende il cambio **già misurato** quando servirà |
| **xUnit v3 → v2** | 🟠 **MEDIO** | le `IAssemblyFixture` non esistono in v2: servirebbe una collection che abbraccia tutto, con perdita di parallelismo. Mezza-una giornata |
| **Aggiungere SQLite come secondo provider** | 🔴 **ALTO e crescente** | non è un'inversione, è un **raddoppio permanente**: due dialetti, due percorsi di creazione schema, due comportamenti su `decimal`. Si paga a ogni entità nuova. **È la scelta da non fare** |
| **POCO in `Roamly.Domain` → entità ombra nei test** | 🔴 **ALTO, e silenzioso** | l'inversione non rompe nulla: rompe la **garanzia**. Il test resta verde mentre smette di verificare la realtà. Il peggior tipo di costo di inversione |
| **Rinunciare al test B (solo analizzatore L0)** | 🔴 **ALTO** | si torna a oggi: convinzione argomentata, non fatto. Costa poco in codice e molto in garanzia |
| **Aggiungere `TimeProvider` dopo** | 🔴 **ALTO** | tocca **ogni** handler che scrive `CreatedAtUtc` o `UpdatedAtUtc`. È additivo solo il primo giorno |
| **Aggiungere i benchmark alla fixture dopo** | 🟠 **MEDIO** (🟢 se previsto ora) | la parametrizzazione costa ~10 righe oggi e una riscrittura della fixture dopo |
| **Passare a hardware ARM64** | 🔴 **ALTO, e non mitigabile da noi** | nessuna immagine ufficiale ARM64, Azure SQL Edge ritirata (smentita 3). **È un trigger di revisione, non un'inversione** |

---

## Tradeoff negativi accettati

1. **Ogni push paga 60-100 s di container.** Su un progetto personale con molti push piccoli è un costo reale e percepibile. Accettato perché l'alternativa — relegare l'integrazione al nightly — sposta la scoperta dei guasti **a dopo il cambio di contesto**.
2. **Serve Docker sulla macchina di sviluppo**, ~2 GB di RAM occupati durante i test. Su un portatile modesto in multitasking si sente.
3. **G1 rende i test DB seriali.** Se la suite crescesse oltre le previsioni si supererebbero i 3 minuti prima di accorgersene. Mitigazione: annotare il tempo del job a ogni revisione di `TESTING.md`; **T1**.
4. **I POCO Phase 2/3/4 esistono in `Roamly.Domain` senza tabella, per mesi.** È codice non usato in produzione — esattamente ciò che `CONTEXT.md` §2.6 voleva evitare per le *tabelle*. Accettato: codice morto in un assembly costa zero a runtime, una tabella vuota in produzione è debito operativo. **Ma va detto, non scoperto.**
5. **L'analizzatore 1785 è codice nostro che può sbagliare.** È un verificatore che deve essere verificato. Mitigazione: passo 14 del primo giorno, R38, e l'assenza di `needs:` tra i due job.
6. **Il verificatore dei builder garantisce l'esistenza, non la significatività.**
7. **Nessun test di concorrenza reale.** `rowversion` ed ETag saranno testati in sequenza, mai sotto contesa. **Un bug di race condition non verrà intercettato da questa suite.** Dichiarato, non mitigato: il costo di un test di concorrenza affidabile è sproporzionato a questa fase.
8. **Nessun test su backup e finestra di ripristino** (vincolo F5 di ADR-0004). Resta, come la retention del sink, un requisito non verificabile da test.
9. **`EnsureCreated` (test B) e le migration (test C) possono divergere.** Il modello completo non ha migration per scelta. Quando Phase 2 arriverà, la sua migration andrà confrontata **a mano** con ciò che il test B creava. Mitigazione parziale: il test C verifica che non ci siano pending migration per Phase 1.
10. **Gli E2E girano solo su PR e nightly.** Un push diretto su un branch personale può rompere l'accessibilità senza accorgersene. Accettato per il tempo di parete.
11. **Questa strategia dipende dall'hardware.** Su ARM64 non esiste percorso pulito (smentita 3). È l'unico tradeoff che **non possiamo mitigare**: dipende da Microsoft.
12. **Docker Desktop ha licenza commerciale sopra certe soglie aziendali.** Irrilevante per un progetto personale. Alternative gratuite supportate da Testcontainers: Podman, Rancher Desktop.

---

## Trigger di revisione pre-approvati

Al verificarsi di **uno solo**, la parte corrispondente va rivista prima di procedere.

| # | Trigger | Azione |
|---|---|---|
| **T1** | `backend-integration` supera i **3 minuti** | salire da G1 a G2. ~15 righe |
| **T2** | Il tempo di parete su push supera i **5 minuti** | rivalutare `services:` al posto di Testcontainers in CI e spostare parte dei test su PR-only |
| **T3** | Più di **1 test su 100** fallisce in modo non riproducibile in un mese | fermarsi e diagnosticare. **Non aggiungere retry** (R37). Le tre sorgenti previste sono note |
| **T4** | Si passa a hardware **ARM64** | 🔴 **decisione riaperta per intero.** Oggi non esiste un percorso pulito. Opzioni: SQL Server remoto condiviso, test DB solo in CI, o rivalutare il database |
| **T5** | La **#9** sceglie **Azure SQL Database** | il container resta fedele al ~95%, ma Azure SQL diverge (niente `USE`, limiti su `CREATE DATABASE`, feature T-SQL). Valutare l'emulatore o un DB Azure dedicato al nightly |
| **T6** | Microsoft cambia licenza o disponibilità dell'immagine SQL Server | riaprire §*Opzioni considerate* |
| **T7** | I test di integrazione superano i **500** | G3 (template/restore) diventa giustificabile |
| **T8** | Entra in scope la **#10 (PWA/offline)** | serve un livello di test aggiuntivo (storage locale, purge al logout) oggi non previsto |
| **T9** | Il test B rivela un **1785 non previsto** da `CONTEXT.md` §5.2 | non è un trigger sulla strategia: **è il successo della strategia.** Ma va riaperto `CONTEXT.md` §5, mai aggirato con un trigger |
| **T10** | Un ADR futuro introduce **condivisione tra utenti** | il grafo di cancellazione smette di essere un albero: R9, i builder e l'analizzatore 1785 vanno tutti rivisti insieme |

---

## Vincoli derivati per altri agenti

| Destinatario | Vincolo |
|---|---|
| **@oracle** (ADR-0008) | il verificatore R26-R29 **vive in `Roamly.Model.Tests`** ed è L0, tranne la verifica CLUSTERED che è L1 nel test B. I **benchmark** riusano `SqlServerFixture` parametrizzata (4 GB, nessun Respawn, dataset 10⁵-10⁶ righe) dal progetto `Roamly.Benchmarks`, **solo in locale** (R39) |
| **@solomon** | ogni handler riceve `TimeProvider` iniettato (R34). Nessun `DateTime.UtcNow`: è un errore di compilazione |
| **@hermes** | R7 è un **contratto di API**, non di database: il `ProblemDetails` del `404` per risorsa altrui deve essere **byte-identico** a quello dell'id inesistente. È testabile solo se l'error model è centralizzato |
| **@vulcan** (#9) | `ci.yml` passa da 3 a **4 job** con i tempi dichiarati; serve `nightly.yml`; l'environment "test" è **SQL Server effimero via Testcontainers con tag pinnato**, non un ambiente persistente |
| **@sentinel** | i verificatori di R1/R2/R8/R10 vivono in `Roamly.Model.Tests` e sono **L0**, non integration: `SECURITY.md` §2.2 va allineato sui nomi |
| **@pixel** (ADR-0007) | i tre verificatori girano nel job `frontend`; `axe` è **PR + nightly**, non ogni push. La **seconda metà di R14** (copertura del manifesto) è la parte che protegge il futuro |

---

## Conseguenze su altri documenti — checklist di propagazione

> ⚠️ Con questo ADR è stato aggiornato **solo** `architecture/TESTING.md`. Tutto il resto è da propagare.

| File | Sezione | Cosa cambiare |
|---|---|---|
| **`architecture/CONTEXT.md`** | **§3.11** | da ⚠️ aperta a ✅ chiusa **solo dopo il Checkpoint 4**, cioè dopo l'esecuzione reale — non dopo l'approvazione di questo ADR |
| | **§2.6** | aggiungere il prerequisito della **smentita 2**: i POCO strutturali Phase 2/3/4 devono esistere in `Roamly.Domain` perché il test dello schema completo sia scrivibile. "Tabella non creata" ≠ "codice non scritto" |
| | **§5.2** | "verificata analiticamente **ed eseguibilmente**", con riferimento all'analizzatore L0; la riserva finale si scioglie al Checkpoint 4 |
| | **§5.3** | rimuovere i riferimenti alle chiavi alternate `UNIQUE (Id, OwnerId)`: ADR-0008 le ha eliminate, e i verificatori ora ne asseriscono **l'assenza** |
| | **§3.8** | chiusa da ADR-0008; annotare che i benchmark usano la fixture di questo ADR, solo in locale |
| **`architecture/DATA.md`** | **§2.2** | linkare ADR-0009 e il nome esatto del test: `FullSchemaCreationTests` |
| | **§5** | il **bundle di migration** è generato ed eseguito nel **nightly** |
| | **§6** | l'ordine topologico di cancellazione è verificato da un test L0 **ed è lo stesso** che Respawn deriva: una divergenza tra i due è un **segnale**, non un fastidio |
| **`architecture/DEVOPS.md`** | **§1** | sostituire i tre job con i **quattro** dichiarati qui, con i tempi attesi; aggiungere **`nightly.yml`** |
| | **§2.4** | cache NuGet/npm sì; cache dell'immagine Docker **da misurare prima di adottare** (F-13) |
| | **§3** | aggiungere la riga "test" alla tabella degli environment: SQL Server **effimero** via Testcontainers, tag pinnato (R36). Promuovere la nota ARM64 a **vincolo hardware** (smentita 3) |
| **`architecture/ARCHITECTURE.md`** | **§3** | albero `tests/`: da **3 a 4** progetti, più `benchmarks/Roamly.Benchmarks` fuori da `tests/` |
| | **§4** | `BannedSymbols.txt` acquisisce `DateTime.UtcNow`/`Now`/`DateTimeOffset.UtcNow` (R34), `UseInMemoryDatabase`, `UseSqlite` (R30) |
| **`architecture/SECURITY.md`** | **§2.2** | allineare i nomi: i verificatori di R1/R2/R8/R10 vivono in `Roamly.Model.Tests` e sono **L0 senza database** |
| | **§2.2** | correggere: **R7 non richiede un database**, è un contratto HTTP (smentita 1) |
| **`OPEN-DECISIONS.md`** | tabella "Prima della Phase 1" | **#6 → ✅ chiusa 2026-09-25**, con riga di esito e link a ADR-0009 |
| | vincoli derivati da #2 / #4 / #7 | aggiornare le righe "**#6** DB di test": il vincolo è soddisfatto. **Correggere** la riga derivata dalla #2: R7 non richiede un database e R4 è in gran parte L0 |
| | 🆕 gap minori | aggiungere "**`TimeProvider` e controllo del tempo nei test**" (chiuso da R34) e "**Entità Phase 2+ come POCO strutturali**" (chiuso dal modo A) |
| | Rischi | aggiungere "**Test di integrazione flaky**", mitigazione = G1 + le tre sorgenti note + R37 + T3 |
| | 🆕 vincolo su #9 | `ci.yml` a quattro job + `nightly.yml`; environment "test" effimero |
| **`adr/0007-design-system.md`** | verificatori | nessuna modifica di sostanza (un ADR non si modifica): annotare **altrove** che i tre verificatori girano nel job `frontend` e che `axe` è PR+nightly |
| **`AGENTS.md`** | — | aggiungere i comandi di esecuzione dei test: `dotnet test tests/Roamly.Model.Tests` (< 5 s, no Docker) vs suite completa (richiede Docker) |
| **`product/ROADMAP.md`** | Phase 1 | inserire **~1,5 giorni di infrastruttura di test prima della prima slice di dominio**, con i cinque checkpoint |

---

## Riferimenti

Documenti impattati: `architecture/TESTING.md` (riscritto con questo ADR), `architecture/CONTEXT.md` (§2.6, §3.8, §3.11, §5.2, §5.3), `architecture/DATA.md` (§2.2, §5, §6), `architecture/DEVOPS.md` (§1, §2.4, §3), `architecture/ARCHITECTURE.md` (§3, §4), `architecture/SECURITY.md` (§2.2), `OPEN-DECISIONS.md`, `AGENTS.md`, `product/ROADMAP.md`.

ADR correlati: [`0001`](0001-use-sql-server.md) · [`0003`](0003-auth-and-ownership.md) · [`0004`](0004-privacy-and-erasure.md) · [`0007`](0007-design-system.md) · [`0008`](0008-primary-key-strategy.md).

Analisi completa che ha prodotto questa decisione (opzioni scartate in dettaglio, costi per singolo test, formato Decision Required con le 11 domande): sessione @argus del 2026-09-25.
