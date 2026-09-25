# Roamly — Strategia di test

Stato: **attivo — decisione #6 chiusa** · Ultimo aggiornamento: 2026-09-25
Owner: **@argus** · Decisione di riferimento: [`ADR-0009`](../adr/0009-test-strategy.md)
Origine: `docs/archive/Roamly_Planning_2026-09-24.md` §19 — esteso secondo review I8, riscritto con ADR-0009

> Questo documento è **operativo**: dice come si scrivono ed eseguono i test.
> Le motivazioni, le opzioni scartate, i tradeoff e i trigger stanno in [`ADR-0009`](../adr/0009-test-strategy.md) e non vengono duplicati qui.

---

## 1. I cinque livelli

Il criterio che governa tutta la suite (**R31**): **ogni verificatore vive al livello più economico che può davvero diventare rosso.** Un verificatore promosso a un livello più costoso del necessario si paga a ogni push, per sempre.

| Livello | Cosa verifica | Motore | Progetto | Costo |
|---|---|---|---|---|
| **L0** | convenzioni sul modello EF, unit puri di dominio | **nessun database**: `IModel` costruito offline + POCO puri | `Roamly.Model.Tests`, `Roamly.Domain.Tests` | **< 5 s** in totale |
| **L1** | schema reale, FK che mordono, erasure, migration | **Testcontainers `MsSql`** | `Roamly.IntegrationTests` | 60-100 s fissi + 1-3 min |
| **L2** | contratti HTTP, cookie, antiforgery, ownership per slice | `WebApplicationFactory` **sopra lo stesso container** di L1 | `Roamly.IntegrationTests` | incluso in L1 |
| **L3** | design system, i18n, budget, contrasto | Vitest + lint + script Node | `frontend/roamly-web` | ~2 min |
| **L4** | accessibilità, reduced motion, flussi critici | Playwright + `@axe-core/playwright` | `frontend/roamly-web/e2e` | 4-6 min |

> **Il fatto più importante di questo documento:** costruire `DbContext.Model` **non richiede un database**. Si usa `UseSqlServer` con una connection string mai aperta e si legge `IModel`. È fedele, costa ~200 ms per assembly, e su di esso girano **17 dei 25 verificatori R1-R25**.
>
> ❌ **Non** si usa il provider EF in-memory nemmeno per questo: produce un `Model` **diverso** (ignora delete behavior, precisione, nomi delle constraint, layout della PK) — cioè un modello che non va in produzione.

```csharp
// Il fondamento di L0. Nessun container, nessuna connessione.
var options = new DbContextOptionsBuilder<FullSchemaDbContext>()
    .UseSqlServer("Server=none;Database=none;")
    .Options;
using var ctx = new FullSchemaDbContext(options);
IModel model = ctx.Model;
```

---

## 2. Priorità

**0. Test di convenzione sul modello EF — bloccanti.** Non sono test di comportamento: leggono `DbContext.Model` e falliscono la build se il modello viola **R1, R2, R4-forma, R8, R10** (`SECURITY.md` §2.2), **R26-R29** (chiavi, [`ADR-0008`](../adr/0008-primary-key-strategy.md)) e **R32/R33** (completezza del modello e dei builder). Sono i verificatori più economici e quelli che proteggono tutti gli altri.

> **R8 è il più importante, perché è l'unico che protegge il *futuro*.** Il fallimento che intercetta è: in Phase 2 si aggiunge `JournalEntry`, la slice funziona, i test passano, e per due anni l'export non contiene il diario e la cancellazione lo lascia nel database. Nessun errore, nessun log, nessun sintomo.

**0b. Test di convenzione sul frontend — bloccanti.** Stessa logica sul design system: **R12** (nessun colore letterale), **R13**, **R15**, **R16**, **R22**. Vedi [`ADR-0007`](../adr/0007-design-system.md). Senza di essi la mitigazione del rischio I9 non vale, e ADR-0007 la dichiara esplicitamente *condizionata* alla loro presenza in CI.

**0c. Il doppio test 1785 — bloccante, §5.** È il test che ha reso la #6 un prerequisito tecnico.

Poi, nell'ordine: 1. autenticazione · 2. camper · 3. manutenzione · 4. viaggi · 5. spese · 6. checklist · 7. calcolo distanza e filtro per raggio (unit puri su Haversine/bounding box, **senza database**).

---

## 3. Struttura dei progetti

