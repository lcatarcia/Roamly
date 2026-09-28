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

> **Il fatto più importante di questo documento:** costruire il modello EF **non richiede un database**. Si usa `UseSqlServer` con una connection string mai aperta e si legge `IModel`. È fedele, costa ~200 ms per assembly, e su di esso girano **17 dei 25 verificatori R1-R25**.
>
> ❌ **Non** si usa il provider EF in-memory nemmeno per questo: produce un `Model` **diverso** (ignora delete behavior, precisione, nomi delle constraint, layout della PK) — cioè un modello che non va in produzione.

```csharp
// Il fondamento di L0. Nessun container, nessuna connessione.
var options = new DbContextOptionsBuilder<FullSchemaDbContext>()
    .UseSqlServer("Server=none;Database=none;")
    .Options;
using var ctx = new FullSchemaDbContext(options);

// ⚠️ NON `ctx.Model`: e' il modello read-optimized, privo delle annotazioni
// del provider (fra cui SqlServer:Clustered). Vedi §8.2 — e' un reperto, non
// una preferenza stilistica.
IModel model = ctx.GetService<IDesignTimeModel>().Model;
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
├── Roamly.TestSupport/             # LIBRERIA, non progetto di test. Riferita da L0 e da L1.
│   ├── IEntityBuilder.cs · BuilderContext.cs   # ctx.Parent<T>() tipizzato, FakeTimeProvider
│   └── Builders/                           # 13 builder concreti (le entità owned)
│
├── Roamly.Model.Tests/             # L0 — convenzioni su IModel. NESSUN database, nessun Docker.
│   ├── ModelFixture.cs                     # IAssemblyFixture: IModel una volta, offline
│   ├── Ownership/                          # R1, R2, R4-forma, R5-manifesto
│   ├── Privacy/                            # R8, R10
│   ├── Keys/                               # R26-R29, R40 — verificatore chiavi di @oracle
│   ├── Conventions/                        # R30, R36, forma dei .csproj sotto tests/
│   ├── Schema/
│   │   ├── CascadePathAnalyzerTests.cs     # test A — analizzatore 1785
│   │   └── FullSchemaCompletenessTests.cs  # R32
│   └── TestData/BuilderRegistryCoverageTests.cs   # R33
│
├── Roamly.IntegrationTests/        # L1 + L2 — l'UNICO progetto che possiede il container
│   ├── Infrastructure/
│   │   ├── SqlServerFixture.cs             # IAssemblyFixture: Testcontainers, tag pinnato (R36)
│   │   ├── DatabaseLease.cs                # CREATE DATABASE / Respawn / DROP
│   │   ├── FullSchemaDatabase.cs           # esito di EnsureCreated (test B)
│   │   ├── Phase1Database.cs               # esito di MigrateAsync (test C)
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

> ✅ **Risolta al Blocco 4.** L'albero qui sopra è aggiornato. La versione originale collocava `IEntityBuilder`, `BuilderRegistry` e i builder concreti in `Roamly.IntegrationTests/TestData/`, ma il loro verificatore di copertura (R33, ADR-0009) in `Roamly.Model.Tests/TestData/`. **Non era implementabile**: un progetto di test non referenzia un altro progetto di test, e non deve iniziare a farlo.
>
> La soluzione è **`tests/Roamly.TestSupport`**, una **libreria** — non un progetto di test — riferita sia da L0 sia da L1. R33 resta L0, e la tabella normativa §6.1 resta invariata. Le alternative scartate e le ragioni sono nell'analisi di @archimedes.
>
> ⚠️ **`tests/Directory.Build.props` è condizionata su `IsTestProject`**, altrimenti `TestSupport` erediterebbe `OutputType=Exe` e il runner MTP, e la soluzione uscirebbe con **exit code 8** ("zero test") su una libreria che test non ne ha per definizione. Il discriminatore è il suffisso `Tests` nel nome, definito nella props di radice. Un test di convenzione in `Model.Tests/Conventions/` lo presidia.

**Perché cinque progetti sotto `tests/` e non tre**: L0 deve poter girare **senza Docker, in meno di 5 secondi**, anche in un pre-commit hook. Tenerlo dentro `IntegrationTests` gli farebbe trascinare il container e ne annullerebbe il beneficio principale.

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

**Test C — le migration Phase 1 si applicano davvero** (L1, ~0,5 s, ogni push): `MigrateAsync()` su DB vuoto e `GetPendingMigrationsAsync()` vuoto. Chiude `DATA.md` §5 e impedisce la deriva modello ↔ migration.

> ✅ **Implementato al Blocco 5** in `Schema/Phase1MigrationTests.cs`, con **cinque** fatti e non uno. La forma letterale qui sopra — «`MigrateAsync` e poi `GetPendingMigrations` vuoto» — **non intercetta la deriva che il test C esiste per intercettare**: `GetPendingMigrations` confronta l'assembly delle migration con `__EFMigrationsHistory`, e **non sa nulla del modello**. Aggiunta l'asserzione che porta il peso, `Database.HasPendingModelChanges()`, che confronta lo *snapshot* della migration con il modello corrente. Più due asserzioni strutturali (**PK `(OwnerId, Id)` CLUSTERED** e **ordine delle colonne in `REFERENCES`**) lette da `sys.*` sul database **migrato**, che è un database diverso da quello del test B.
>
> ⚠️ **Reperto: `MigrateAsync()` di EF Core 10 rifiuta di applicare le migration se il modello ha modifiche in sospeso** (`PendingModelChangesWarning` promosso a eccezione). La deriva viene quindi intercettata **due volte**, e la prima è così rumorosa da far fallire tutti e cinque i fatti della classe. È una buona notizia, ma non rende superflua `HasPendingModelChanges()`: è quella a nominare la causa in modo leggibile, ed è l'unica che resterebbe in vigore se un giorno quel warning venisse configurato diversamente.

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

> ✅ **Confermato al Blocco 3 in forma dimostrabile.** Il verificatore di R26 legge ora `IsClustered()` su `IDesignTimeModel` e ottiene `False` — un valore, non `null` e non un'eccezione. La differenza tra «annotazione assente» e «annotazione presente e falsa» è esattamente ciò che separa un verificatore inerte da uno in vigore.

### 8.3 R32 è cieco sulle entità raggiungibili per navigazione

**Reperto del Blocco 3.** R32 (ADR-0009) confronta `DomainModelManifest.AllEntityTypes` con gli entity type di `FullSchemaDbContext`. Ma EF Core **scopre per convenzione** ogni tipo raggiungibile per navigazione da un tipo già mappato: togliere la `IEntityTypeConfiguration` di un'entità **non la rimuove dal modello**, e R32 resta **verde**.

Ciò che diventa rosso in quel caso sono R26-R29, perché l'entità scoperta per convenzione prende `PK(Id)` semplice invece di `(OwnerId, Id)` clusterizzata. **La rete regge**, ma per una ragione diversa da quella scritta.

> **Conseguenza operativa.** R32 protegge davvero il caso che ADR-0009 §*Modo A vs modo B* descrive — un'entità nuova **non raggiungibile** da nessuna navigazione esistente, cioè una radice di aggregato nuova. Non protegge dalla rimozione di una configuration. Il verificatore resta necessario e resta scritto così; è la sua **motivazione** che va letta con questa precisazione, altrimenti si scambia per copertura ciò che è copertura di R26-R29.

### 8.4 `InvariantGlobalization` rompe `Microsoft.Data.SqlClient`

**Reperto del Blocco 4.** Il `Directory.Build.props` di radice impostava `InvariantGlobalization=true` sull'intera soluzione — una scelta ragionevole e comune, che qui è **incompatibile con il driver di accesso ai dati**. La prima `SqlConnection.OpenAsync` lancia:

```
System.NotSupportedException : Globalization Invariant Mode is not supported.
```

Verificato empiricamente rimuovendo il fix e osservando fallire 8 test di integrazione, nello spirito di R38.

> **Perché è un rischio e non un dettaglio.** Il fallimento si manifesta **solo** quando qualcuno apre davvero una connessione. `Roamly.Api` ha ereditato quella riga dal primo commit **senza mai romperne nulla**, perché era il template `dotnet new web`: sarebbe esplosa al Blocco 5 o alla prima slice, con un messaggio che non nomina né il `.csproj` né la proprietà responsabile, mentre si sta debuggando altro. È il terzo caso di configurazione che *sembra* attiva e innocua e non lo è, dopo §8.1 e §8.2 — con la differenza che qui la modalità **era** in vigore: a mancare era la conoscenza della sua conseguenza.

**Risoluzione:** la proprietà è stata **rimossa dalla props di radice** (il default .NET è `false`), con un commento che spiega perché non va reintrodotta. Non è un'eccezione locale a un progetto: `Roamly.Api` e `Roamly.IntegrationTests` aprono entrambi connessioni, e `Money.Currency` rende le regole di globalizzazione parte del dominio.

### 8.5 R43 è verde: EF Core 10 emette `REFERENCES` nell'ordine della PK

**Reperto del Blocco 4, e chiusura dell'ultimo punto aperto di [`ADR-0008`](../adr/0008-primary-key-strategy.md).** Quell'ADR dichiarava un solo punto empirico non verificato: se EF Core 10 emetta `REFERENCES Campers (OwnerId, Id)` nell'ordine della PK, o riordini le colonne secondo l'ordine di dichiarazione della FK. Un ordine invertito produce uno schema **sintatticamente valido e semanticamente sbagliato**, che — a differenza di 1785 — **non fallisce affatto**.

Verificato su SQL Server reale interrogando `sys.foreign_key_columns`: **tutte** le FK composite elencano le colonne nell'ordine della PK. **Il *Piano B* di ADR-0008 non serve**, e il trigger di revisione **T6** non scatta.

---

### 8.6 «La PK è CLUSTERED» è vero anche quando nessuno lo ha chiesto — reperto del Blocco 5

**Sesto caso della serie, e il primo che riguarda un verificatore già in produzione.** Applicando R38 al test C si è rimossa deliberatamente l'annotazione `.Annotation("SqlServer:Clustered", true)` dalla `PK_Equipment` del file di migration, aspettandosi il rosso. **La suite è rimasta verde.**

La ragione è che SQL Server crea una `PRIMARY KEY` come **CLUSTERED per default** quando la tabella non ha già un indice clusterizzato. Su una tabella creata da zero, «annotazione assente» e «annotazione presente e `true`» producono **lo stesso schema fisico**, e nessuna interrogazione di `sys.indexes` può distinguerli.

Che l'asserzione sia comunque **in vigore** è stato dimostrato nella direzione opposta: con `.Annotation("SqlServer:Clustered", false)` il test diventa rosso e nomina la tabella (`Equipment: la PK creata dalla migration non e' CLUSTERED`).

