# Roamly — Documentazione

Stato del progetto: **pre-implementazione.** Nessun codice è stato scritto.

Il planning originale è stato splittato e corretto il 2026-09-24 a seguito della review di @janus. Gli originali sono preservati verbatim in `docs/archive/`.

---

## Indice

### Prodotto

| Documento | Contenuto |
|---|---|
| [`product/VISION.md`](product/VISION.md) | Visione, principi di prodotto, pilastri, metriche di successo |
| [`product/FEATURES.md`](product/FEATURES.md) | Descrizione funzionale: dashboard, camper, trip planner, routing, checklist, costi, journal, intelligence, memory |
| [`product/ROADMAP.md`](product/ROADMAP.md) | MVP 1.0, fasi con criteri di uscita, anti-scope, sequenza di lavoro |
| [`product/UX.md`](product/UX.md) | Direzione UX, dashboard, design system, i18n, offline |

### Architettura

| Documento | Contenuto |
|---|---|
| [`architecture/ARCHITECTURE.md`](architecture/ARCHITECTURE.md) | Stack, struttura repo, Vertical Slice + CQRS, modular monolith, observability, Docker |
| [`architecture/CONTEXT.md`](architecture/CONTEXT.md) | Domain model e ubiquitous language — **incompleto, da completare prima del codice** |
| [`architecture/DATA.md`](architecture/DATA.md) | SQL Server, regole di persistenza, seed, migration |
| [`architecture/API-CONVENTIONS.md`](architecture/API-CONVENTIONS.md) | Endpoint, error model, paginazione, versioning, concorrenza |
| [`architecture/SECURITY.md`](architecture/SECURITY.md) | Baseline, **regola di ownership**, privacy/GDPR, confine AI, upload |
| [`architecture/TESTING.md`](architecture/TESTING.md) | Tipi di test, priorità, provisioning DB di test |
| [`architecture/DEVOPS.md`](architecture/DEVOPS.md) | CI/CD, migration in deploy, target, segreti |

### Decisioni

| Documento | Contenuto |
|---|---|
| [`OPEN-DECISIONS.md`](OPEN-DECISIONS.md) | **Decisioni aperte + rischi monitorati. Partire da qui.** |
| [`adr/ADR-FORMAT.md`](adr/ADR-FORMAT.md) | Formato e processo ADR |
| [`adr/0001-use-sql-server.md`](adr/0001-use-sql-server.md) | ✅ SQL Server confermato, tipi spaziali rimandati con trigger |
| [`adr/0003-auth-and-ownership.md`](adr/0003-auth-and-ownership.md) | ✅ Identity + cookie httpOnly; ownership a tre livelli, `OwnerId` su ogni entità, `404` uniforme |
| [`adr/0004-privacy-and-erasure.md`](adr/0004-privacy-and-erasure.md) | ✅ Topologia FK `NO ACTION`, export model-driven, hard delete con 30 giorni di grazia, R8-R10 |
| [`adr/0007-design-system.md`](adr/0007-design-system.md) | ✅ Tailwind v4 + shadcn/ui + token proprietari; identità su `Ribbon`, dato numerico e materiale sand/contour; dark mode in Phase 1; R11-R25 |
| [`adr/0008-primary-key-strategy.md`](adr/0008-primary-key-strategy.md) | ✅ **PK composita `(OwnerId, Id)` CLUSTERED**, `Guid` COMB generato client-side via `IIdGenerator`; le 6 chiavi alternate `UNIQUE (Id, OwnerId)` **spariscono**; ❌ GUID v7 non è sequenziale su SQL Server (causa: ordinamento del tipo `uniqueidentifier`, non .NET); R26-R33 |
| [`adr/0009-test-strategy.md`](adr/0009-test-strategy.md) | ✅ Testcontainers `MsSql` pinnato + Respawn, un container per run; **4 progetti di test** separati per costo, non per livello; 💡 **17 verificatori su 25 non toccano il database**; doppio test 1785 (analizzatore + `EnsureCreated` reale); `TimeProvider` dal primo commit; R30-R39 |

### Archivio

| Documento | Contenuto |
|---|---|
| `archive/Roamly_Planning_2026-09-24.md` | Planning originale, verbatim |
| `archive/Roamly_Planning_Review_2026-09-24.md` | Review di @janus, verbatim |
| `archive/Design_Direction_Input_2026-09-24.md` | Direzione visiva fornita dall'utente, verbatim. ⚠️ **Da non applicare alla lettera**: contiene raccomandazioni respinte (Next.js) e una palette con errori di contrasto WCAG. Va letto **insieme alla tabella "Tieni / Adatta / Scarta"** di [`adr/0007-design-system.md`](adr/0007-design-system.md), che è la versione vincolante |

---

## Convenzioni di questi documenti

- 🔴 = bloccante: non si scrive codice finché non è risolto.
- ⚠️ = decisione aperta o gap noto, tracciato in `OPEN-DECISIONS.md`.
- Ogni documento dichiara **stato** e **origine** nell'intestazione.
- Le decisioni non si prendono dentro i documenti: si prendono con il Decision Gate e si registrano come ADR.

---

## Prossimo passo

Chiudere le decisioni **#1-#4** in `OPEN-DECISIONS.md`, una alla volta.
Nessuna implementazione parte prima.
