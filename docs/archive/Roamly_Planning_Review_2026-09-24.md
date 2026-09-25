# Roamly — Revisione del Planning

> Review eseguita con il quality gate **@janus**, con contributi di dominio da **@archimedes**, **@oracle**, **@hermes**, **@sentinel**, **@vulcan**, **@pixel**.
> Documento sorgente: `Roamly_Planning.md`
> Data: 2026-09-24

---

## CHANGES REQUIRED

Il piano è **sopra la media** come documento di visione: il posizionamento ("personal travel OS", non "app per aree di sosta") è chiaro, i principi di prodotto sono coerenti e la sezione 25 ("cosa NON fare") è una rarità sana in un planning iniziale.

Non è però ancora un documento su cui si può iniziare a implementare, per tre motivi:

1. contiene **contraddizioni tecniche interne bloccanti** (database);
2. omette **decisioni ad alto impatto** che condizionano l'architettura (dati geografici, auth, storage file, privacy, offline);
3. descrive **cosa** costruire ma quasi mai **quando è finito** (nessun criterio di uscita, nessuna stima, nessun contratto di errore/API).

**Production Readiness Score (come piano esecutivo): 58/100**
Come documento di visione sarebbe 82/100.

---

## 1. Punti di forza (da non perdere nel refactor del documento)

- **Tesi di prodotto forte e difendibile.** "Come arrivo da A a B *con questo camper*" è un vero differenziatore, non una feature list.
- **Camper-first come principio di dominio**, non come sezione del profilo utente: è la scelta che rende il resto coerente.
- **Vertical Slice + CQRS logico su singolo DB**, con rifiuto esplicito di event sourcing e doppio database: scelta pragmatica e corretta per uno-sviluppatore.
- **Rifiuto esplicito di `GenericRepository<T>`** e preferenza per duplicazione controllata vs astrazione prematura.
- **Sezione 25 (anti-scope)** e **sezione 26 (metriche di activation/engagement, non vanity metrics)**.
- **ADR previsti fin dall'inizio.**

Questi punti vanno mantenuti testualmente: sono il contratto architetturale del progetto.

---

## 2. Critical Findings

Bloccanti: vanno risolti prima di scrivere codice.

### C1 — Il documento dichiara due database diversi

Il piano afferma in modo esplicito (§3 e §18) *"Il database applicativo sarà SQL Server. Non verrà utilizzato PostgreSQL"*, ma PostgreSQL/PostGIS sopravvive in almeno cinque punti:

| Sezione | Contenuto incoerente |
|---|---|
| §4 — diagramma architettura | box `PostgreSQL + PostGIS` |
| §21 — Docker | `docker compose → postgres`, e "PostgreSQL, Redis, Object Storage" nell'evoluzione |
| §24 — Roadmap Phase 0 | "PostgreSQL" tra le foundation |
| §27 — Tabella decisioni | `Database: PostgreSQL`, `Geo: PostGIS` |
| §29 — ADR | `0001-use-postgresql.md` |

Questo non è un refuso cosmetico: la tabella §27 è la sezione che un lettore (o un agente) usa come fonte di verità delle decisioni, ed è quella *sbagliata*. Rischio concreto di generare codice, compose e migration su Npgsql.

**Azione:** allineare tutto a SQL Server, e decidere esplicitamente il pacchetto spaziale (`NetTopologySuite` + `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite`). Rinominare l'ADR in `0001-use-sql-server.md` e scriverlo *davvero*, includendo il motivo del rifiuto di PostGIS (che su geo è oggettivamente superiore: la decisione va motivata, non nascosta).

> Nota onesta, da mettere nell'ADR: SQL Server Spatial è più debole di PostGIS su routing/geografia avanzata e l'immagine Docker è più pesante. La scelta resta difendibile per l'integrazione .NET e per le competenze esistenti, ma va scritto che è un tradeoff consapevole, non un pareggio.

