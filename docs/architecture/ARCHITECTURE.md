# Roamly — Architettura

Stato: attivo · Ultimo aggiornamento: 2026-09-24
Origine: `docs/archive/Roamly_Planning_2026-09-24.md` §3, §4, §5, §21, §23, §28 — **corretto** secondo review C1

---

## 1. Stack tecnologico

### Backend

- **C#**
- **ASP.NET Core 10** (LTS)
- Entity Framework Core
- REST API + OpenAPI / Swagger
- Dependency Injection nativa
- CQRS (separazione logica write/read)
- Vertical Slice Architecture
- FluentValidation o equivalente
- Authentication / Authorization owner-scoped
- Structured logging
- Health checks
- Background services quando necessari

> ⚠️ **Override rispetto ai default dell'orchestratore (review I2).**
> `C:\dev\ai-agents\orchestrator.md` assume **.NET 8**, **Azure DevOps** e **MediatR + FluentValidation**.
> Roamly usa **ASP.NET Core 10** e **GitHub Actions**. Questo override è vincolante: gli agenti non devono applicare i default.
> Resta da verificare che il target di hosting scelto (decisione #9) supporti il runtime .NET 10.

> ⚠️ **Decisione aperta #5 — MediatR (review I3), severità MEDIUM. Owner: @archimedes.**
> Il planning diceva "MediatR o equivalente, se utile": non è una decisione.
> **Tecnico:** con handler invocati direttamente dagli endpoint, MediatR è spesso indirezione pura; il suo valore sta nelle pipeline behavior, ottenibili anche con un decorator o con i filtri degli endpoint.
> **Licenza:** MediatR è dual-license (Lucky Penny Software). Dalla v13 serve una licenza; la Community edition è gratuita sotto i 5M$ di fatturato ma richiede **registrazione chiave e rinnovo annuale**. Costo zero per Roamly, ma vincolo amministrativo ricorrente.
> Alternative sul tavolo: MediatR community, dispatcher minimale in casa (~50 righe), handler diretti senza mediator, libreria alternativa MIT.

### Database

Scelta esplicita: **Microsoft SQL Server**. Vedi `docs/architecture/DATA.md`.

### Frontend

- **React** + **Vite** + **TypeScript**
- React Router
- TanStack Query
- **Tailwind CSS v4** — decisione #7 chiusa, vedi [`adr/0007-design-system.md`](../adr/0007-design-system.md)
- **shadcn/ui** usato come *generatore*: il codice viene copiato nel repository e riscritto con i token di Roamly, non è una dipendenza runtime
- **Lucide** per le icone
- **i18n** dalla prima stringa: R21 richiede i namespace `neutral.*` e `narrative.*` separati
- gestione form tipizzata
- client API generato o fortemente tipizzato

> **Vincoli di frontend derivati da ADR-0007.**
> - 🔴 **Nessuna CDN di terzi** (R19): i font sono **self-hosted**. È un vincolo di **privacy**, non di performance, e va verificato in CI.
> - **Nessuna libreria di motion in Phase 1.** Le animazioni previste sono ottenibili con transizioni CSS; una libreria si valuta solo quando serve il disegno progressivo del tracciato.
> - Il frontend è servito come **bundle statico** e deve restare **same-site** rispetto all'API (vincolo di ADR-0003).
> - **Next.js è stato valutato e respinto**: contraddice questo stack e il vincolo same-site. In un'app interamente autenticata il SEO è irrilevante e il suo vantaggio principale decade.
> - `components/ui/` contiene **solo** wrapper di primitive (R13). Nessun componente `Card` generico (R16): esistono `Ribbon`, `MetricBlock`, `CamperHeader`.
> - **Nessun colore letterale nel codice** (R12): solo token semantici. Verificato da lint bloccante.

### DevOps

Git, GitHub, **GitHub Actions**, Docker, environment separation. Vedi `docs/architecture/DEVOPS.md`.

---

## 2. Architettura di sistema

```text
                           ┌──────────────────────┐
                           │       Browser        │
                           │ React + Vite + TS    │
                           └──────────┬───────────┘
                                      │ HTTPS
                                      ▼
                           ┌──────────────────────┐
                           │    ASP.NET Core 10   │
                           │       Web API        │
                           └──────────┬───────────┘
                                      │
                  ┌───────────────────┼───────────────────┐
                  │                   │                   │
                  ▼                   ▼                   ▼
           ┌────────────┐      ┌────────────┐      ┌─────────────┐
           │ SQL Server │      │ External   │      │ AI /        │
           │ + Spatial  │      │ Services   │      │ Intelligence│
           └────────────┘      └────────────┘      └─────────────┘
```

---

## 3. Struttura repository

Il backend segue **Vertical Slice Architecture**, non una separazione globale per layer.

```text
roamly/
├── src/
│   ├── Roamly.Api/
│   │   ├── Features/
│   │   │   ├── Authentication/
│   │   │   ├── Campers/
│   │   │   ├── Maintenance/
│   │   │   ├── Documents/
│   │   │   ├── Trips/
│   │   │   ├── Places/
│   │   │   ├── Expenses/
│   │   │   ├── Checklists/
│   │   │   ├── Journal/
│   │   │   └── Intelligence/
│   │   ├── Common/
│   │   │   ├── Ownership/          # ADR-0003: unico luogo autorizzato ad aggirare il filtro
│   │   │   └── Privacy/            # ADR-0004: export model-driven, erasure job
│   │   └── Program.cs
│   ├── Roamly.Infrastructure/
│   └── Roamly.Domain/
├── tests/
│   ├── Roamly.Domain.Tests/          # L0 — dominio puro, nessuna dipendenza
│   ├── Roamly.Model.Tests/           # L0 — verificatori su DbContext.Model, SENZA database
│   ├── Roamly.IntegrationTests/      # L1 — Testcontainers MsSql + Respawn
│   └── Roamly.Architecture.Tests/    # L0 — convenzioni di dipendenza (dopo la prima slice)
├── benchmarks/
│   └── Roamly.Benchmarks/            # solo locale, mai in CI
├── frontend/
│   └── roamly-web/
├── database/
├── docs/
│   ├── architecture/
│   ├── product/
│   ├── adr/
│   └── archive/
├── .github/
│   └── workflows/
├── docker/
└── README.md
```

Il principio importante: ogni feature verticale contiene **vicini tra loro** request, handler, validation, mapping e response model. Organizzare per **comportamento/feature**, non per tipo tecnico.

> **Quattro progetti di test, non tre** ([`ADR-0009`](../adr/0009-test-strategy.md)). La separazione che conta non è per livello architetturale ma per **costo di esecuzione**: `Roamly.Model.Tests` esiste perché **17 dei 25 verificatori non toccano alcun database** — EF Core costruisce `DbContext.Model` offline con `UseSqlServer` senza mai connettersi. Tenerli in un progetto senza Docker li rende eseguibili in meno di 5 secondi a ogni salvataggio, invece che in 60-100 secondi in CI.

> **`BannedSymbols.txt`** è parte dell'impianto, non un accessorio: vieta `DateTime.UtcNow` e `DateTimeOffset.UtcNow` (si usa `TimeProvider`, **R34**), `UseSqlite` e `UseInMemoryDatabase` (un solo motore di persistenza nei test, **R30**), e `Guid.NewGuid()` nel dominio (**R41**, ADR-0008). Sono tutte regole che nessun test funzionale intercetta: violarle non rompe nulla, degrada soltanto.

---

## 4. Vertical Slice + CQRS

Non vogliamo:

```text
Controllers/  Services/  Repositories/  DTOs/  Validators/  Handlers/
```

Vogliamo:

```text
Features/
└── Campers/
    ├── CreateCamper/
    │   ├── Endpoint.cs
    │   ├── Command.cs
    │   ├── Handler.cs
    │   ├── Validator.cs
    │   └── Response.cs
    ├── GetCamper/
    │   ├── Endpoint.cs
    │   ├── Query.cs
    │   ├── Handler.cs
    │   └── Response.cs
    └── UpdateCamper/
        └── ...
```

### Commands e Queries

**Commands** (modificano stato): `CreateCamper`, `UpdateCamper`, `AddMaintenance`, `CompleteMaintenance`, `CreateTrip`, `AddTripStop`, `AddExpense`, `CompleteChecklist`.

**Queries** (sola lettura): `GetCamperDashboard`, `GetCamper`, `GetMaintenanceSchedule`, `GetTrip`, `GetTripSummary`, `GetExpenses`, `GetJournal`.

**CQRS in Roamly è una separazione logica tra write model e read model su un singolo SQL Server.** Niente event sourcing, niente secondo database.

### Principi di slice

Ogni feature deve:

- essere il più possibile autonoma;
- possedere il proprio handler;
- validare il proprio input;
- definire il proprio response contract;
- evitare dipendenze inutili da servizi globali;
- contenere query ottimizzate per il caso d'uso;
- **non filtrare a mano per utente corrente.** Il filtro è automatico e applicato dal `DbContext`; **aggirarlo è vietato**. Una slice che scrive `.Where(x => x.OwnerId == ...)` sta duplicando una garanzia che già esiste, e segnala che qualcuno non si fida del meccanismo. Vedi `SECURITY.md` §2 e [`ADR-0003`](../adr/0003-auth-and-ownership.md).

#### API bandite (errore di compilazione fuori da `Common/Ownership/`)

`Find` / `FindAsync` · `Attach` / `AttachRange` · `Update` / `UpdateRange` · `Entry().State = ...` · `ExecuteUpdate` / `ExecuteDelete` · `FromSqlRaw` / `FromSqlInterpolated` · `IgnoreQueryFilters`.

Sono tutte vie che **non passano dal query filter**. Il divieto è applicato da `BannedApiAnalyzers` e fa fallire la build, non la code review.

`IgnoreQueryFilters` è bandito in **entrambe** le forme. Esistono solo `IgnoreQueryFilters()` — che disattiva *tutti* i filtri — e `IgnoreQueryFilters(IEnumerable<string>)`. La forma `IgnoreQueryFilters("OwnerScope")` con stringa singola, citata in ADR-0003, **non compila**: la forma corretta è `IgnoreQueryFilters(["OwnerScope"])`.

> **Non è bandito** `context.Set(Type)`: è l'API su cui si reggono l'export model-driven e il job di erasure ([`ADR-0004`](../adr/0004-privacy-and-erasure.md)), e non aggira il filtro. Non richiede deroghe.

### Regola sulla duplicazione

La duplicazione controllata è preferibile a un'astrazione prematura, **con una soglia esplicita**:

> Alla **terza** ripetizione dello stesso comportamento, si promuove in `Common/`.

Senza questa soglia, "duplicazione controllata" degenera in duplicazione e basta (review §5).

### Database access

EF Core è usato direttamente dalle feature dove appropriato. Evitare `GenericRepository<T>` / `GenericService<T>` quando non aggiungono valore.

Query read-heavy: projection LINQ, `AsNoTracking`, SQL mirato quando serve, query specifiche per feature.
Command: modifica esplicita e transazionale del modello.

---

## 5. Modular Monolith

Roamly parte come **modular monolith**, non come microservices.

```text
Roamly
├── Identity
├── Campers
├── Maintenance
├── Documents
├── Trips
├── Places
├── Expenses
├── Checklists
├── Journal
└── Intelligence
```

Ogni modulo ha confini chiari. Questo permette di sviluppare velocemente, mantenere semplice il deployment, evitare la complessità dei sistemi distribuiti ed estrarre un servizio successivamente, se davvero necessario.

> **Ordine di implementazione vincolato da [`ADR-0003`](../adr/0003-auth-and-ownership.md):** `Identity` è il **primo modulo** da implementare. Non è una scelta di priorità di prodotto: senza `ICurrentUser` e senza il perimetro di ownership, nessun'altra slice può essere scritta in modo corretto — andrebbe riscritta dopo.
>
> [`ADR-0004`](../adr/0004-privacy-and-erasure.md) colloca **export ed erasure dentro `Identity`**: sono operazioni sull'account, non feature trasversali. Il loro codice vive in `Common/Privacy/`, gli endpoint in `Features/Identity/`.

---

## 6. Observability

Da prevedere fin dall'inizio, senza overengineering:

- structured logging;
- correlation ID;
- health checks;
- error handling centralizzato (`ProblemDetails` — vedi `API-CONVENTIONS.md`);
- **OpenTelemetry** per traces e correlazione, attivato subito: in .NET è la via standard e costa poco. Il backend di raccolta può essere deciso dopo;
- metriche applicative legate agli eventi di dominio di `VISION.md` §4;
- **eventi di sicurezza dedicati**, distinti dagli errori applicativi: `OwnershipViolationException` (un bug che ha tentato una scrittura fuori scope) e `ownership_miss` (una lettura a vuoto su un `{id}` esistente di un altro utente, con `requestedId` e `actualOwnerId`). Poiché l'API risponde `404` indistinguibile, **i log sono l'unico luogo in cui la distinzione esiste**.
  Vivono sulla **pipeline di logging strutturato**, con retention configurata **nel sink**: nessuna tabella `SecurityEvent` su database, nessun job di purge ([`ADR-0004`](../adr/0004-privacy-and-erasure.md)). Contengono `actualOwnerId`, cioè l'identificativo di un terzo: **mai esposto in export né in API**;
- **telemetria**: distinguere ciò che è **identificabile** (entità owned → esportata e cancellata con l'account) da ciò che è un **contatore aggregato** senza identificatori (sopravvive alla cancellazione). Le metriche per-utente spariscono con l'utente;
- tracing distribuito solo quando il sistema lo richiederà.

---

## 7. Docker

Ambiente locale:

```text
docker compose
│
├── roamly-api
├── roamly-web
└── mssql          ← SQL Server, non postgres
```

Evoluzione possibile (**non** prematura):

```text
SQL Server
Object Storage      ← decisione #8
API
Frontend
Background Worker
```

Redis e background worker **non** vanno introdotti prematuramente.