> **Conseguenza operativa.** L'asserzione *PK CLUSTERED* letta da `sys.*` — sia quella del test B sia quella nuova del test C — protegge da un **`IsClustered(false)` esplicito**, non dalla **perdita silenziosa della dichiarazione**. La copertura di quel secondo caso sta altrove, ed è già attiva: **R26 a L0** legge `IsClustered()` su `IDesignTimeModel`, dove la differenza fra `null`, `false` e `true` esiste davvero (§8.2). Le due verifiche non sono ridondanti, come poteva sembrare: coprono due metà diverse della stessa regola.
>
> ⚠️ Il caso in cui il default smetterebbe di salvare è una tabella che acquisisse un **altro** indice clusterizzato prima della PK — cioè un `ALTER`, non un `CREATE`. Non accade oggi; accadrà alla prima migration correttiva su tabella esistente.

---

### 8.7 R41 è dichiarata in due documenti e non esiste in nessuno dei due — reperto della decisione #5

**Settimo caso della serie, e il primo trovato leggendo la documentazione anziché eseguendo un test.** Istruendo la decisione #5, @archimedes ha citato `ARCHITECTURE.md` §3, che dichiarava: *`BannedSymbols.txt` vieta `Guid.NewGuid()` nel dominio (R41)*. La verifica diretta del file lo smentisce.