```text
tests/
├── Roamly.Domain.Tests/            # unit puri, nessuna dipendenza EF
│   ├── Money/ · Geo/ · Trips/
│   └── Identifiers/CombGuidTests.cs        # monotonicità del generatore COMB (ADR-0008)
│
├── Roamly.Model.Tests/             # L0 — convenzioni su IModel. NESSUN database, nessun Docker.
│   ├── ModelFixture.cs                     # IAssemblyFixture: IModel una volta, offline
│   ├── Ownership/                          # R1, R2, R4-forma, R5-manifesto
│   ├── Privacy/                            # R8, R10
│   ├── Keys/                               # R26-R29 — verificatore chiavi di @oracle
│   ├── Schema/
│   │   ├── CascadePathAnalyzerTests.cs     # test A — analizzatore 1785
│   │   └── FullSchemaCompletenessTests.cs  # R32
│   └── TestData/BuilderRegistryCoverageTests.cs   # R33
│
├── Roamly.IntegrationTests/        # L1 + L2 — l'UNICO progetto che possiede il container
│   ├── Infrastructure/
│   │   ├── SqlServerFixture.cs             # IAssemblyFixture: Testcontainers, tag pinnato (R36)
│   │   ├── DatabaseLease.cs                # CREATE DATABASE / Respawn / DROP
│   │   ├── RoamlyApiFactory.cs             # WebApplicationFactory + CookieContainer
│   │   ├── RoamlyClient.cs                 # login vero + antiforgery nascosto (R35)
│   │   └── DatabaseCollection.cs           # [CollectionDefinition] — punto di sblocco G1→G2
│   ├── Schema/                             # test B, test C, topologia FK
│   ├── Privacy/                            # R9, isolamento export, idempotenza, revoca
│   ├── Features/                           # ownership per slice
│   └── TestData/                           # IEntityBuilder, BuilderRegistry, builder concreti
│
└── Roamly.ArchitectureTests/       # Fase 1+, NetArchTest sulle slice verticali

benchmarks/
└── Roamly.Benchmarks/              # misure di @oracle. Riusa SqlServerFixture. MAI in CI (R39)

frontend/roamly-web/
├── src/**/__tests__/               # Vitest + Testing Library (L3)
├── scripts/design-guard/           # R11, R12, R14, R18, R19, R22
└── e2e/                            # Playwright (L4), matrice sui due temi
```

**Perché quattro progetti e non tre**: L0 deve poter girare **senza Docker, in meno di 5 secondi**, anche in un pre-commit hook. Tenerlo dentro `IntegrationTests` gli farebbe trascinare il container e ne annullerebbe il beneficio principale.

**Framework**: **xUnit v3 + Microsoft.Testing.Platform**. Su .NET 10 MTP è il default e VSTest non è più supportato ufficialmente; inoltre le **Assembly Fixtures** di v3 sono esattamente ciò che serve perché **un solo assembly** possieda il container.

### Naming

- **File**: `{Soggetto}Tests.cs`; per le regole invarianti si antepone il codice — `R9_EraseAndSweepTests.cs`. Il codice nel nome è deliberato: dal documento al test deve bastare una **ricerca testuale**.
- **Metodi**: frase in inglese, `snake_case` — `Cross_owner_write_is_rejected_by_the_interceptor`. Leggibile nel report di CI senza aprire il codice.
- `// Arrange` / `// Act` / `// Assert` espliciti solo oltre le ~10 righe.
- Niente `Test1`, niente `Should` nel nome: il nome dice il **comportamento atteso**, non la sintassi dell'assert.

---

## 4. Provisioning e isolamento del database

### 4.1 Un solo motore, un solo percorso

| | |
|---|---|
| **Motore** | **SQL Server vero**, via `Testcontainers.MsSql`, un container per run di assembly |
| **Immagine** | `mcr.microsoft.com/mssql/server:2022-CUxx-ubuntu-22.04`, **tag pinnato in un punto unico**, mai `latest` (**R36**) |
| **Locale e CI** | **identici.** Windows + Docker Desktop in locale, `ubuntu-latest` in CI. Nessun percorso LocalDB, nemmeno opt-in |
| **Proprietario** | **un solo assembly**: `Roamly.IntegrationTests`, via `IAssemblyFixture<SqlServerFixture>` |
| **Vincolo di progettazione** | **nessun test conosce il container.** La fixture espone una sola `ConnectionString`: è ciò che rende basso il costo di passare a `services:` o ad altro |

❌ **Vietati a ogni livello (R30): SQLite e EF Core in-memory.** La motivazione decisiva non è `decimal`/collation/`rowversion` — è che **SQLite non può fallire con l'errore 1785**: il verificatore che giustifica questa intera strategia sarebbe, su SQLite, strutturalmente incapace di diventare rosso. `BannedSymbols.txt` banna `UseSqlite` e `UseInMemoryDatabase`, e un test di convenzione verifica che nessun `.csproj` li referenzi.

⚠️ **Vincolo hardware.** Non esiste immagine SQL Server ufficiale per **ARM64**, e Azure SQL Edge — l'unica via ARM64 che esisteva — è stata **ritirata il 30 settembre 2025**. Su Apple Silicon o Windows on ARM **questa strategia non ha un percorso pulito**: è il trigger T4 di ADR-0009, non un dettaglio operativo.

### 4.2 Isolamento: G1 con Respawn

**Si parte da G1 e non si sale finché non serve.**

