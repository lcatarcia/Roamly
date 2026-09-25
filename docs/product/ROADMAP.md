# Roamly — Roadmap e scope

Stato: attivo · Ultimo aggiornamento: 2026-09-24
Origine: `docs/archive/Roamly_Planning_2026-09-24.md` §6bis, §24, §25, §30 — **rivisto** secondo review I7

---

## 1. MVP 1.0 — definizione stretta

Il planning originale dichiarava un MVP che includeva dashboard completa, anagrafica, equipaggiamento, manutenzione, documenti *e* Trip Planner *e* design system foundation. Era in contraddizione con la prima slice proposta dallo stesso documento.

**MVP 1.0 = la prima vertical slice completa, niente di più:**

```text
Login
  ↓
Create Camper
  ↓
Camper Dashboard
  ↓
Add Maintenance
  ↓
Maintenance reminder
```

**Criterio di uscita verificabile:**

> Un utente reale registra il proprio camper, inserisce una manutenzione ricorrente e riceve il primo promemoria corretto in dashboard, su ambiente deployato.

Tutto il resto è **1.1 o successivo**. Il Trip Planner è la **1.1**.

---

## 2. Fasi

Ogni fase ha un criterio di uscita verificabile. Nessuna fase inizia prima che @janus abbia validato la precedente.

### Phase 0 — Foundation