`BannedSymbols.txt` contiene **sei voci** — quattro temporali (`P:System.DateTime.UtcNow`, `.Now`, e le due `DateTimeOffset`) e due sui provider EF (`UseSqlite`, `UseInMemoryDatabase`) — e **nessuna su `Guid`**. Non esiste alcun altro `BannedSymbols` nella soluzione.

Il punto meno ovvio è che **la fonte normativa prescriveva un meccanismo diverso**: ADR-0008 R41 chiede un *«regex su `src/**/Domain/**` che vieta quelle chiamate fuori da `Roamly.Common.SequentialGuidGenerator`»*, e vieta `Guid.CreateVersion7()` **ovunque nella persistenza**. BannedSymbols non sarebbe comunque stato lo strumento giusto: deve consentire la chiamata **dentro** il generatore, e `BannedApiAnalyzers` non conosce le eccezioni per percorso.

Quindi R41 non è un verificatore rotto: **non è mai stato scritto**, e due documenti indipendenti affermavano il contrario — uno sbagliando lo strumento, l'altro descrivendo quello giusto al futuro.

> **Conseguenza operativa.** Innocuo oggi: nessun tipo di dominio genera id, perché non esiste ancora una slice. Diventa dannoso **esattamente al passo 7** di `ROADMAP.md` §4, dove `CreateCamper` produce il primo `Id` ed è il momento in cui un `Guid.NewGuid()` distratto entrerebbe senza opposizione, degradando la clustering key `(OwnerId, Id)` di ADR-0008 in inserimenti casuali. **Il lint va scritto prima di quella slice, non dopo**, e va visto fallire (R38).
>
> ⚠️ Generalizzazione dei sette casi: **la differenza fra i primi sei e questo è che gli altri erano verificatori che esistevano e non mordevano, questo è un verificatore che non esiste e che due documenti davano per esistente.** Il secondo tipo è più difficile da trovare, perché nessun esperimento lo rivela: solo la lettura incrociata di ciò che il repo contiene e di ciò che dichiara di contenere.