### C2 — Manca la strategia dati geografici, che è il vero rischio di progetto

Il Vehicle-Aware Routing (§8) è definito come *la* feature strategica, ma il piano non nomina nessuna fonte dati: né routing engine, né dati di restrizione (altezza ponti, limiti di peso, ZTL), né aree di sosta.

Questo è il punto in cui il progetto può fallire, e nel piano occupa mezza pagina senza una sola decisione.

Domande aperte non affrontate: chi fornisce il routing per veicoli alti/pesanti (HERE, TomTom, PTV, GraphHopper self-hosted su OSM)? Qual è il costo per chiamata e il limite del tier gratuito? La licenza permette di memorizzare i risultati? Da dove arrivano le aree di sosta (Park4Night e simili non sono licenziabili)? Come si distingue un dato verificato da una stima?

**Azione:** ADR `geo-data-strategy` + **spike tecnico time-boxed (max 3 giorni)** prima della Phase 3, non durante. Nel frattempo il piano deve dichiarare che il routing della v1 è *non* vehicle-aware ed è solo una stima distanza/costi.

### C3 — Manca il modello di autorizzazione multi-utente

Il dominio è `User → Camper → Trips`: ogni singola query è per definizione **owner-scoped**. Il piano parla di "authentication / authorization" e di "least privilege" come voci di elenco, ma non definisce **la regola invariante**: nessun handler può leggere o scrivere un'entità senza filtrare per l'utente corrente.

In Vertical Slice questa regola è particolarmente a rischio, perché ogni slice scrive la propria query: è esattamente lo scenario che produce IDOR (`GET /api/v1/campers/{id}` che restituisce il camper di un altro).

**Azione:** definire il meccanismo (query filter EF globale su `OwnerId` + `ICurrentUser` iniettato + test di sicurezza obbligatorio per ogni slice che espone un `{id}`). Va nel piano come regola architetturale, non come "buona pratica". Owner: **@sentinel** + **@archimedes**.

### C4 — Privacy e GDPR assenti

Roamly tratta: posizione geografica storica, foto, documenti del veicolo, spese, abitudini di viaggio, e (§13) costruisce un profilo di preferenze inferito. È, senza giri di parole, un profilo comportamentale geolocalizzato.

Il piano non contiene: base giuridica, data retention, export dati, cancellazione account, informativa, e soprattutto cosa succede ai dati personali quando (§12) vengono inviati a un modello AI di terze parti.

**Azione:** sezione dedicata + requisiti minimi già nell'MVP (export + delete account sono economici *se* progettati subito, costosissimi dopo). La §13 dice già "trasparente, modificabile e sotto il controllo dell'utente": va trasformato in requisiti verificabili.

---

## 3. Important Findings

Non bloccano l'inizio, ma vanno chiusi prima della Phase 1.

### I1 — Numerazione e struttura del documento

Esistono **due sezioni "# 6"** (*Core Domain* e *MVP 1.0*); da lì la numerazione è sfalsata rispetto ai riferimenti. Inoltre §2 usa sotto-livelli (`2.1`) mentre il resto del documento no.

Il documento è lungo ~1.400 righe e mescola tre generi: visione di prodotto, specifica tecnica e roadmap. **Va splittato**, altrimenti invecchia male:

```text
docs/product/VISION.md          → §1, §2, §11, §12, §13, §26, §31
docs/architecture/ARCHITECTURE.md → §3, §4, §5, §17, §18, §23, §28
docs/architecture/CONTEXT.md    → §6 Core Domain (ubiquitous language)
docs/product/ROADMAP.md         → §6bis MVP, §24, §30
docs/product/UX.md              → §14, §15, §16
docs/adr/                       → le decisioni, una per file
```

### I2 — Conflitto con i default dell'orchestratore