| Gradino | Forma | Quando |
|---|---|---|
| **G1** ⭐ **attivo** | un solo database, tutti i test DB in `[Collection("Database")]`, **Respawn** tra un test e l'altro (~50-200 ms) | **dal giorno 1**, fino a ~150 test o ~3 min |
| **G2** | un database per collection (≈ una per slice), `maxParallelThreads: 4` | quando il job supera i 3 min (T1) — **~15 righe di `DatabaseLease`** |
| **G3** | template + `BACKUP`/`RESTORE` | probabilmente mai |

❌ **Niente `TransactionScope` / rollback.** Non regge i test L2 (l'handler apre il proprio `DbContext` su una propria connessione, e forzarlo promuove a transazione distribuita) e non regge `AccountErasureJob`, che gestisce le proprie transazioni.

Configurazione Respawn:

```csharp
_respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
{
    DbAdapter        = DbAdapter.SqlServer,
    SchemasToInclude = ["dbo"],
    TablesToIgnore   = [new Table("__EFMigrationsHistory")],
});
```

- ⚠️ **`ErasureReceipt` non va ignorata.** Non ha FK ed è non-owned: se sopravvivesse, il test di idempotenza dipenderebbe dall'ordine di esecuzione.
- ⚠️ **Un fallimento di Respawn è un segnale.** L'ordine di `DELETE` che deriva dalle FK è **lo stesso** che `AccountErasureJob` percorre: una divergenza va indagata, **mai aggirata** con `TablesToIgnore`.
- ✅ Il mancato reset delle `IDENTITY` è irrilevante: la PK è `Guid` COMB generato **client-side** ([`ADR-0008`](../adr/0008-primary-key-strategy.md)).

### 4.3 Parallelismo (`xunit.runner.json` per assembly)

| Assembly | `parallelizeTestCollections` | `maxParallelThreads` |
|---|---|---|
| `Roamly.Domain.Tests` | `true` | default |
| `Roamly.Model.Tests` | `true` | default |
| `Roamly.IntegrationTests` | **`false`** (G1) | **`4`** |

**Le tre sorgenti di flakiness previste** — e come si prevengono, non come si mascherano:

1. **Deadlock SQL Server** su scritture parallele che toccano `AspNetUsers` e tabelle owned in ordine inverso → G1, o DB distinti in G2.
2. **Container orfani**: un solo assembly possiede il container.
3. **Tempo e fuso** → `TimeProvider` (§8).

❌ **Nessun retry, mai (R37).** Niente attributi di retry, niente loop di ritentativo, niente `Thread.Sleep` speculativi. Un fallimento non riproducibile si **diagnostica**: i retry non risolvono la flakiness, la rendono invisibile.

✅ **R38 — ogni verificatore va visto fallire almeno una volta.** Alla prima scrittura di un verificatore bloccante si introduce deliberatamente la violazione che deve intercettare, si osserva il rosso, si ripristina, e lo si annota nel commit. Un verificatore mai visto fallire è una speranza compilata.

---

## 5. Il doppio test 1785 e lo schema completo

La topologia delle 21 FK di `CONTEXT.md` §5 era **argomentata, non dimostrata**, ed entrambi i casi noti di errore **1785** coinvolgono entità di Phase 2/3/4 — quindi non emergono dalla prima migration. Il test che chiude questo buco **è due test**.

| | Test A — analizzatore | Test B — creazione reale |
|---|---|---|
| **Livello** | L0, nessun database | L1, SQL Server vero |
| **Costo** | **~10 ms** | ~3 s (container già acceso) |
| **Quando** | **ogni push** | **ogni push** |
| **Cosa fa** | calcola sui metadati di `IModel` tutti i cammini di azione referenziale e asserisce **al massimo uno** per coppia di tabelle | esegue `EnsureCreatedAsync()` dello **schema completo** e asserisce che non sollevi 1785 |
| **Valore proprio** | **nomina entrambi i cammini in conflitto.** L'errore 1785 reale dice quale FK ha rifiutato, **non qual è l'altro percorso** | dimostra che il test A **dice il vero** |

```csharp
// Test A — Roamly.Model.Tests/Schema/CascadePathAnalyzerTests.cs
static bool IsPath(IForeignKey fk) => fk.DeleteBehavior is
    DeleteBehavior.Cascade or DeleteBehavior.SetNull or DeleteBehavior.ClientSetNull;

var offenders = CascadeGraph.EnumerateAllPaths(edges)
    .GroupBy(p => (p.Source, p.Target))
    .Where(g => g.Count() > 1)
    .ToList();

Assert.True(offenders.Count == 0, "Errore 1785 in arrivo. Percorsi multipli:\n" + ...);
```

**Test B**, oltre a `EnsureCreatedAsync`, asserisce **cinque** proprietà strutturali:

| # | Asserzione | Fonte |
|---|---|---|
| 1 | ogni FK `OwnerId → AspNetUsers` è `NO ACTION` | `DATA.md` §6 |
| 2 | ogni PK è `(OwnerId, Id)`, **CLUSTERED**, e `CreatedAtUtc` **non** è nella clustering key | ADR-0008, R26-R29 e R42 |
| 3 | **nessuna chiave alternata esiste** — ADR-0008 le ha eliminate tutte | ADR-0008 |
| 4 | ogni FK figlia è **composita** e punta alla PK del padre (il "morso" di R4, **una volta sola**) | ADR-0003 |
| 5 | le colonne `Money` sono `decimal(19,4)`, `Coordinates` `decimal(8,6)`/`decimal(9,6)` | `CONTEXT.md` §2.1 |

**Test C — le migration Phase 1 si applicano davvero** (L1, ~2 s, ogni push): `MigrateAsync()` su DB vuoto e `GetPendingMigrationsAsync()` vuoto. Chiude `DATA.md` §5 e impedisce la deriva modello ↔ migration.

> ⚠️ **Divergenza dichiarata.** Il test B verifica il **modello** via `EnsureCreated`; il test C verifica le **migration**. Sono cose diverse, e servono entrambe: il modello completo **non ha migration e non deve averne** (`CONTEXT.md` §2.6, niente tabelle vuote in produzione). Quando Phase 2 arriverà, la sua migration andrà confrontata **a mano** con ciò che il test B creava.

> ⚠️ **A e B non dipendono l'uno dall'altro in CI** (niente `needs:`). Devono poter fallire **entrambi**: la loro discordanza è informazione — significa che l'analizzatore è sbagliato e va corretto prima di proseguire.

### 5.1 Prerequisito: i POCO strutturali

`FullSchemaDbContext` richiede che le classi C# di `Trip`, `TripStop`, `Checklist`, `ChecklistItem`, `JournalEntry`, `Expense`, `SavedPlace`, `Document` **esistano**. "Topologia decisa, tabella non creata" **non** significa "codice non scritto".

✅ Vivono in **`Roamly.Domain`** come POCO **strutturali** (`Id`, `OwnerId`, `CreatedAtUtc`, FK composite, navigazioni) con `IEntityTypeConfiguration` completa, e sono registrati **solo in `FullSchemaDbContext`**, mai in `RoamlyDbContext`. Così il codice validato oggi è **lo stesso** che arriverà in Phase 2.

❌ Entità "ombra" nel progetto di test: dimostrerebbero la topologia di un modello che nessuno implementa; il giorno in cui Phase 2 divergesse, il test resterebbe **verde** mentre la garanzia evapora.

✅ **R32 — meta-verificatore obbligatorio** (L0, ~10 ms): `DomainModelManifest.AllEntityTypes` vs `FullSchemaModel.GetEntityTypes()`. Senza, fra sei mesi qualcuno aggiunge un'entità, non la registra, e il test 1785 resta verde **su uno schema incompleto**.

---

## 6. Mappatura regole → livello

Questa tabella è **normativa** (R31): spostare una regola di livello richiede di aggiornarla.

### 6.1 Ownership — R1-R7 ([`ADR-0003`](../adr/0003-auth-and-ownership.md))

| Regola | Livello | Come | Costo |
|---|---|---|---|
| **R1** ownership universale | **L0** | ogni entità di `GetEntityTypes()` implementa `IOwnedResource` o è in whitelist annotata. I complex type non compaiono per costruzione → nessuna whitelist a mano | ~5 ms |
| **R2** filtro `"OwnerScope"` | **L0** | ogni entità owned ha un filtro **nominato**; nessun'altra ne ha uno | ~5 ms |
| **R3** scrittura cross-owner | **L1** | **due test**: (a) interceptor — `RunAsUser(A)` modifica una riga di B → `OwnershipViolationException`; (b) assegnazione — `OwnerId` è A anche se il request model dichiarasse B | ~200 ms |
| **R4** coerenza gerarchica | **L0** + **L1 una volta** | L0: ogni FK figlia è composita, include `OwnerId`, punta alla **PK `(OwnerId, Id)`** del padre. L1: `INSERT` con `OwnerId` divergente rifiutato — **nel test B, non per slice** | ~5 ms + ~200 ms una tantum |
| **R5** API bandite | **compilazione** | `BannedApiAnalyzers` + `TreatWarningsAsErrors`. **Non è un test.** L0 di secondo livello: `BannedSymbols.txt` contiene l'elenco esatto di `ARCHITECTURE.md` §4, altrimenti il file si svuota in silenzio | 0 s |
| **R6** identità presente | **L0 + L2** | L0: `ICurrentUser` senza identità lancia. L2: endpoint owner-scoped senza cookie → `401`, mai `200` con lista vuota | ~150 ms |
| **R7** indistinguibilità | **L2** | **non è una regola di database**: `404` con `ProblemDetails` **byte-identico** a quello di un id inesistente | ~150 ms/riga |

### 6.2 Chiavi — R26-R29 ([`ADR-0008`](../adr/0008-primary-key-strategy.md))

Il verificatore di @oracle **vive in `Roamly.Model.Tests/Keys/`** ed è L0, con una sola controparte L1.

| Cosa | Livello |
|---|---|
| PK = **esattamente 2 colonne**, ordine `(OwnerId, Id)` | L0 |
| **Nessuna chiave alternata** su alcuna entità (`GetKeys().Count() == 1`) | L0 |
| Nessuna FK con `SetNull`/`ClientSetNull` | L0 |
| `Id` è `ValueGeneratedNever()` | L0 |
| PK **CLUSTERED** (annotazione `SqlServer:Clustered` sull'`IKey`) | **L0**, ma **solo via `IDesignTimeModel`** — vedi §8.2 |
| `CreatedAtUtc` fuori dalla clustering key | L0, stessa lettura |
| Monotonicità del generatore COMB | L0, in `Domain.Tests` |

### 6.3 Privacy — R8-R10 ([`ADR-0004`](../adr/0004-privacy-and-erasure.md))

Vedi §9 per l'elenco completo dei test di privacy.

| Regola | Livello | Costo |
|---|---|---|
| **R8** copertura universale (entità owned vs grafi di export/cancellazione, **entrambi derivati a runtime**) | **L0 — il più importante** | ~10 ms |
| **R9** erase and sweep | **L1, non negoziabile** | ~2-5 s (il test più lento della suite) |
| **R10** residuo non personale (whitelist non-owned + `ErasureReceipt` senza FK verso `AspNetUsers`) | **L0** | ~5 ms |

### 6.4 Design system — R11-R25 ([`ADR-0007`](../adr/0007-design-system.md)): **nessuna tocca il database**

| Regole | Livello | Come |
|---|---|---|
| R11, R12, R13, R15, R16, R17, R21, R24, R25 | **L3** | lint, `design:guard`, `dependency-cruiser`, test di render |
| **R14** matrice 47 coppie × 2 temi | **L3** | script che **parsa `theme.css`** e ricalcola WCAG · **+ test di copertura del manifesto** — ⚠️ è la R8 del design system, la seconda metà è quella che protegge il futuro |
| **R18 / R22** budget font, Fraunces per eccezione | **L3** | somma byte di `public/fonts/**/*.woff2` + lint CSS + conteggio occorrenze per pagina |
| **R19** nessun asset di terzi | **L3** | ⚠️ **deve girare sul bundle prodotto**, non sul sorgente: una dipendenza npm può iniettare un `@import` da CDN |
| **R20** reduced motion | **L4** | Playwright con `prefers-reduced-motion: reduce`: nessun elemento con `opacity < 1` computata |
| **R23** date dal server | **L2 + L3** | L2: la risposta contiene `deletionScheduledForUtc`. L3: lint contro aritmetica su date in `features/privacy/**` |

> **Esito complessivo: dei 25 verificatori R1-R25, 17 non toccano alcun database**, 5 richiedono SQL Server, 3 richiedono un browser. Il budget di tempo della CI è dominato da **5 regole su 25** — ed è per questo che il job veloce è separato da quello di integrazione.

---

## 7. Test obbligatori per slice

Per ogni slice che espone un `{id}`: **test di ownership**, parte della definition of done. Vedi `SECURITY.md` §2 e [`ADR-0003`](../adr/0003-auth-and-ownership.md).

- **Non solo lettura.** Il query filter copre le letture; le scritture no. Il test deve coprire "utente A **modifica o cancella** la risorsa di B", altrimenti verifica il livello già protetto e ignora quello a rischio.
- **Deve costare una riga.** Un test di ownership che richiede 20 righe di setup viene saltato sotto pressione.

```csharp
[Theory]
[OwnershipMatrix<CampersSlice>]   // deriva (metodo, rotta) dagli endpoint registrati
public Task Cross_owner_access_is_indistinguishable_from_absence(HttpMethod m, string route)
    => Ownership.AssertNotFoundForOther(m, route);
```

### 7.1 Endpoint autenticati con cookie

Gli integration test non possono usare un header `Authorization`. Servono:

- un `WebApplicationFactory` che esegua un **vero login** e conservi il cookie in un `HttpClient` con `CookieContainer`;
- due client preconfigurati (`AsUserA`, `AsUserB`), fondamento dei test di ownership.

**Antiforgery (R35): resta attivo.** ❌ È **vietato** disattivarlo nell'ambiente di test: smetterebbe di testare una barriera reale. ✅ L'estrazione del token vive **dentro `RoamlyClient`** e **non compare mai nel corpo di un test** — è l'unica conciliazione possibile tra "antiforgery attivo" e "un test di ownership costa una riga". Senza questa regola scritta, la tensione si risolve da sola nel modo sbagliato.

⚠️ **`IEmailSender` fake — sorgente di flakiness numero uno.** ADR-0003 richiede la conferma email per il login. Il fake **non deve** esporre "l'ultimo token generato": con un database condiviso (G1) è **stato globale mutabile** e due test che registrano un utente si rubano il token a vicenda. Deve essere un **dizionario per indirizzo**.

---

## 8. `TimeProvider` — il tempo è un input iniettato

**R34, dal primo commit.** `CreatedAtUtc`, i 30 giorni di grazia di ADR-0004 e `currentStale` a 90 giorni (`CONTEXT.md` §3.9) rendono il tempo un **input di dominio**.

- ❌ `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow` nel codice di produzione: sono in `BannedSymbols.txt` → **errore di compilazione**, non test.
- ✅ Ogni handler e ogni job riceve `TimeProvider` iniettato.
- ✅ I test usano `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`).

> Senza questo, i test di revoca, di grazia scaduta e di lettura stantia **non sono scrivibili** o sono flaky. Aggiungerlo dopo significa toccare **ogni** handler che scrive `CreatedAtUtc`: è additivo solo il primo giorno.

### 8.1 La sintassi di `BannedSymbols.txt` è essa stessa un rischio — reperto del Blocco 1

`UtcNow` è una **proprietà**, quindi la voce deve essere `P:System.DateTime.UtcNow`. Scritta come `M:System.DateTime.get_UtcNow` — la forma del *getter*, plausibile e sbagliata — l'analyzer **non segnala nulla e non protesta**: le voci che non risolvono a un simbolo vengono ignorate in silenzio.

Nella prima stesura di questo progetto tutte e quattro le voci temporali erano in quella forma. Il file esisteva, la build era verde, e il divieto **non era in vigore**. È stato scoperto solo applicando **R38** — scrivere una violazione deliberata e pretendere di vedere il rosso — e sarebbe altrimenti passato per settimane, fino al primo `DateTime.UtcNow` in un handler.

> **Conseguenza operativa.** Le due voci `UseSqlite` e `UseInMemoryDatabase` (R30) **non sono ancora verificate**: i rispettivi pacchetti non sono referenziati, quindi non c'è modo di distinguere "divieto attivo" da "voce non risolta". Vanno messe alla prova nel momento esatto in cui qualcuno aggiunge uno di quei pacchetti — che è anche l'unico momento in cui servono davvero.

---

### 8.2 `DbContext.Model` non contiene le annotazioni del provider — reperto del Blocco 2

`ModelFixture` (Blocco 3) **non deve leggere `DbContext.Model`**, ma:

```csharp
var model = context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;
```

`DbContext.Model` è il **modello read-optimized** di runtime, da cui EF Core **rimuove le annotazioni che servono solo alla generazione dello schema** — fra cui `SqlServer:Clustered`. Leggendolo, `key.FindAnnotation("SqlServer:Clustered")` restituisce `null` su **tutte** le entità, e `key.IsClustered()` non restituisce `false`: **lancia** `InvalidOperationException` con il messaggio *"The requested configuration is not stored in the read-optimized model, please use `DbContext.GetService<IDesignTimeModel>().Model`"*.

> **Perché è un rischio e non un dettaglio.** Un verificatore di R26 scritto con `FindAnnotation` su `DbContext.Model` è **rosso su tutto** — rumoroso, quindi innocuo: ci si accorge subito. Ma la forma speculare, `FindAnnotation(...) is not null` usata come guardia permissiva, sarebbe **verde su tutto** e il controllo `IsClustered` **non sarebbe mai in vigore**. È lo stesso schema di fallimento silenzioso di §8.1, su un'annotazione diversa.

**Conseguenza operativa:** la verifica *PK CLUSTERED* **scende da L1 a L0** (§6.2). Non richiede Docker, non richiede il test B, costa millisecondi e vale su tutte e 13 le entità owned. Il test B del Blocco 4 resta necessario per ciò che solo il database può dire: l'errore 1785 e l'**ordine delle colonne in `REFERENCES`** (R43).

> ⚠️ **Il tipo sta in `Microsoft.EntityFrameworkCore.Metadata`, non in `...Infrastructure`**, contrariamente a quanto suggerisce la memoria: la documentazione lo colloca spesso nel secondo. Verificato per riflessione sull'assembly `Microsoft.EntityFrameworkCore.dll` 10.0.12.

---

## 9. Test data builder

Un `Camper` valido ha ~15 campi. Senza una convenzione di builder ogni test diventa 20 righe di setup, e i test smettono di essere scritti.

**Forma (R33):**

```csharp
public interface IEntityBuilder
{
    Type EntityType { get; }
    object Build(BuilderContext ctx);   // istanza valida e minimale, agganciata al padre
}
```

`BuilderRegistry` scopre le implementazioni per riflessione e le ordina **topologicamente sul grafo delle FK derivato da `IModel`** — lo stesso ordinamento che `AccountErasureJob` percorre al contrario. **Nessuna lista scritta a mano da nessuna parte.**

✅ Con la PK COMB client-side ([`ADR-0008`](../adr/0008-primary-key-strategy.md)) il builder conosce l'`Id` **prima** dell'`INSERT`: aggancia i figli senza round-trip e senza `SaveChanges` intermedi.

Il builder di `SavedPlace`/`TripStop` usa due `decimal` (complex type `Coordinates`) invece di un `Point` NTS: setup più semplice, nessun SRID da impostare.

**Verificatore di completezza** (L0, ~10 ms, bloccante): entità owned da `IModel` vs `BuilderRegistry.Discover()`. Il messaggio d'errore dichiara **la conseguenza**, non il sintomo:

> *"Entità owned senza builder: `JournalEntry`. Il test R9 'erase and sweep' NON la coprirebbe: sarebbe verde su una cancellazione parziale."*

⚠️ **Debolezza dichiarata:** il verificatore garantisce che il builder **esista**, non che sia **significativo**. Convenzione non automatizzabile, verificabile solo in review: *il builder popola tutti i campi non-nullable e almeno un campo nullable per complex type.*

---

## 10. Test di privacy

Derivano da R8/R9/R10 ([`ADR-0004`](../adr/0004-privacy-and-erasure.md)).

| # | Test | Livello |
|---|---|---|
| a | **Completezza dell'export**: ogni entità owned del modello compare nello ZIP (R8) | **L0**, bloccante |
| b | **Isolamento dell'export**: l'`OwnerId` di B non compare **nei byte** dello ZIP di A | **L1** |
| c | **Erase and sweep**: dopo il job, `COUNT(*) = 0` per ogni entità owned **iterando sul modello**; il `DELETE` finale della riga utente **riesce** (R9) | **L1**, DB reale |
| d | **Idempotenza**: il job rieseguito non lancia e non altera la `ErasureReceipt` | **L1** |
| e | **Annullamento**: richiesta → +10 giorni → revoca → account pieno | **L1 + `FakeTimeProvider`** |
| f | **Accesso bloccato**: durante la grazia il login non riesce | **L2** |

Il test (c) non è "per slice": è **globale**, e **popola via `BuilderRegistry`**, mai a mano. La catena è chiusa: **modello → registro → popolamento → verifica sul modello.** Un'entità nuova senza builder rompe la build a **L0 in 10 ms**, invece di produrre un R9 silenziosamente parziale.

---

## 11. Test di interfaccia e accessibilità

Derivano da R14, R17, R20, R23, R25 ([`ADR-0007`](../adr/0007-design-system.md)). L'accessibilità non è una rifinitura di fine fase: ADR-0004 ha reso la chiarezza di una conferma irreversibile un **requisito di sicurezza**, e una conferma non raggiungibile da tastiera è una conferma ambigua.

| # | Test | Livello |
|---|---|---|
| a | **Matrice di contrasto**: le 47 coppie testo/sfondo rispettano WCAG AA **su entrambi i temi** (R14) | **L3**, bloccante |
| b | **Copertura del manifesto**: ogni token usato nel codice esiste nel manifesto, e nessun token dichiarato è orfano | **L3**, bloccante |
| c | **`axe` sulle pagine principali**, eseguito **due volte**, una per tema | **L4** (PR + nightly) |
| d | **`prefers-reduced-motion`**: nessuna animazione oltre la soglia dichiarata (R20) | **L4** |
| e | **Cancellazione account da sola tastiera**: flusso percorribile, conferma mai raggiungibile per errore | **L4** |
| f | **Date di cancellazione dal server**: il client le mostra, non le ricalcola (R23) | **L2 + L3** |
| g | **`Ribbon` senza lettura corrente**: degrada sull'asse temporale, nessun km inventato (R25) | **L3** |

> **Il test (a) è la mitigazione di un rischio accettato consapevolmente.** La dark mode in Phase 1 raddoppia la matrice: senza questo verificatore il degrado avviene in silenzio, perché nessuno rilegge 47 coppie a mano a ogni modifica di token.

---

## 12. Pipeline

### 12.1 `ci.yml` — quattro job paralleli

| Job | Trigger | Contenuto | Tempo atteso |
|---|---|---|---|
| **`backend-fast`** | ogni push + PR | restore → build con `TreatWarningsAsErrors` + `BannedApiAnalyzers` (R5, R30, R34) → `Domain.Tests` + `Model.Tests` (**tutto L0**, incluso l'analizzatore 1785, R26-R29, R32, R33) | **90-120 s** |
| **`backend-integration`** | ogni push + PR | Testcontainers → **test B** → **test C** → R3, R7, R9, privacy, ownership per slice | **3-4 min** |
| **`frontend`** | ogni push + PR | `npm ci` → ESLint + stylelint → `design:guard` → `contrast:check` → Vitest → `vite build` → `fonts:budget` + `no-external-assets` **sul bundle** | **2-3 min** |
| **`e2e`** | solo PR + nightly | Playwright: `axe` × 2 temi, reduced-motion, cancellazione da tastiera | **4-6 min** |

**Tempo di parete:** push ≈ **3-4 min** · PR ≈ **5-7 min**. Entrambi sotto la soglia del "si smette di guardare".

⚠️ `backend-fast` **non** è un gate di `backend-integration` (niente `needs:`): devono poter fallire entrambi.

### 12.2 `nightly.yml`

| Cosa | Perché |
|---|---|
| Suite completa incluso E2E su entrambi i temi | copertura piena senza pesare sul loop di sviluppo |
| Test B **anche** contro il tag SQL Server successivo a quello pinnato | si scopre che un CU cambia comportamento **prima** di doverci aggiornare |
| `dotnet ef migrations bundle` generato ed eseguito su DB vuoto | `DEVOPS.md` §2.1 |
| `dotnet list package --vulnerable --include-transitive` | sicurezza a costo zero |

### 12.3 Pre-commit locale (facoltativo, raccomandato)

**Solo L0 + lint: < 10 s, nessun Docker.** Un pre-commit che avvia un container viene disinstallato entro una settimana.

### 12.4 Benchmark

**R39: i benchmark non girano in CI.** `Roamly.Benchmarks` riusa `SqlServerFixture` parametrizzata (4 GB di RAM, nessun Respawn, dataset 10⁵-10⁶ righe) e si esegue **solo in locale**. Sui runner GHA condivisi un numero assoluto è rumore presentato come dato.

---

## 13. Comandi

> ⚠️ **La CLI è cambiata in .NET 10 RTM.** VSTest è stato rimosso e il runner si seleziona in `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`) — **non** in `dotnet.config`, sintassi valida solo fino a RC1, né con `TestingPlatformDotnetTestSupport`, che attiva il bridge VSTest deprecato. Con il nuovo runner il percorso di un progetto **non è più posizionale**: serve `--project` o `--solution`.

```powershell
# L0 — nessun Docker, < 5 s. È quello da tenere aperto mentre si lavora.
dotnet test --project tests/Roamly.Domain.Tests
dotnet test --project tests/Roamly.Model.Tests

# L1 + L2 — richiede Docker in esecuzione. Primo avvio a freddo: 2-4 min.
dotnet test --project tests/Roamly.IntegrationTests

# Tutto
dotnet test --solution Roamly.slnx

# Frontend
npm run lint && npm run design:guard && npm run contrast:check && npm run test

# E2E
npm run e2e
```

> ⚠️ **Un progetto di test senza test fa fallire il comando — reperto del Blocco 2.** Microsoft.Testing.Platform tratta *"zero test eseguiti"* come **fallimento**, con **exit code 8**, non come successo vacuo. Oggi `dotnet test --solution Roamly.slnx` esce `8` pur con `non riuscito: 0`, perché `Roamly.Domain.Tests` e `Roamly.IntegrationTests` sono ancora vuoti. Si risolve da sé nei Blocchi 3-4, quando quei progetti ricevono i loro test.
> Va però ricordato in `ci.yml` (Blocco 5): **un filtro `--filter` che non seleziona nulla rende il job rosso**, e il messaggio (`non riuscito: 0`) non lo fa sembrare un errore. È l'opposto del fallimento silenzioso di §8.1 — qui è il *successo* a essere silenzioso — ma la lezione è la stessa: leggere l'exit code, non il riepilogo.

---

## 14. Tempi misurati

> ⚠️ **Da compilare al Blocco 4 del primo giorno.** I valori di ADR-0009 sono **stime di ordine di grandezza** (F-12), non misure. Una strategia che dichiara tempi mai misurati invecchia male.

| Grandezza | Stima | Misurato il | Valore reale |
|---|---|---|---|
| Suite L0 a progetto singolo, build inclusa (1 test) | < 5 s | Blocco 1 | **3,9 s** di esecuzione, 18,8 s a freddo con build |
| `dotnet build` dell'intera solution, 7 progetti, incrementale | — | Blocco 2 | **11,1 s**, 0 avvisi, 0 errori |
| Costruzione offline di `FullSchemaDbContext.Model` (21 entity type) | — | Blocco 2 | inclusa nei **6,6 s** della suite L0 con sonda, mai connessa |
| Pull immagine su runner GHA (non cachata) | 40-70 s | — | — |
| Readiness del container | 20-45 s | — | — |
| `CREATE DATABASE` vuoto (container caldo) | 150-400 ms | — | — |
| `EnsureCreated` schema completo | 1-3 s | — | — |
| `Respawn.ResetAsync()` | 50-200 ms | — | — |
| Migration Phase 1 su DB vuoto | 1-2 s | — | — |
| Job `backend-fast` | 90-120 s | — | — |
| Job `backend-integration` | 3-4 min | — | — |

Il tempo del job `backend-integration` va **riannotato a ogni revisione di questo documento**: è l'indicatore che fa scattare il passaggio da G1 a G2 (trigger T1).