---

### 8.8 R5 è dichiarata applicata da `BannedApiAnalyzers` e non lo è — e condivide la causa con §8.7

**Ottavo caso, trovato da @hermes istruendo ADR-0005.** `ARCHITECTURE.md` §4 dichiara che sette famiglie di API — `Find`/`FindAsync`, `Attach`/`AttachRange`, `Update`/`UpdateRange`, `Entry().State = ...`, `ExecuteUpdate`/`ExecuteDelete`, `FromSqlRaw`/`FromSqlInterpolated`, `IgnoreQueryFilters` — sono **errore di compilazione** fuori da `Common/Ownership/`, e che *«il divieto è applicato da `BannedApiAnalyzers` e fa fallire la build, non la code review»* (**R5, ADR-0003**).

Verificato: l'infrastruttura è **correttamente agganciata** — `Directory.Build.props` righe 40 e 46-47 referenziano il pacchetto (5.6.0) e includono `BannedSymbols.txt` come `AdditionalFiles` — ma **il file non contiene nessuna delle sette**. Ha sei voci: quattro temporali e due sui provider EF. E **nessun test di convenzione le presidia**: una ricerca di `IgnoreQueryFilters`, `FindAsync`, `FromSqlRaw` ed `ExecuteUpdate` sotto `tests/` non restituisce nulla. **R5 non è in vigore.**

> **La causa comune con §8.7, che è il vero reperto.** R5 e R41 chiedono entrambe *«vietato **tranne** dentro un percorso»* — R5 tranne in `Common/Ownership/`, R41 tranne in `Roamly.Common.SequentialGuidGenerator`. **`BannedApiAnalyzers` non sa esprimere eccezioni per percorso**: bandisce un simbolo nell'intera compilazione, punto. Quindi non è una dimenticanza capitata due volte: è **lo stesso strumento sbagliato scelto due volte, per due regole che hanno la stessa forma**. ADR-0008 lo aveva già capito per R41, prescrivendo un regex lint; `ARCHITECTURE.md` no.
>
> **Conseguenza operativa.** Entrambe vogliono un **lint testuale con eccezione per percorso**, ed è opportuno scriverne **uno solo, parametrico**, anziché due. Il livello naturale è **L0**: `RepositoryRoot.CodeFiles()` esiste già e fa esattamente questa scansione per R30/R36. Va scritto **prima della prima slice** — R5 protegge il query filter di ownership, cioè la garanzia di sicurezza centrale del progetto, e oggi è scoperta nel momento esatto in cui stanno per nascere gli handler che potrebbero violarla.
>
> ⚠️ Nota per chi scriverà il lint: **`Entry()` non va bandito**. Vietato è `Entry().State = ...`; `Entry(e).Property(...).OriginalValue` è il pattern che **ADR-0005 R49 prescrive** per la concorrenza ottimistica. Un lint troppo largo qui renderebbe impossibile la regola di un altro ADR.

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