`C:\dev\ai-agents\orchestrator.md` assume **.NET 8**, **Azure DevOps**, **xUnit**, **MediatR + FluentValidation**. Roamly usa **ASP.NET Core 10** e **GitHub Actions**. L'override è già scritto in `AGENTS.md`, ma va reso esplicito anche nel planning, altrimenti gli agenti applicheranno i default.

.NET 10 / ASP.NET Core 10 è LTS, quindi la scelta è solida; resta da verificare solo che il target di hosting scelto (vedi I10) supporti quel runtime.

### I3 — MediatR non è una decisione presa, e ora ha implicazioni di licenza

§3 dice "MediatR **o equivalente** ... se utile". Due problemi distinti.

**Tecnico:** con Vertical Slice e handler invocati direttamente dagli endpoint, MediatR è spesso puro overhead — un livello di indirezione che rende più difficile navigare dal `MapPost` al codice che esegue. Il suo valore reale sta nelle pipeline behavior (validation, logging, transaction), che però si possono ottenere anche con un semplice decorator o con i filtri degli endpoint.

**Di licenza:** MediatR è passato a un modello dual-license (Lucky Penny Software). Dalla v13 in poi serve una licenza; la Community edition è gratuita sotto i 5M$ di fatturato ma richiede **registrazione di una chiave e rinnovo annuale**. Le versioni precedenti restano open source ma senza aggiornamenti. Per Roamly il costo sarebbe zero, ma introduce un vincolo amministrativo ricorrente e un rischio di lock-in che va accettato consapevolmente — non ereditato dai default dell'orchestratore.

Questa è una decisione MEDIUM: va presa con il Decision Gate, non lasciata a "se utile". Alternative da mettere sul tavolo: MediatR con licenza community, un dispatcher minimale scritto in casa (~50 righe), handler invocati direttamente senza mediator, o una libreria alternativa MIT.

### I4 — Il contratto API è sotto-specificato

§17 elenca gli endpoint, ma manca tutto ciò che rende un'API mantenibile:

- **error model**: `ProblemDetails` (RFC 9457) come formato unico di errore;
- **validation errors**: formato `errors[]` per campo, da FluentValidation;
- **paginazione** (`GET /trips`, `GET /expenses` cresceranno): cursor vs offset;
- **filtering/sorting**: convenzione unica;
- **concorrenza**: ETag / `If-Match` su update, coerente con il `rowversion` già previsto in §18;
- **idempotency key** su POST che generano costi/spese;
- **versioning**: `/api/v1` è in URL, ma manca la policy di breaking change e di deprecazione;
- **status code** per i casi di dominio (es. camper non compatibile con il percorso: 200 con esito negativo, non 400).

Owner: **@hermes**.

### I5 — Il domain model è un elenco di entità, non un modello

§6 elenca entità e attributi, ma non contiene: invarianti, value object, cardinalità, cicli di vita, stati.

Esempi di lacune concrete:
- `Trip` ha stati impliciti (pianificato / in corso / completato / annullato) mai dichiarati: metà delle feature (dashboard, journal, metriche) dipendono da questo stato.
- `Place` è ambiguo: è un POI globale condiviso o un luogo salvato dall'utente? Da questa risposta dipendono tabelle, ownership, dedup e (Phase 6) il modello community. È probabilmente il **secondo** punto più importante del modello dati dopo l'ownership.
- `Expense` è agganciata "al camper o a uno specifico viaggio": polimorfismo di ownership da modellare esplicitamente (due FK nullable + constraint, o tabella per contesto).
- Denaro: serve un value object `Money` (importo + valuta), non un `decimal` nudo — §18 dice "decimal per denaro" ma non nomina la valuta. Multi-valuta è reale per chi viaggia in camper (CHF, HRK/EUR, GBP).
- Unità di misura: km/litri assunti implicitamente. Va dichiarato che sono l'unità canonica di persistenza.
- `MaintenanceItem` ricorrente per **km** e per **tempo** contemporaneamente (es. "ogni 15.000 km o 12 mesi"): è la regola che genera i reminder della dashboard, e non è modellata.

