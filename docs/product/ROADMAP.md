# Roamly — Roadmap e scope

Stato: attivo · Ultimo aggiornamento: 2026-10-21
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

Per la sequenza approvata il 2026-10-07, il criterio funzionale viene verificato localmente e in CI; la pubblicazione generale resta rinviata a dopo la Phase 5. Il 2026-10-22 l'utente ha approvato una deroga limitata per preparare un alpha privato con scope fino alla Phase 2; la deroga non autorizza provisioning o rilascio e richiede ancora decisioni su accesso e gate tecnici.

Tutto il resto è **1.1 o successivo**. Il Trip Planner è la **1.1**.

> **Decisione di scope pre-pubblicazione (2026-10-07):** completare le Phase 2-5 prima della pubblicazione generale. Ogni fase mantiene la validazione Janus, con verifiche locali/in CI fino al completamento della Phase 5. Il deployment reale è un gate separato. Il 2026-10-22 l'utente ha approvato una deroga limitata per preparare un alpha privato con scope fino alla Phase 2, senza completare prima le Phase 3-5, e ha scelto l'accesso per un massimo iniziale di 3 account totali (incluso il titolare), nominativi e provisionati manualmente; la registrazione Production resta chiusa. I dati di camper, viaggio e Journal devono essere sintetici per tutti; è ammessa un'email reale solo come identificativo di login, che resta dato personale da proteggere. Gli account alpha devono cambiare password al primo accesso; il backend applicherà un flag persistito su `ApplicationUser` con backfill degli account già provisionati. Le password temporanee saranno casuali e univoche, consegnate con condivisione sicura tramite password manager; il reset dopo lo smarrimento resta amministrativo e fuori banda. Test, rollback e gate tecnici restano da definire. La deroga modifica il perimetro di prodotto, ma non autorizza provisioning o rilascio. La Community (Phase 6) resta fuori dal perimetro attuale. Il 2026-10-20 l'utente aveva autorizzato separatamente il solo provisioning dello storage Blob secondo ADR-0010; tale autorizzazione resta limitata a quello storage.
>
> Le decisioni ancora da risolvere lungo il percorso sono tracciate in [`DECISION-MAP-PRE-PUBBLICAZIONE.md`](DECISION-MAP-PRE-PUBBLICAZIONE.md).

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
- manutenzione ricorrente km/tempo, registrazione immutabile degli interventi e storico;
- promemoria con assi tempo/km separati, visibili nella dashboard dopo il primo intervento; nessuna email, push o job in background;
- nessun dato seed automatico in questa slice: le regole sono inserite dall'utente;
- ~~documents~~ → **fuori dalla Phase 1** (decisione dell'utente, 2026-09-24): i documenti del camper restano Phase 4. Le foto del Journal Phase 2 seguono invece ADR-0010 e i vincoli F1-F7;
- dashboard;
- **seed di 3-5 manutenzioni tipiche** alla creazione del camper, modificabili ed eliminabili (R24) — mitiga il problema del "giorno zero", in cui la prima dashboard sarebbe vuota;
- **export dei dati e cancellazione account** (`POST /api/v1/me/export`, `POST /api/v1/me/deletion`), con le **tre schermate** di ADR-0004 progettate **insieme alla dashboard** e non alla fine (~2-3 giorni di lavoro UI). Il motivo è che schermate di privacy fatte in fretta all'ultimo diventano esattamente le schermate ambigue che ADR-0004 vuole impedire.

> ⚠️ **Due prerequisiti aperti pesano su questa fase.**
> - La decisione sulla dashboard camper è chiusa: `GET /api/v1/campers/{camperId}/dashboard` espone manutenzione su assi tempo/km indipendenti. Lettura assente o oltre 90 giorni ⇒ asse km non verificabile; nessuna previsione o stato complessivo. Vedi [`CONTEXT.md` §3.9](../architecture/CONTEXT.md).
> - 🔴 Il gap sulla **revoca della cancellazione** (`SECURITY.md` §3, @sentinel + @hermes) **blocca il rilascio** della schermata "Elimina account" e va risolto entro il criterio di uscita di questa fase.

**Uscita:** il criterio di uscita dell'MVP 1.0 (§1), con test di ownership verdi su ogni slice che espone un `{id}`, **più**: export e cancellazione funzionanti con i test di privacy di `TESTING.md` §6 verdi, e **informativa pubblicata**.

> Gli endpoint di export e cancellazione **non sono nell'MVP 1.0 stretto** — con un solo utente (lo sviluppatore) non servono. Sono criterio di uscita della Phase 1, cioè **prima del primo utente reale**. Ciò che entra fin dalla prima migration è lo **schema** ([`ADR-0004`](../adr/0004-privacy-and-erasure.md)). Distinzione necessaria, altrimenti diventa scope creep sulla prima slice.

### Phase 2 — Trips (1.1)

**Stato al 2026-10-21:** completati Trip/TripStop, gestione Expense, Checklist manuali, stime manuali di distanza e durata e stima locale live del solo costo carburante. La UI Viaggi consente modifica inline di viaggio e tappe, riordino e transizioni esplicite con conferma prima dell'annullamento; la UI Spese nel dettaglio del viaggio supporta lettura paginata e gestione CRUD con ETag per riga. La UI Checklist supporta elenco paginato, CRUD di checklist e voci, completamento e riordino; la raccolta è owner-scoped e paginata via cursore, e le mutazioni usano l'ETag del Trip con recupero esplicito dai conflitti `412`. Le stime di distanza/durata sono inserite manualmente, indipendenti e facoltative; la stima carburante usa distanza, consumo medio Camper e prezzo/litro facoltativo persistito come `Money`, senza provider o conversione valuta. Il risultato è derivato e non persistito, non è mostrato per viaggi completati/annullati e per `Electric` è dichiarato non disponibile. Viaggi/Journal resta online-only: nessuna persistenza offline o cache persistente. Il Journal UI espone note e allegati immagine tramite upload mediato dall'API, anteprima su richiesta ed eliminazione confermata. Il 2026-10-20 sono stati creati e verificati lo storage account Azure `storoamlywse7wit7fzpja` e il container privato `roamly-journal-images` secondo ADR-0010. Il test d'accettazione con owner e file sintetici ha superato upload HTTP, download SAS, export, delete/outbox e account-erasure su Azure; il cleanup ha lasciato zero blob e i ruoli RBAC temporanei sono stati revocati. Non sono stati usati dati personali né pubblicata l'app. Separatamente, un viaggio reale è stato verificato localmente con pianificazione, completamento, Journal e costo consuntivo ufficiale totale di 153 EUR, registrato come spesa aggregata "Altro" con componenti non categorizzate. Janus ha approvato la chiusura operativa della Phase 2 il 2026-10-20 sulla base delle prove applicative dichiarate e verificate localmente. La decisione #3 in `OPEN-DECISIONS.md` continua a bloccare Maps & Places della Phase 3.

> **Validazione aggiornata (2026-10-20):** suite backend completa **308/308**, inclusa R51 dopo aggiornamento dello snapshot approvato dall'utente; build, lint, flussi browser Camper/Trip/Journal/Privacy e `design:check` verdi. Il test d'accettazione Azure è passato con dati sintetici; la configurazione effettiva dell'account è stata verificata separatamente prima del test. La validazione Janus precedente approvava la readiness tecnica locale, non la chiusura operativa né un rilascio. Dopo la registrazione e la verifica locale, anche dopo reload, del costo consuntivo ufficiale di 153 EUR, Janus ha approvato il 2026-10-20 la chiusura operativa della Phase 2, limitatamente al criterio di uscita documentato. Questa approvazione non equivale a readiness di rilascio. La decisione geo #3 resta un prerequisito bloccante per la Phase 3.

- trip CRUD e stati del viaggio;
- stops;
- stime manuali di distanza e durata totali (non vehicle-aware);
- stima locale e live del solo costo carburante per viaggi `Planned` e `Active`, con prezzo per litro manuale `Money` e consumo medio del Camper; senza provider/conversione, non disponibile per `Electric`, non mostrata per `Completed`/`Cancelled`;
- expenses;
- checklist;
- Journal testuale e UI allegati implementati; configurazione Blob verificata e test API su Azure reale superato con owner e file sintetici (vedi [`TESTING.md` §13.2](../architecture/TESTING.md)). La pianificazione, il completamento e la presenza dei ricordi nel Journal sono stati verificati localmente; il consuntivo ufficiale di 153 EUR è registrato in una voce aggregata "Altro" con le componenti esplicitate nella descrizione.

**Uscita:** un viaggio reale viene pianificato, completato e compare nel journal con costi consuntivi.

### Phase 3 — Maps & Places

**Stato al 2026-10-20: rinviata su scelta dell'utente.** La decisione geo #3 resta aperta/differita; non è stato selezionato un provider né autorizzato alcun test live o trasferimento di coordinate. La fase non è completata. Prima di riprenderla serviranno la decisione `geo-data-strategy` e lo spike previsto. Le attività di fasi successive che dipendono da provider, posizioni o routing restano sospese; quelle indipendenti vanno valutate separatamente.

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

**Uscita:** l'utente può vedere, correggere, esportare e cancellare il profilo inferito su di lui; il profilo è usato solo in funzioni richieste esplicitamente e non viene trasmesso automaticamente a terzi.

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
| 3d | **Infrastruttura di test** nei 5 checkpoint: (1) solution e progetti di test · (2) Testcontainers · (3) POCO strutturali Phase 2+ · (4) test dello schema completo · (5) verificatori L0 e pipeline | ✅ checkpoint 1-5 completati; conferma in [`ADR-0009`](../adr/0009-test-strategy.md) e `TESTING.md` |
| 4 | ADR e convenzioni API | ✅ convenzioni API in [`ADR-0005`](../adr/0005-api-conventions.md); decisione geo #3 aperta, non bloccante per MVP |
| 5 | Convenzioni API complete **prima** del primo endpoint | ✅ |
| 6 | Walking skeleton (health + login + `GET /api/v1/me`) | ✅ presente |
| 7 | Prima slice di dominio: `Create Camper` in TDD, con test di ownership | ✅ presente |
| 8 | Dashboard, regole e storico manutenzione | ✅ slice implementata; criterio di uscita deployato dell'MVP ancora da verificare |

**Nessuna implementazione di dominio parte prima del completamento del passo 3d.** Il gate alle decisioni bloccanti è superato: ciò che resta prima della prima slice non è più una decisione, è lavoro.

> La #3 (provider geo) non blocca l'MVP 1.0, che non contiene mappe. Blocca la Phase 3. Le tre lacune che il passo 3 aveva lasciato aperte sono ora tutte chiuse: semantica di `Place` (§3.2), modellazione di `Expense` (§3.3) e chiave primaria (§3.8).
