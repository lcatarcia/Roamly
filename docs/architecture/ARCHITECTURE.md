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

> ✅ **Decisione #5 — nessun mediator. Chiusa 2026-09-28, severità MEDIUM. Owner: @archimedes.**
> Le slice espongono **handler diretti**, classi semplici risolte da DI e invocate dall'endpoint. I concern trasversali vivono negli **endpoint filter** e nel middleware, non in una pipeline di behavior.
>
> **Perché non un mediator.** Mappando una per una le otto voci aperte di [`API-CONVENTIONS.md`](API-CONVENTIONS.md) §2, **cinque sono concern HTTP** (error model, validation errors, ETag/`If-Match`, idempotency, versioning): vivono in middleware o endpoint filter, dove un mediator non arriva senza portarsi un header dentro il command. Le altre due — paginazione e status code di dominio — vivono **nell'handler**, perché sono forma del contratto e non comportamento trasversale. **I pipeline behavior di dominio richiesti da [`ADR-0005`](../adr/0005-api-conventions.md) sono zero**: i due candidati esistenti (stamping di `UpdatedAtUtc`, enforcement dell'ownership in scrittura) sono già un `SaveChangesInterceptor` per ADR-0003.
>
> *Nota: la prima stesura di questo paragrafo contava «sei concern HTTP». Il numero difendibile, verificato voce per voce da @hermes istruendo ADR-0005, è **cinque**. La conclusione non cambia.*
>
> **Perché non MediatR** (v14.2.0, RPL-1.5 o commerciale). Il motivo dominante **non è la licenza**: è che un comando **senza handler registrato compila verde** e fallisce a runtime. In un progetto che ha già trovato sette verificatori inerti, "verde ma non in vigore" è il rischio da cui ci si difende. Secondariamente, senza chiave MediatR emette a runtime `warn: LuckyPennySoftware.MediatR.License[0]`, che finisce **nello stesso sink degli eventi di sicurezza** di §6 e che `TreatWarningsAsErrors` non può intercettare.
>
> **Alternativa più vicina:** `martinothamar/Mediator` (MIT, source-generated), che trasforma quel fallimento in `error MSG0005` a compile time. Scartata perché il costo di indirezione resta e il verificatore si può scrivere. **Il costo di inversione verso di essa è 🟢 BASSO** (~10 min per slice: cambia una dichiarazione di interfaccia, non la forma del codice), ed è la ragione per cui questa decisione è stata presa subito anziché rimandata.
> `Immediate.Handlers` è stata **scartata per testabilità**: `error IHR0011` impone `HandleAsync` privato, costringendo ogni test L0 a ricostruire a mano la catena di behavior — una regressione contro [`../adr/0009-test-strategy.md`](../adr/0009-test-strategy.md).
>
> **Condizione vincolante:** la decisione regge solo con **R44**, il verificatore di registrazione (§4). Senza, A è la peggiore delle opzioni, non la migliore.
>
> FluentValidation resta **non condizionata** da questa scelta (dominio @hermes).
> Istruttoria completa con quattro prototipi compilati ed eseguiti: file di sessione `decisione5-archimedes.md`.

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

> **`BannedSymbols.txt`** è parte dell'impianto, non un accessorio: vieta `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow` e `DateTimeOffset.Now` (si usa `TimeProvider`, **R34**), più `UseSqlite` e `UseInMemoryDatabase` (un solo motore di persistenza nei test, **R30**). Sono tutte regole che nessun test funzionale intercetta: violarle non rompe nulla, degrada soltanto.
>
> ⚠️ **Questo paragrafo dichiarava anche `Guid.NewGuid()` nel dominio (R41): era falso.** Il file ha **sei voci e nessuna su `Guid`**, e `BannedApiAnalyzers` non sarebbe comunque lo strumento giusto, perché non conosce le eccezioni per percorso e R41 deve restare lecita **dentro** il generatore COMB.
>
> ✅ **Sanato il 2026-09-28.** R41 è ora in vigore come **lint testuale L0**: `tests/Roamly.Model.Tests/Conventions/R41_IdGenerationTests.cs`, istanza del lint parametrico `PathScopedBan`. Vieta `Guid.NewGuid()`, `Guid.CreateVersion7()` e `new Guid(...)` sotto `src/**/Domain/**`, più `Guid.CreateVersion7()` in tutto `src/`, con **unica esenzione `src/Roamly.Common/SequentialGuidGenerator.cs`**. Visto fallire su una violazione deliberata (R38); dettagli e testo del rosso in [`TESTING.md`](TESTING.md) §8.7.

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