Owner: **@archimedes**, con la skill `domain-modeling` e output in `docs/architecture/CONTEXT.md`.

### I6 — Storage di file assente dallo stack

`Document` (§6) e `Photos` (§11) implicano upload, storage, thumbnail, quota, scansione, URL firmati. Lo stack (§3) non nomina nessuno storage, e §22 cita "protezione upload" come voce di elenco.

Le foto di viaggio sono anche il cuore dell'esperienza premium descritta in §14: non è un dettaglio infrastrutturale.

**Azione:** decidere lo storage (filesystem locale in dev, Azure Blob / S3-compatible in prod) e la strategia di serving. Decisione MEDIUM.

### I7 — L'MVP è troppo largo

§6bis "MVP 1.0" include dashboard, anagrafica camper, equipaggiamento, manutenzione, documenti; §7 aggiunge il Trip Planner completo; §24 Phase 0 aggiunge auth, Docker, CI e *design system foundation*.

Ma §30 propone come prima slice: `Login → Create Camper → Camper Dashboard → Add Maintenance → Maintenance reminder`. Questa è la cosa giusta, ed è in contraddizione con l'ampiezza dichiarata sopra.

**Azione:** ridefinire MVP 1.0 = **§30**. Il Trip Planner è la 1.1. Aggiungere a ogni fase un **criterio di uscita verificabile** (es. "un utente reale registra il proprio camper e riceve il primo promemoria manutenzione corretto").

### I8 — Strategia di test non definita operativamente

§19 elenca i tipi di test e le priorità, ma la decisione difficile non è "fare integration test": è **come si ottiene un SQL Server per i test**.

Opzioni reali: Testcontainers (fedele, lento, richiede Docker in CI), LocalDB (solo Windows, diverge da prod), SQLite in-memory (veloce, ma diverge su spatial/decimal/collation — sconsigliato qui), database condiviso + Respawn.

Va deciso ora perché condiziona la scrittura di ogni test da qui in avanti. Owner: **@argus**. Serve anche una convenzione per i **test data builder** sul dominio camper (un `Camper` valido ha ~15 campi).

### I9 — Design system proprietario: costo sottovalutato

§16 chiede un design system completo (palette, tipografia, spacing, card, mappe, timeline, chart, empty/loading state, animazioni) e §14 rifiuta esplicitamente la "UI standard React".

Per un progetto a singolo sviluppatore, costruirlo da zero è il modo più efficiente di non arrivare mai alla Phase 1. L'alternativa sensata è **primitive headless (Radix / shadcn) + design token proprietari + componenti di dominio custom** (map card, trip timeline, cost breakdown): la personalità visiva sta nei token e nei componenti di dominio, non nei bottoni.

Decisione MEDIUM. Owner: **@pixel**.

### I10 — CI/CD: overengineering iniziale e migration mancanti

§20 propone 5 workflow (`ci.yml`, `backend.yml`, `frontend.yml`, `docker.yml`, `deploy.yml`). All'inizio è un costo di manutenzione senza beneficio: **un `ci.yml` con job paralleli** (backend / frontend / docker) più un `deploy.yml` separato è sufficiente.

Mancano invece cose importanti:
- **strategia migration in deploy**: mai `Database.Migrate()` all'avvio in produzione; usare `dotnet ef migrations bundle` come step controllato, con backup e rollback;
- **nessun target di deploy dichiarato** (Azure App Service? Container Apps? VPS?): senza questo, Docker e "environment separation" sono decorativi;
- **gestione segreti** e mapping environment → configurazione;
- caching di NuGet/npm, altrimenti la CI diventa lenta e la si smette di guardare.

Owner: **@vulcan**.

### I11 — AI: nessun modello di costo, nessun confine

§12 descrive un Copilot di dominio, ma non dichiara provider, costo per richiesta, latenza attesa, comportamento in caso di fallimento, né cosa viene inviato (che è dato personale — vedi C4).