- repository e solution .NET;
- React / Vite / TypeScript;
- **SQL Server** (non PostgreSQL — vedi C1);
- Docker per lo sviluppo locale;
- GitHub Actions (`ci.yml`);
- environment configuration e gestione segreti;
- authentication foundation;
- **base del design system (decisione #7 ✅ chiusa** → [`adr/0007-design-system.md`](../adr/0007-design-system.md)**)**, in otto voci:
  1. manifesto dei **token semantici a due livelli**, chiaro **e scuro** — la dark mode è in Phase 1;
  2. i **cinque correttivi anti-look** (R11), da applicare **prima del primo componente**: è un'azione dimenticabile e la sola debolezza nota della scelta;
  3. setup Tailwind v4 e primo prelievo shadcn/ui, con riscrittura sui token;
  4. tipografia: Fraunces con subset e Inter, dentro il **budget di 120 KB** (R22);
  5. scala di spacing, radius, ombre, focus a doppio anello;
  6. i tre **verificatori CI** (contrasto sulle 47 coppie × 2 temi, copertura del manifesto, `axe`);
  7. **i18n** con i namespace `neutral.*` e `narrative.*` (R21): serve dalla prima stringa, non dopo;
  8. il **`Ribbon`** come prototipo statico, prima di collegarlo ai dati.

> Le stime della Phase 0 vanno riviste al rialzo per la dark mode: le voci 1 e 2 passano a ~1 giorno ciascuna, la 6 a ~0,75. È il costo che l'utente ha accettato scegliendo la dark mode in Phase 1.

**Uscita:** walking skeleton verde — health check + login + `GET /api/v1/me` attraversano React → API → EF Core → SQL Server → CI → ambiente deployato.

### Phase 1 — Camper (MVP 1.0)

- camper profile e specifications;
- equipment;
- maintenance con ricorrenza km/tempo;
- ~~documents~~ → **fuori dalla Phase 1** (decisione dell'utente, 2026-09-24): rimanda tutto lo storage file, quindi la decisione #8 e i vincoli F1-F7;
- dashboard;
- **seed di 3-5 manutenzioni tipiche** alla creazione del camper, modificabili ed eliminabili (R24) — mitiga il problema del "giorno zero", in cui la prima dashboard sarebbe vuota;
- **export dei dati e cancellazione account** (`POST /api/v1/me/export`, `POST /api/v1/me/deletion`), con le **tre schermate** di ADR-0004 progettate **insieme alla dashboard** e non alla fine (~2-3 giorni di lavoro UI). Il motivo è che schermate di privacy fatte in fretta all'ultimo diventano esattamente le schermate ambigue che ADR-0004 vuole impedire.

> ⚠️ **Due prerequisiti aperti pesano su questa fase.**
> - Il `Ribbon` della dashboard dipende dalla **lettura corrente del contachilometri** (`CONTEXT.md` §3.9, @archimedes). Se resta indeciso, il componente degrada sul solo asse temporale (R25).
> - 🔴 Il gap sulla **revoca della cancellazione** (`SECURITY.md` §3, @sentinel + @hermes) **blocca il rilascio** della schermata "Elimina account" e va risolto entro il criterio di uscita di questa fase.

**Uscita:** il criterio di uscita dell'MVP 1.0 (§1), con test di ownership verdi su ogni slice che espone un `{id}`, **più**: export e cancellazione funzionanti con i test di privacy di `TESTING.md` §6 verdi, e **informativa pubblicata**.

> Gli endpoint di export e cancellazione **non sono nell'MVP 1.0 stretto** — con un solo utente (lo sviluppatore) non servono. Sono criterio di uscita della Phase 1, cioè **prima del primo utente reale**. Ciò che entra fin dalla prima migration è lo **schema** ([`ADR-0004`](../adr/0004-privacy-and-erasure.md)). Distinzione necessaria, altrimenti diventa scope creep sulla prima slice.

### Phase 2 — Trips (1.1)

- trip CRUD e stati del viaggio;
- stops;
- route (stima, non vehicle-aware);
- expenses;
- checklist;
- journal.

**Uscita:** un viaggio reale viene pianificato, completato e compare nel journal con costi consuntivi.

### Phase 3 — Maps & Places

- mappa e geolocalizzazione;
- places, search, saved places;
- dati geografici.

**Pre-requisito bloccante:** spike geo time-boxed (max 3 giorni) + ADR `geo-data-strategy` conclusi **prima** dell'inizio della fase.

**Uscita:** l'utente salva un luogo dalla mappa e lo riusa come tappa di un viaggio.

### Phase 4 — Intelligence

- vehicle-aware constraints;
- recommendations;
- checklist contestuale;
- cost estimation;
- AI Copilot.

**Uscita:** ogni funzione Intelligence è disattivabile da feature flag e l'app resta pienamente utilizzabile a flag spento.

Prerequisiti introdotti da [`ADR-0004`](../adr/0004-privacy-and-erasure.md), da soddisfare **prima** del primo invio di dati a un modello di terze parti:

- **tabella dei consensi** (il feature flag non è il consenso: la condizione effettiva è `flag ∧ consenso`);
- accordo con il provider AI e valutazione del trasferimento **extra-UE**;
- dichiarazione esplicita di quali campi lasciano il sistema.

### Phase 5 — Personal Memory

- travel history;
- preference extraction;
- recommendations basate sullo storico;
- personalized insights.

**Uscita:** l'utente può vedere, correggere ed esportare il profilo inferito su di lui.

### Phase 6 — Community

Solo se il prodotto lo richiederà: places condivisi, ratings, reviews, dati di accessibilità camper-specifici, qualità collaborativa del dato.

**Pre-requisito:** la semantica di `Place` (POI globale vs luogo dell'utente) deve essere già risolta dalla Phase 1 — vedi `docs/architecture/CONTEXT.md`.

---

## 3. Cosa NON fare inizialmente

Evitare:

- microservizi;
- Kubernetes;
- event-driven architecture complessa;
- Redis senza necessità;
- social network;
- marketplace;
- app mobile native;
- AI ovunque;
- decine di integrazioni esterne;
- sistema di recensioni complesso.

La prima versione deve essere **solida, bella e realmente utilizzabile dal proprietario di un camper**.

---

## 4. Sequenza di lavoro prima del codice

Sostituisce §30 del planning originale, mantenendone lo spirito.

| # | Passo | Stato |
|---|---|---|
| 1 | Correggere e splittare il planning in `docs/` | ✅ fatto (2026-09-24) |
| 1 | Correggere e splittare il planning in `docs/` | ✅ fatto (2026-09-24) |
| 2 | Chiudere le decisioni HIGH #1-#4 con Decision Gate | 🔶 #1 ✅ · #2 ✅ · #4 ✅ · **#3 aperta** |
| 3 | Domain model in `docs/architecture/CONTEXT.md` (stati di `Trip`, semantica di `Place`, `Money`, ricorrenza manutenzione, **lettura del contachilometri** §3.9) | ✅ **fatto (2026-09-24)** |
| 3b | **Decisione #6 — provisioning del DB di test** | ✅ **chiusa 2026-09-25** (@argus) → [`adr/0009-test-strategy.md`](../adr/0009-test-strategy.md). Testcontainers + Respawn |
| 3c | **Chiave primaria** — la lacuna con il costo di inversione più alto del progetto | ✅ **chiusa 2026-09-25** (@oracle) → [`adr/0008-primary-key-strategy.md`](../adr/0008-primary-key-strategy.md). PK composita `(OwnerId, Id)` CLUSTERED |
| 3d | **Infrastruttura di test — ~1,5 giorni, prima della prima slice**, in 5 checkpoint: (1) solution e 4 progetti di test · (2) Testcontainers, primo test verde · (3) POCO strutturali Phase 2+ · (4) **test dello schema completo eseguito** — è ciò che chiude `CONTEXT.md` §3.11 · (5) verificatori L0 e pipeline a 4 job | ⬜ **prossimo passo — è qui che comincia il codice** |
| 4 | ADR restanti: `0002-modular-monolith`, `0005-api-conventions`, `0006-geo-data-strategy` | 🔶 `0001` ✅ · `0003` ✅ · `0004` ✅ · `0007` ✅ · `0008` ✅ · `0009` ✅ |
| 5 | Convenzioni API complete **prima** del primo endpoint | ⬜ da fare |
| 6 | Walking skeleton (health + login + `GET /api/v1/me`) | ⬜ da fare |
| 7 | Prima slice di dominio: `Create Camper` in TDD, con test di ownership | ⬜ da fare |
| 8 | Dashboard e manutenzione | ⬜ da fare |

**Nessuna implementazione di dominio parte prima del completamento del passo 3d.** Il gate alle decisioni bloccanti è superato: ciò che resta prima della prima slice non è più una decisione, è lavoro.

> La #3 (provider geo) non blocca l'MVP 1.0, che non contiene mappe. Blocca la Phase 3. Le tre lacune che il passo 3 aveva lasciato aperte sono ora tutte chiuse: semantica di `Place` (§3.2), modellazione di `Expense` (§3.3) e chiave primaria (§3.8).