> **Stato al Blocco 5 (passo 18).** `.github/workflows/ci.yml` esiste e contiene **due** dei quattro job: `backend-fast` e `backend-integration`. `frontend` ed `e2e` arriveranno col frontend, che oggi non esiste. I comandi esatti sono in §13.1, i tempi misurati in §14.
>
> **Scostamento deliberato sul trigger:** il workflow reagisce a `push` su ogni branch e a `workflow_dispatch`, **non** a `pull_request`. Con entrambi i trigger attivi ogni commit di una PR verrebbe costruito due volte sullo stesso SHA, e il repository è privato (minuti contati). Ciò che si perde è la verifica del **merge commit**: va riconsiderato all'apertura della prima PR. Un `concurrency` annulla le esecuzioni superate su ogni ref **tranne `main`**, dove la storia deve conservare un esito per commit.

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

### Migration

Il tool `dotnet-ef` è pinnato a **10.0.12** in `.config/dotnet-tools.json`, allineato ai pacchetti EF Core. Su una macchina nuova serve `dotnet tool restore` **prima** di qualunque comando `ef`; un tool globale più vecchio del runtime è un disallineamento, non un dettaglio.

```powershell
dotnet tool restore

# Nuova migration. --context è obbligatorio: la solution ha DUE DbContext, e
# FullSchemaDbContext non ha migration e NON deve averne (§5).
dotnet ef migrations add <Nome> `
  --project src/Roamly.Infrastructure `
  --startup-project src/Roamly.Infrastructure `
  --context RoamlyDbContext `
  --output-dir Persistence/Migrations
```