**Azione:** l'Intelligence va isolata dietro un'interfaccia applicativa con **feature flag**, deve essere sempre degradabile (se l'AI non risponde, l'app funziona lo stesso) e non deve mai essere l'unico modo per ottenere un risultato.

---

## 4. Suggested Improvements

- **Offline / PWA.** I camperisti sono spesso senza rete: checklist di partenza, journal e dettagli del viaggio andrebbero consultabili offline. Ha impatto architetturale forte (sync, conflitti) e va deciso presto, anche solo per escluderlo esplicitamente.
- **i18n.** I mockup (§15) sono in inglese ("Good morning, Luca"), il target sembra italiano. Anche solo predisporre le stringhe evita un refactor trasversale.
- **Strumentazione delle metriche.** §26 definisce metriche eccellenti ma nessun meccanismo per raccoglierle: servono eventi di dominio tracciati (activation, trip created/completed).
- **Osservabilità.** §23 non nomina OpenTelemetry: in .NET è ormai la via standard e costa poco attivarla subito (traces + correlation id), rinviando solo il backend di raccolta.
- **Seed data reale.** Le checklist (§9) e le manutenzioni tipiche sono contenuto di prodotto, non dati di test: vanno versionate e gestite come seed controllato (già previsto in §3).
- **Formato ADR.** §29 elenca i file ma non definisce il formato né il processo (quando si scrive un ADR, chi lo approva, come si supera). Usare la skill `domain-modeling` + `docs/adr/ADR-FORMAT.md`.
- **Glossario / ubiquitous language.** "Stop", "Place", "Trip", "Journal" ricorrono con sfumature diverse: un glossario in `CONTEXT.md` previene divergenze tra DB, API e UI.
- **Dashboard come read model.** `GET /api/v1/dashboard` aggrega 6-7 fonti: va progettata come query dedicata con projection (e misurata), non come composizione di N query per widget. Owner: **@oracle**.

---

## 5. Regression Risks

Rischi che il piano, così com'è, introdurrebbe nel tempo:

| Rischio | Origine | Mitigazione |
|---|---|---|
| Codice generato su PostgreSQL/Npgsql | C1 (tabella §27) | Correggere il documento *prima* della prima slice |
| IDOR su risorse di altri utenti | C3 | Query filter globale + test per ogni slice con `{id}` |
| Feature strategica (routing) irrealizzabile o fuori budget | C2 | Spike time-boxed prima della Phase 3 |
| Duplicazione incontrollata tra slice | §5 "duplicazione controllata" senza soglia | Regola esplicita: alla terza ripetizione si promuove in `Common/` |
| Migrazioni divergenti tra dev e prod | I10 | Migration bundle + niente migrate all'avvio |
| Refactor totale della UI | I9 | Decidere la base componenti prima della Phase 1 |
| Rework su dati personali | C4 | Export/delete progettati nell'MVP |

---

## 6. Decisioni da prendere prima di iniziare

Elencate in ordine di urgenza, da trattare una alla volta con il formato **Decision Required** dell'orchestratore (opzioni + raccomandazione + tradeoff):

| # | Decisione | Severità | Agente |
|---|---|---|---|
| 1 | Conferma SQL Server + libreria spatial (chiude C1) | HIGH | @oracle |
| 2 | Modello di autenticazione e autorizzazione owner-scoped (chiude C3) | HIGH | @sentinel |
| 3 | Provider dati geografici e routing (chiude C2) | HIGH | @archimedes |
| 4 | Requisiti privacy/GDPR minimi per l'MVP (chiude C4) | HIGH | @sentinel |
| 5 | MediatR sì/no in Vertical Slice (I3) | MEDIUM | @archimedes |
| 6 | Strategia integration test e provisioning DB di test (I8) | MEDIUM | @argus |
| 7 | Base del design system: headless + token vs da zero (I9) | MEDIUM | @pixel |
| 8 | Storage file/foto (I6) | MEDIUM | @vulcan |
| 9 | Target di deploy e strategia migration (I10) | MEDIUM | @vulcan |
| 10 | Offline/PWA: dentro o fuori scope (suggerimento) | MEDIUM | @archimedes |

