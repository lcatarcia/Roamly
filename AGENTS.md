# Roamly — Istruzioni di progetto

## Contesto di progetto (leggere prima di operare)

Il progetto è **in implementazione**. Le decisioni architetturali approvate sono indicizzate in `docs/README.md`; le decisioni e i gate ancora aperti sono tracciati in `docs/OPEN-DECISIONS.md` e `docs/product/DECISION-MAP-PRE-PUBBLICAZIONE.md`.

- Indice documentazione: `docs/README.md`
- **Avanzamento:** Phase 2 è chiusa secondo `docs/product/ROADMAP.md`. La Phase 3 è differita: la decisione geo #3 resta aperta e blocca Maps & Places. Il confine prodotto #4 (Intelligence/AI) è definito e Gemini UE è il provider di riferimento scelto; la due diligence pubblica #7 è documentata. Nella mappa pre-pubblicazione le #8-#9 registrano l'accettazione dei default anti-abuso per modelli ordinari e la scelta di disabilitare la cache implicita; Gemini 3.1 Flash-Lite è solo il primo candidato per una futura valutazione sintetica (#10 della mappa), non una scelta di produzione. Contratto, modello e applicazione effettiva dei controlli vanno verificati prima dell'uso; la configurazione è rimandata. La Personal Memory (#5 della mappa) è definita come profilo inferito trasparente e usato solo su richiesta. La decisione offline/PWA #10 di `OPEN-DECISIONS.md` è chiusa: niente modalità offline, PWA installabile o cache applicativa dei dati utente nella v1. Il deploy #9 resta da configurare.
- Vincoli non negoziabili già stabiliti: **SQL Server** (non PostgreSQL), **ASP.NET Core 10**, **GitHub Actions**, ownership owner-scoped su ogni query, **PK composita `(OwnerId, Id)`** (ADR-0008).
- I checkpoint infrastrutturali 1-5 della strategia di test sono chiusi; la suite completa si esegue con `dotnet test --solution Roamly.slnx`.

### Comandi di test

Due livelli, separati per **costo**, non per livello architetturale ([`docs/adr/0009-test-strategy.md`](docs/adr/0009-test-strategy.md)):

| Comando | Cosa | Costo | Docker |
|---|---|---|---|
| `dotnet test --project tests/Roamly.Model.Tests` | **L0** — dominio + 17 dei 25 verificatori | **< 5 s** | **no** |
| `dotnet test --solution Roamly.slnx` | suite completa, include Testcontainers | 60-100 s in CI, 5-15 s in locale | sì |

> ⚠️ Col runner Microsoft.Testing.Platform di .NET 10 RTM il percorso del progetto **non è più posizionale**: serve `--project` o `--solution`. Un progetto di test senza test produce exit code 8, quindi i nuovi progetti di test devono aggiungere almeno un test prima di entrare nella suite ([`TESTING.md`](docs/architecture/TESTING.md) §13).

Usa **L0 durante il lavoro**: EF Core costruisce `DbContext.Model` offline con `UseSqlServer` senza mai connettersi, quindi la maggior parte delle regole si verifica senza database. La suite completa prima di consegnare.

⚠️ **ARM64 non è supportato**: Azure SQL Edge era l'unica via ufficiale ed è stato ritirato il 30/09/2025. Su Apple Silicon o Windows-on-ARM gli integration test non sono eseguibili.

## Sistema di agenti (obbligatorio)

Questo progetto usa l'orchestratore e il set di agenti condivisi dell'utente, che vivono fuori dal repo:

- Orchestratore: `C:\dev\ai-agents\orchestrator.md`
- Agenti: `C:\dev\ai-agents\agents\*.md`

**Regola:** all'inizio di ogni task su questo progetto leggi `C:\dev\ai-agents\orchestrator.md` e applicane integralmente
FLOW, DECISION GATE, DECISION SEVERITY, RULES, GLOBAL AGENT RULES, REGRESSION PREVENTION POLICY e HUMAN-IN-THE-LOOP POLICY.
Quei file sono la single source of truth: non duplicarli nel repo, non reinterpretarli.

## Agenti disponibili

Sono esposti come custom agent Copilot in `.github/agents/` (wrapper sottili che caricano le definizioni condivise):

| Agente | Dominio |
|---|---|
| `archimedes` | Architettura, moduli, boundaries, tradeoff |
| `solomon` | Clean code + vertical slice, implementazione feature |
| `oracle` | Data layer, EF Core, query e migrazioni (file sorgente: `horacle.md`) |
| `hermes` | API REST, DTO, Swagger, versioning |
| `argus` | Testing, edge case, regression coverage |
| `sentinel` | Security, hardening, authorization |
| `vulcan` | CI/CD, deploy, logging, monitoring |
| `pixel` | UX/UI, accessibilità, consistenza frontend |
| `janus` | Quality gate finale, production readiness |
| `scribe` | Audit trail tecnico e storia architetturale |

## Come vanno usati

- Instrada ogni richiesta all'agente competente secondo il FLOW dell'orchestratore; per task complessi concatena gli agenti
  (es. `archimedes` → `solomon` → `oracle` → `hermes` → `argus` → `sentinel` → `vulcan` → `janus`).
- `janus` ha autorità finale: nessuna feature è production-ready senza la sua validazione.
- Nessun agente esce dal proprio dominio.
- Decisioni MEDIUM/HIGH → consulto obbligatorio con l'utente nel formato "Decision Required" dell'orchestratore.

## Skill di progetto

Le skill vivono in `.github/skills/<nome>/SKILL.md` e sono adattate allo stack Roamly.

**Engineering:** `tdd` · `implement` · `codebase-design` · `domain-modeling` · `diagnosing-bugs` · `resolving-merge-conflicts` · `improve-codebase-architecture` · `prototype` · `grill-with-docs`
**Prodotto e flusso:** `to-prd` · `to-issues` · `triage` · `review` · `decision-mapping`
**Collaborazione:** `grilling` · `grill-me` · `handoff` · `teach` · `writing-great-skills` · `which-skill`
**Setup:** `setup-pre-commit` · `git-guardrails`

Se non sai quale usare, parti da `which-skill`.

Skill e agenti sono complementari: **l'agente decide chi risponde** (dominio e autorità), **la skill decide come si lavora** (procedura). Esempio: `argus` + `tdd`, `archimedes` + `domain-modeling`, `janus` + `review`.

## Manutenzione

Per modificare il comportamento degli agenti, edita i file in `C:\dev\ai-agents\`.
I wrapper in `.github/agents/` vanno toccati solo per aggiungere/rimuovere un agente.