### Forma degli handler — decisione #5

**Nessun mediator.** `Handler.cs` è una **classe semplice**, non statica, registrata in DI e invocata **direttamente** dall'endpoint:

```csharp
public sealed class CreateCamperHandler(RoamlyDbContext db, IIdGenerator ids, TimeProvider clock)
{
    public async Task<CreateCamperResponse> HandleAsync(CreateCamperCommand cmd, CancellationToken ct) { /* ... */ }
}
```

I concern trasversali vivono negli **endpoint filter** e nel middleware, dove hanno accesso alla richiesta HTTP. Non esiste una pipeline di behavior: se un giorno servissero più di due comportamenti realmente trasversali *al dominio* (non all'HTTP), la decisione #5 va riaperta — il costo di inversione verso `martinothamar/Mediator` è basso per costruzione.

Gli handler sono classi **non statiche** deliberatamente: una classe statica non è sostituibile né intercettabile, e chiuderebbe quella via d'uscita.

| Regola | Enunciato | Verificatore |
|---|---|---|
| **R44** | **Ogni handler è registrato.** Ogni tipo il cui nome termina in `Handler` sotto `Features/` è risolvibile dal container. Il fallimento che un mediator con source generator intercetterebbe a compile time, qui è intercettato dal test | test di convenzione L0 (**bloccante**) in `Roamly.Model.Tests/Conventions/`: confronta i tipi `*Handler` scoperti per riflessione con quelli registrati nella `IServiceCollection` dell'API. Un handler nuovo e non registrato **rompe la build** |

> **R44 non è un contorno: è la condizione della decisione #5.** Senza, gli handler diretti sono la peggiore delle opzioni valutate, perché pagano lo stesso fallimento a runtime di MediatR senza averne l'ecosistema.

> 🔴 **R44 non è oggi in vigore, e non va scritta prima del primo handler.** Non esiste alcun `*Handler` (`Program.cs` è ancora il template `dotnet new web`, senza composition root) e `Roamly.Model.Tests` non referenzia `Roamly.Api`: un verificatore scritto oggi confronterebbe `∅ ⊆ ∅` e sarebbe **verde a vuoto**. Va scritta al **passo 6** di [`ROADMAP.md`](../product/ROADMAP.md) §4, nello stesso commit che crea il composition root. Analisi completa, alternative valutate e la decisione di struttura aperta (il `ProjectReference` verso `Roamly.Api`) in [`TESTING.md`](TESTING.md) §8.9.

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

Sono tutte vie che **non passano dal query filter**. Il divieto **non è applicato da `BannedApiAnalyzers`** — quell'analyzer bandisce un simbolo nell'intera compilazione e **non sa esprimere eccezioni per percorso**, mentre queste API devono restare lecite dentro `Common/Ownership/`. Il meccanismo reale è un **lint testuale L0 bloccante**: `tests/Roamly.Model.Tests/Conventions/R5_OwnershipBypassApiTests.cs`, istanza del lint parametrico `PathScopedBan` condiviso con R41. Fa fallire la suite L0, non la code review. Il reperto che ha corretto questa affermazione — e il resoconto R38 del rosso osservato — è in [`TESTING.md`](TESTING.md) §8.8.

> ⚠️ **`Entry()` non è bandito**: vietata è solo l'assegnazione `Entry(...).State = ...`. `Entry(e).Property(...).OriginalValue` è il pattern che **ADR-0005 R49** prescrive per la concorrenza ottimistica, ed è verificato come caso negativo del lint.
>
> ⚠️ **`Update`/`Find` sono riconosciuti sulla chiamata a un `DbSet`/`DbContext`, non come sottostringa** — `UpdateCamper` e `UpdatedAtUtc` non scattano. Il falso negativo accettato (`var set = db.Campers; set.Update(x);`) è dichiarato in `TESTING.md` §8.8.
>
> ⚠️ **L'esenzione `Common/Ownership/` è dichiarata ma non ancora esercitata**: la cartella non esiste finché non c'è la prima slice.

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