Nessuna implementazione dovrebbe partire prima delle decisioni 1-4.

---

## 7. Sequenza di lavoro consigliata

Sostituisce §30, mantenendone lo spirito:

1. **Correggere il planning** (C1, I1): documento coerente e splittato in `docs/`.
2. **Decisioni 1-4** con Decision Gate.
3. **Domain model** (`domain-modeling` → `docs/architecture/CONTEXT.md`) con stati di `Trip`, semantica di `Place`, `Money`, regole di ricorrenza manutenzione.
4. **ADR iniziali**: `0001-use-sql-server`, `0002-modular-monolith`, `0003-auth-and-ownership`, `0004-api-conventions`, `0005-geo-data-strategy`.
5. **Convenzioni API** (`ProblemDetails`, paginazione, versioning, concorrenza) prima del primo endpoint.
6. **Walking skeleton**: una slice banale end-to-end (health + login + `GET /api/v1/me`) che attraversa React → API → EF → SQL Server → CI → deploy. Serve a validare la pipeline, non il dominio.
7. **Prima slice di dominio**: `Create Camper` in TDD (`tdd` skill), con test di ownership.
8. **Poi** dashboard e manutenzione, secondo §30.
9. **@janus** valida ogni fase prima della successiva.

---

## 8. Correzioni puntuali al documento

Checklist operativa per l'editing di `Roamly_Planning.md`:

- [ ] §4 — sostituire `PostgreSQL + PostGIS` con `SQL Server + Spatial` nel diagramma.
- [ ] §21 — `postgres` → `mssql` nel compose; rimuovere "PostgreSQL" dall'evoluzione futura.
- [ ] §24 Phase 0 — "PostgreSQL" → "SQL Server".
- [ ] §27 — `Database: SQL Server`, `Geo: SQL Server Spatial (NetTopologySuite)`.
- [ ] §29 — `0001-use-postgresql.md` → `0001-use-sql-server.md`.
- [ ] Rinumerare le sezioni (due `# 6`) e uniformare i livelli di heading.
- [ ] §3 — dichiarare l'override rispetto ai default dell'orchestratore (.NET 10, GitHub Actions) e verificare la disponibilità di ASP.NET Core 10.
- [ ] §3 — sciogliere "MediatR o equivalente, se utile" in una decisione.
- [ ] §6 — aggiungere stati di `Trip`, natura di `Place`, `Money`, ownership di `Expense`, ricorrenza `MaintenanceItem`.
- [ ] §17 — aggiungere error model, paginazione, concorrenza, idempotency, policy di versioning.
- [ ] §19 — scegliere la strategia di provisioning del DB di test.
- [ ] §20 — ridurre a `ci.yml` + `deploy.yml`; aggiungere migration strategy e caching.
- [ ] §22 — aggiungere la regola di ownership (C3) e la sezione privacy/GDPR (C4).
- [ ] §23 — nominare OpenTelemetry.
- [ ] §24 — aggiungere criteri di uscita per fase.
- [ ] Nuove sezioni: **Storage file**, **Privacy & Data Protection**, **i18n & unità di misura**, **Strategia dati geografici**.

---

## 9. Verdetto

Il piano descrive un prodotto che vale la pena costruire, con un'architettura adatta alla sua scala reale (un modular monolith vertical-slice è la scelta giusta, e il rifiuto esplicito di microservizi/Kubernetes/Redis è maturo).

Va però trasformato da **documento di visione** a **documento eseguibile**: risolvere la contraddizione sul database, chiudere le quattro decisioni HIGH, stringere l'MVP su §30 e dare a ogni fase un criterio di completamento.

Fatto questo, il progetto è pronto per la prima vertical slice.