> Il contesto è costruito da **`RoamlyDbContextFactory`** (`IDesignTimeDbContextFactory`, in `Roamly.Infrastructure`), che inietta `NoCurrentUser.Instance` e una connection string **mai aperta**. `--startup-project` punta a Infrastructure e non a `Roamly.Api` di proposito: legare la generazione delle migration al bootstrap dell'API è un accoppiamento che si paga ogni volta che l'avvio dell'API cambia.
>
> ⚠️ **Ispezionare sempre il file generato prima di accettarlo** (PK composita e clustered, nessuna `AlternateKey`, `principalColumns` nell'ordine della PK, nessun `SetNull`, precisione dei `decimal`). E **non correggerlo a mano**: una migration ritoccata diverge dal modello al `migrations add` successivo, che è esattamente la deriva che il test C intercetta.
>
> ⚠️ Lo scaffolding di EF Core 10 **non compila** con `TreatWarningsAsErrors=true` (emette `new[] { … }` costanti → CA1861, 12 errori). `Persistence/Migrations/.editorconfig` dichiara quella cartella `generated_code = true`. La disciplina zero-warning resta integralmente in vigore su tutto il resto.

> ⚠️ **Un progetto di test senza test fa fallire il comando — reperto del Blocco 2.** Microsoft.Testing.Platform tratta *"zero test eseguiti"* come **fallimento**, con **exit code 8**, non come successo vacuo. Oggi `dotnet test --solution Roamly.slnx` esce `8` pur con `non riuscito: 0`, perché `Roamly.Domain.Tests` e `Roamly.IntegrationTests` sono ancora vuoti. Si risolve da sé nei Blocchi 3-4, quando quei progetti ricevono i loro test.
> Va però ricordato in `ci.yml` (Blocco 5): **un filtro `--filter` che non seleziona nulla rende il job rosso**, e il messaggio (`non riuscito: 0`) non lo fa sembrare un errore. È l'opposto del fallimento silenzioso di §8.1 — qui è il *successo* a essere silenzioso — ma la lezione è la stessa: leggere l'exit code, non il riepilogo.
>
> ✅ **Conseguenza applicata al Blocco 5:** `ci.yml` **non usa alcun `--filter`**. La selezione dei test avviene per progetto, con `--project`, dove "quali test girano" è verificabile leggendo il nome del progetto invece che simulando un'espressione.

### 13.1 Comandi della pipeline — Blocco 5, passo 18

`.github/workflows/ci.yml` esegue **esattamente** questi comandi, in quest'ordine, su `ubuntu-latest` e in configurazione **`Release`** (la configurazione che andrà in deploy: verificare `Release` in CI e `Debug` in locale significherebbe verificare due cose diverse).

```bash
# entrambi i job
dotnet restore Roamly.slnx
dotnet build   Roamly.slnx --configuration Release --no-restore

# job backend-fast (L0, nessun Docker)
dotnet test --project tests/Roamly.Domain.Tests      --configuration Release --no-build
dotnet test --project tests/Roamly.Model.Tests       --configuration Release --no-build

# job backend-integration (L1 + L2, Testcontainers)
dotnet test --project tests/Roamly.IntegrationTests  --configuration Release --no-build
```

> **Perché `--project` e mai `--solution` negli step di test.** È il modo in cui **R39** (ADR-0009) resta in vigore da sola: quando `Roamly.Benchmarks` entrerà nella solution, un `dotnet test --solution` lo eseguirebbe in CI senza che nessuno debba dimenticarsene. Con progetti nominati, includerlo richiede una modifica deliberata di `ci.yml`.
>
> **Perché `dotnet build` e poi `dotnet test --no-build`, e non un `dotnet test` monolitico.** Per separare il segnale: un errore di compilazione e un test rosso diventano due step distinti nel riepilogo del job. ⚠️ **Non è un aggiramento del reperto qui sotto**: al Blocco 5 quel comportamento **non si riproduce** — `dotnet test --project … --configuration Release` senza `--no-build` esce **0** con 3/3 test eseguiti. La forma resta scelta per il suo merito, non come workaround.

> 🔴 **Validare il YAML prima del push, non dopo.** Il primo push di `ci.yml` è fallito in **0 secondi, senza alcun job e senza log**: il file era stato riscritto collassando tutte le righe in una sola, e l'intero workflow era diventato un unico commento. GitHub lo ha rifiutato come *workflow file issue*, e quell'errore **non è leggibile via API**: `gh run view --log-failed` risponde `log not found`, l'endpoint `jobs` restituisce `total_count: 0`, e le annotazioni del check run sono vuote. L'unico indizio è la durata di **0 s**.
>
> Da qui la regola: `ci.yml` si valida localmente prima di committarlo. Su questa macchina `python` non è disponibile, `node` sì:
>
> ```powershell
> cd $env:TEMP; npm install js-yaml --silent
> node -e "const y=require('js-yaml'),fs=require('fs');const d=y.load(fs.readFileSync('C:/dev/Roamly/.github/workflows/ci.yml','utf8'));console.log(Object.keys(d.jobs))"
> ```
>
> ⚠️ La causa prossima è una trappola di PowerShell che vale la pena conoscere: **`Set-Content -NoNewline` applicato a un array di stringhe le concatena senza separatore**, invece di limitarsi a omettere il newline finale. Per riscrivere un file riga per riga si usa `$righe -join [Environment]::NewLine`, oppure si riscrive il file per intero.

> ℹ️ **Annotazione della prima esecuzione, non bloccante.** `actions/checkout@v4`, `actions/setup-dotnet@v4` e `actions/cache@v4` puntano a Node.js 20, deprecato: i runner le forzano già su Node 24 e i job restano verdi. Il passaggio ai tag `@v5` va fatto insieme al pin per SHA, cioè **prima del primo workflow che tocca credenziali di deploy**.

---

## 14. Tempi misurati

> ⚠️ **Da compilare al Blocco 4 del primo giorno.** I valori di ADR-0009 sono **stime di ordine di grandezza** (F-12), non misure. Una strategia che dichiara tempi mai misurati invecchia male.

| Grandezza | Stima | Misurato il | Valore reale |
|---|---|---|---|
| Suite L0 a progetto singolo, build inclusa (1 test) | < 5 s | Blocco 1 | **3,9 s** di esecuzione, 18,8 s a freddo con build |
| `dotnet build` dell'intera solution, 7 progetti, incrementale | — | Blocco 2 | **11,1 s**, 0 avvisi, 0 errori |
| Costruzione offline di `FullSchemaDbContext.Model` (21 entity type) | — | Blocco 2 | inclusa nei **6,6 s** della suite L0 con sonda, mai connessa |
| **Suite L0 `Roamly.Model.Tests`, 13 test** (analizzatore 1785, R26-R29, R40, R32) | < 5 s | **Blocco 3** | **2,8-3,3 s**, esecuzione 2,55-2,97 s |
| **Suite L0 `Roamly.Domain.Tests`, 3 test** (COMB) | — | **Blocco 3** | **1,4 s** |
| **Tutto L0 insieme, 16 test** | < 5 s | **Blocco 3** | **3,5 s** — ✅ Checkpoint 3 |
| **Suite L0 `Roamly.Model.Tests`, 21 test** (con R33, R36, forma dei progetti) | < 5 s | **Blocco 4** | **2,0 s** di esecuzione |
| **Suite L1 `Roamly.IntegrationTests`, 9 test**, container incluso | — | **Blocco 4** | **17,6 s** (di cui ~10,5 s di avvio container) |
| **Tutta la soluzione, 33 test** (`dotnet test --solution --no-build`) | — | **Blocco 4** | **32,7 s** — ✅ Checkpoint 4 |
| Pull immagine su runner GHA (non cachata) | 40-70 s | **Blocco 5** | **≤ 26 s** — l'intero step `Test L1 e L2` (pull + avvio container + 14 test) è durato **26 s**, quindi il solo pull vale meno di così |
| Readiness del container | 20-45 s | **Blocco 4** | **9,6-14,6 s** (x64, Docker Desktop/WSL2, 2 GB, immagine locale) |
| `CREATE DATABASE` vuoto (container caldo) | 150-400 ms | **Blocco 4** | **554-855 ms** — ⚠️ 2-4× la stima |
| `EnsureCreated` schema completo | 1-3 s | **Blocco 4** | **2,5-3,4 s** |
| `Respawn.ResetAsync()` | 50-200 ms | **Blocco 4** | **843 ms** su schema completo vuoto — ⚠️ 4× la stima (include la costruzione del grafo alla prima chiamata) |
| Migration Phase 1 su DB vuoto | 1-2 s | **Blocco 5** | **374-467 ms** — ✅ meglio della stima (5 tabelle di dominio + Identity, `CREATE DATABASE` escluso) |
| `CREATE DATABASE` per il database del test C | — | **Blocco 5** | **360-373 ms** |
| **Suite L1 `Roamly.IntegrationTests`, 14 test** (con il test C), container incluso | — | **Blocco 5** | **16,6-19,4 s** |
| **Tutta la soluzione, 38 test** (`dotnet build` + `dotnet test --solution --no-build`) | — | **Blocco 5** | **21,2 s** di parete, esecuzione 34,5 s al primo giro a freddo — ✅ Checkpoint 5 (parte backend) |
| Job `backend-fast` | 90-120 s | **Blocco 5** | **41 s** — ✅ prima esecuzione reale, run `36410434236` |
| Job `backend-integration` | 3-4 min | **Blocco 5** | **68 s** — ✅ prima esecuzione reale, pull dell'immagine incluso |
| **Tempo di parete della CI** (i due job in parallelo) | 3-4 min (ADR) · 4-6 min (stima del passo 18) | **Blocco 5** | **68 s** — ✅ **Checkpoint 5 chiuso** |
| **Comandi di `ci.yml` eseguiti in locale, configurazione `Release`** — `restore` | — | **Blocco 5** | **1,5 s** (pacchetti già presenti) |
| Build `Release` dell'intera solution, **a freddo** (7 progetti) | — | **Blocco 5** | **405 s** su Windows con Defender attivo — ⚠️ vedi nota sotto |
| Build `Release` dell'intera solution, incrementale | — | **Blocco 5** | **3,9 s**, 0 avvisi, 0 errori |
| `--project tests/Roamly.Domain.Tests --no-build` (3 test) | — | **Blocco 5** | **12,1 s** a freddo · **2,0 s** a caldo |
| `--project tests/Roamly.Model.Tests --no-build` (21 test) | — | **Blocco 5** | **21,0 s** a freddo |
| `--project tests/Roamly.IntegrationTests --no-build` (14 test) | — | **Blocco 5** | **53,4 s**, di cui **17,4 s** di avvio container |
| `--solution Roamly.slnx --no-build` (38 test, `Release`) | — | **Blocco 5** | **38,6 s** — ✅ 38/38, exit 0 |

> ⚠️ **I 405 s della build `Release` a freddo non sono una stima per la CI.** Sono misurati su Windows con antivirus attivo e cache degli analyzer vuota; la stessa build incrementale costa **3,9 s**. Su un runner Linux GitHub-hosted la voce dominante di una build a freddo è il `restore` (mitigato dalla cache NuGet) e la prima esecuzione degli analyzer. Il numero è annotato perché è **l'unico dato in mio possesso** su una build a freddo, non perché sia trasferibile.

> ✅ **Tempo di parete della CI, misurato alla prima esecuzione reale** (run `36410434236`, commit `b3199c1`, `ubuntu-latest`, cache NuGet **fredda**).
> `backend-fast` **41 s**: checkout 1 s · setup SDK 1 s · cache 0 s · restore **11 s** · build **15 s** · 3 test Domain 1 s · 21 test Model **3 s**.
> `backend-integration` **68 s**: le stesse voci fino alla build (restore 14 s, build 17 s) · step `Test L1 e L2` **26 s**, che comprende **pull dell'immagine, avvio del container e 14 test**.
> I due job girano in parallelo, quindi il tempo di parete è **68 s** — contro i 3-4 min di ADR-0009 e i 4-6 min stimati al passo 18. **Entrambe le stime erano larghe di un fattore 3-5.**
>
> Le due sorprese, in direzioni opposte:
> 1. **Il pull dell'immagine non è il costo dominante che si temeva.** Era la voce che giustificava lo scarto fra le due stime, ed è la più piccola: l'intero step L1 costa 26 s contro i 53 s misurati in locale sullo stesso comando. La rete del runner GHA verso `mcr.microsoft.com` è molto più veloce del collegamento locale. **La cache dell'immagine resta quindi non necessaria** (ADR-0009, *Accorgimenti di velocità* punto 2): non c'è nulla da ottimizzare.
> 2. **`restore` + `build` valgono 26 s dei 41 s di `backend-fast`, cioè il 63%.** Il job "rapido" è dominato dalla compilazione, non dai test, che costano 4 s in tutto. Ne segue che la separazione fra i due job **non vale un ordine di grandezza ma 27 secondi**. Resta utile — si vede il rosso L0 senza attendere Docker — ma chi in futuro valutasse di fonderli deve sapere che il risparmio in gioco è quello, non un minuto.
>
> ⚠️ Questi numeri sono di una esecuzione con **cache NuGet vuota**: le successive avranno un `restore` più breve, quindi 41 s e 68 s sono il **caso peggiore**, non il caso medio.

Il tempo del job `backend-integration` va **riannotato a ogni revisione di questo documento**: è l'indicatore che fa scattare il passaggio da G1 a G2 (trigger T1).

> ⚠️ **Reperto del Blocco 4 — `dotnet test` senza `--no-build` non esegue nulla.** Su questa macchina `dotnet test --project …` e `dotnet test --solution Roamly.slnx` riportano *"Nessun test eseguito"* con **exit code 5** in ~250 ms, mentre gli stessi test passano lanciando l'eseguibile del progetto, con `--no-build`, con `-v n` o con argomenti MTP espliciti. Il comportamento è **deterministico** e **precede il Blocco 4** (riprodotto anche con `tests/Directory.Build.props` ripristinato alla versione del commit `6f79c5d`). Fino a diagnosi, la forma da usare — e da mettere in `ci.yml` al Blocco 5 — è **`dotnet build` seguito da `dotnet test --solution Roamly.slnx --no-build`**: un exit code 5 silenzioso in CI è indistinguibile da una suite verde per chi guarda solo il colore.

> ✅ **Aggiornamento del Blocco 5, passo 18: il reperto non si riproduce.** `dotnet test --project tests/Roamly.Domain.Tests --configuration Release` **senza** `--no-build` esce **0** eseguendo 3/3 test in 2,0 s. La forma `build` + `test --no-build` è stata **confermata in `ci.yml` per un'altra ragione** — separare l'errore di compilazione dal test rosso in due step distinti — e non come aggiramento di un bug. La causa dell'osservazione del Blocco 4 resta non diagnosticata: se si ripresentasse, va cercata lì, non nel workflow.
