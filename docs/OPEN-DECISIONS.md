# Roamly — Decisioni aperte

Stato: attivo · Ultimo aggiornamento: 2026-09-24
Origine: review `docs/archive/Roamly_Planning_Review_2026-09-24.md` §6

Ogni decisione va trattata **una alla volta** con il formato **Decision Required** dell'orchestratore (opzioni + raccomandazione + tradeoff), come richiesto dalla HUMAN-IN-THE-LOOP POLICY.

Quando una decisione è chiusa: si scrive l'ADR corrispondente in `docs/adr/`, si aggiorna il documento di riferimento e si segna ✅ qui.

---

## Bloccanti — nessuna implementazione prima di queste

> **Stato: 3 su 4 chiuse, e il gate all'implementazione è superato.** Resta aperta solo la **#3** (provider geo), che **non blocca l'MVP 1.0** perché non contiene mappe: blocca la Phase 3. Anche i tre prerequisiti che il passo 3 di `ROADMAP.md` §4 aveva lasciato aperti — semantica di `Place`, modellazione di `Expense`, chiave primaria — sono ora chiusi. Ciò che resta prima della prima slice non è più una decisione: è l'infrastruttura di test del passo **3d** (~1,5 giorni).

| # | Decisione | Severità | Owner | Documento | Stato |
|---|---|---|---|---|---|
| 1 | Conferma SQL Server + libreria spatial — chiude C1 | HIGH | @oracle | [`adr/0001-use-sql-server.md`](adr/0001-use-sql-server.md) | ✅ **chiusa 2026-09-24** |
| 2 | Modello di autenticazione e autorizzazione owner-scoped — chiude C3 | HIGH | @sentinel | [`adr/0003-auth-and-ownership.md`](adr/0003-auth-and-ownership.md) | ✅ **chiusa 2026-09-24** |
| 3 | Provider dati geografici e routing — chiude C2 | HIGH | @archimedes | `product/FEATURES.md` §4 | ⬜ aperta |
| 4 | Requisiti privacy/GDPR minimi per l'MVP — chiude C4 | HIGH | @sentinel | [`adr/0004-privacy-and-erasure.md`](adr/0004-privacy-and-erasure.md) | ✅ **chiusa 2026-09-24** |

> **Esito #1:** SQL Server confermato; **NetTopologySuite e tipi spaziali rimandati**, coordinate come `decimal` WGS84 in **complex type** `Coordinates`, upgrade a `geography` pre-approvato su 4 trigger documentati. *Emendato il 2026-09-24 dal domain model: complex type invece di owned type, perché un value object non deve comparire nei verificatori di ownership ed erasure.*

> **Esito #2:** ASP.NET Core Identity con **cookie httpOnly** (endpoint come slice, non `MapIdentityApi`); ownership a **tre livelli** — query filter nominato `"OwnerScope"` in lettura, `SaveChangesInterceptor` in scrittura, `BannedApiAnalyzers` sulle vie di fuga; `OwnerId` denormalizzato su ogni entità con **FK composite**; **`404` uniforme**. Regole invarianti **R1-R7**, ognuna con un verificatore automatico. *Emendato da ADR-0004: due correzioni tecniche e `RunAsUser` come quarto modo di `ICurrentUser`.*

### Vincoli derivati dalla #2 su decisioni ancora aperte

| Decisione | Vincolo |
|---|---|
| **#9** deploy | 🔴 **same-site obbligatorio** (stesso dominio o reverse proxy). Domini separati → Parte A dell'ADR-0003 da rinegoziare. Serve inoltre un **key ring di Data Protection** condiviso e persistito |
| **#8** storage file | deve includere il controllo di ownership sugli **URL firmati**: R1-R7 si fermano al database |
| **#6** DB di test | ✅ **chiusa** → [`ADR-0009`](adr/0009-test-strategy.md). ❌ **La formulazione originale era sbagliata**: diceva «R3/R4/R7 verificabili solo con un database reale». **R7 è un contratto HTTP** (`WebApplicationFactory`, nessun DB) e **R4 è in gran parte convenzione** sul modello. Dei dieci verificatori, **solo R3 e R9 richiedono davvero SQL Server** |

> **Esito #4:** la parte irreversibile è la **topologia delle FK**, non il codice. `OwnerId → AspNetUsers` sempre **`NO ACTION`** (errore 1785), cascade solo gerarchico sulle FK composite già esistenti, **una sola cascade path** per coppia di tabelle, **`CreatedAtUtc` obbligatorio**. Export **ZIP model-driven**, **hard delete** con **30 giorni** di grazia, `ErasureReceipt` non owned. Regole **R8-R10**. Vincoli **F1-F7** dichiarati per la #8. Emendamento ad ADR-0003: `RunAsUser` come quarto modo di `ICurrentUser` e due correzioni tecniche.

### Vincoli derivati dalla #4 su decisioni ancora aperte

| Decisione | Vincolo |
|---|---|
| **#8** storage file | 🔴 **F1-F7 sono requisiti di accettazione.** In particolare F1 (chiavi owner-prefixed) prima del primo upload, F4 (soft delete/versioning reale del provider) **da verificare, non assumere**, F7 (ricevuta solo dopo sweep confermato) |
| **#9** deploy | scheduler affidabile per `AccountErasureJob`; **health check** su richieste scadute non processate; **retention del sink di logging**; finestra di **backup** da misurare (F5); ⚠️ hosting fuori dallo SEE → trigger |
| **#6** DB di test | 🔴 anche **R9** richiede un database reale; e i **test data builder devono coprire ogni entità owned**, altrimenti "erase and sweep" è parziale |
| **#10** offline/PWA | ⚠️ **punto nuovo, prima non tracciato**: una copia locale dei dati sopravvive alla cancellazione dell'account. Se la #10 entra in scope serve una policy di purge del client al logout e alla cancellazione, e va detto nell'informativa |
| **#3** provider geo | inviare **posizioni dell'utente** (non query generiche) a un provider esterno è un trasferimento a terzi → attiva il trigger della tabella consensi. La distinzione tra i due casi va fatta **nella #3** |
| **#7** design system | due schermate obbligatorie — "Scarica i miei dati" ed "Elimina account" — con conferma inequivocabile su **30 giorni di grazia e irreversibilità dopo**. Una conferma ambigua su un'azione irreversibile è un problema di sicurezza, non di UX |

> La #3 non blocca l'MVP 1.0 **solo** perché il Trip Planner v1 è dichiarato non vehicle-aware (`FEATURES.md` §3). Blocca la Phase 3 e richiede uno spike time-boxed di max 3 giorni.

---

## Da chiudere prima della Phase 1

| # | Decisione | Severità | Owner | Documento | Stato |
|---|---|---|---|---|---|
| 5 | MediatR sì/no in Vertical Slice (tecnico + licenza) | MEDIUM | @archimedes | `architecture/ARCHITECTURE.md` §1 | ⬜ aperta |
| 6 | Strategia integration test e provisioning DB di test | MEDIUM | @argus | [`adr/0009-test-strategy.md`](adr/0009-test-strategy.md) | ✅ **chiusa 2026-09-25** |
| 7 | Base del design system: headless + token vs da zero | MEDIUM | @pixel | [`adr/0007-design-system.md`](adr/0007-design-system.md) | ✅ **chiusa 2026-09-24** |
| 8 | Storage file/foto | MEDIUM | @vulcan | `architecture/SECURITY.md` §5 | ⬜ aperta |
| 9 | Target di deploy e strategia migration | MEDIUM | @vulcan | `architecture/DEVOPS.md` §2.2 | ⬜ aperta |
| 10 | Offline/PWA: dentro o fuori scope | MEDIUM | @archimedes | `product/UX.md` §5 | ⬜ aperta |

> **Esito #7:** **Tailwind v4 + shadcn/ui come generatore + token proprietari + componenti di dominio custom.** L'identità dell'MVP non poggia su fotografia e mappe — che non esistono prima della Phase 2-3 — ma su tre elementi realizzabili subito: il **Ribbon** (la route come asse astratto: in Phase 1 è l'asse km/tempo della manutenzione, in Phase 3 diventa la route geografica), il **dato numerico come superficie primaria** al posto della fotografia, e **sand, contour e texture generati in CSS/SVG**. Regole invarianti **R11-R25**, ognuna con un verificatore. **Dark mode in Phase 1** per scelta esplicita dell'utente. Palette corretta per accessibilità: il CTA del documento di input (clay + bianco, 3.20:1) **non passava WCAG AA** ed è stato sostituito da `clay-500` + `ink` (5.03:1); introdotto un token **`danger` separato da `clay`**.

### Vincoli derivati dalla #7 su decisioni ancora aperte

| Decisione | Vincolo |
|---|---|
| **#8** storage file | la fotografia è **dichiarata assente fino alla Phase 2**. Quando entra, non deve sostituire il linguaggio visivo ma aggiungersi: il `Ribbon` e il dato numerico restano la struttura portante |
| **#3** provider geo | il `Ribbon` di Phase 3 è la **stessa componente** di Phase 1 con un asse geografico al posto di quello km/tempo. Un provider che non permetta un tracciato stilizzabile rompe l'elemento identitario principale |
| **#9** deploy | 🔴 **nessuna CDN di terzi** (R19): font self-hosted. È un vincolo di privacy, non di performance. Servono cache header per bundle statico e font `immutable` |
| **#10** offline/PWA | se entra in scope servono stati di sync, gestione dei conflitti e indicatore di connessione, tutti da progettare nel linguaggio visivo |
| **#6** DB di test | i tre verificatori CI del design system (contrasto sulle 47 coppie, copertura del manifesto, `axe` sui due temi) vanno in pipeline accanto a quelli di ownership e privacy |

---

## Gap minori tracciati

| Gap | Documento | Stato |
|---|---|---|
| Strumentazione eventi per le metriche di successo | `product/VISION.md` §4 | ⬜ |
| i18n: predisposizione stringhe | `product/UX.md` §4 | 🔴 **promosso a prerequisito** da ADR-0007: R21 (registro doppio) richiede i namespace `neutral.*` e `narrative.*` fin dalla prima stringa |
| OpenTelemetry attivo da subito | `architecture/ARCHITECTURE.md` §6 | ⬜ |
| Seed data come contenuto di prodotto | `architecture/DATA.md` §3 | 🟡 **parzialmente chiuso** da ADR-0007: alla creazione di un camper vengono create 3-5 manutenzioni tipiche, modificabili ed eliminabili (R24: l'origine da seed va persistita e marcata). Resta aperto il resto del seed |
| Domain model completo (stati Trip, Place, Money, Expense, ricorrenze) | `architecture/CONTEXT.md` §3 | ✅ **chiuso 2026-09-24** (@archimedes) — `SavedPlace` owned + `PublicPlace` in Phase 6; `Expense` con `CamperId` obbligatorio e `TripId` opzionale; `Money` multi-valuta senza conversione; `OdometerReading` datata; `Trip` a 4 stati con matrice di transizioni |
| Glossario / ubiquitous language | `architecture/CONTEXT.md` §4 | ✅ **compilato 2026-09-24** con il domain model |
| **Tipo della chiave primaria** | `architecture/CONTEXT.md` §3.8 | ✅ **chiuso 2026-09-25** (@oracle) → [`adr/0008-primary-key-strategy.md`](adr/0008-primary-key-strategy.md). **PK composita `(OwnerId, Id)` CLUSTERED**, `Guid` generato client-side (COMB sequenziale nell'ordinamento SQL Server). La domanda vera non era il *tipo* ma la **forma** della chiave |
| **Provider email** (conferma account, reset password) — nuovo da ADR-0003: dipendenza esterna non tracciata, costo e deliverability non indagati. Owner: @vulcan | `architecture/SECURITY.md` §1 | ⬜ |
| **Retention del sink di logging** — nuovo da ADR-0004: unico requisito dell'impianto privacy **non verificabile da un test**. Owner: @vulcan | `architecture/DEVOPS.md` §2.3 | ⬜ |
| **Informativa privacy** da scrivere e pubblicare prima del primo utente reale | `architecture/SECURITY.md` §3.5 | ⬜ |
| **Health check** su richieste di cancellazione scadute non processate | `architecture/DEVOPS.md` §5 | ⬜ |
| Convenzioni API complete | `architecture/API-CONVENTIONS.md` §2 | ⬜ |
| 🔴 **Da quale interfaccia si revoca la cancellazione** — nuovo da ADR-0007: ADR-0004 rende l'account immediatamente inaccessibile ma concede 30 giorni di ripensamento. Se l'utente non può più entrare, la revoca non ha una porta. **Blocca il rilascio della schermata "Elimina account"**, non il suo disegno. Owner: @sentinel + @hermes | `architecture/SECURITY.md` §3 | ⬜ |
| **Lettura corrente del contachilometri** — nuovo da ADR-0007: il `Ribbon` in km la richiede, e il domain model non la contiene ancora. Se la risposta è "si conoscono solo i km all'ultimo intervento", il `Ribbon` usa il solo asse temporale (fallback R25). Owner: @archimedes | `architecture/CONTEXT.md` §3 | ✅ **chiuso 2026-09-24**: entità `OdometerReading` datata, alimentata anche dai log di manutenzione. Serve comunque per "km annuali" e "€/km", quindi non è un costo del solo `Ribbon` |
| **`UNIQUE (Id, OwnerId)` su ogni tabella-padre** | `architecture/DATA.md` §2 | ✅ **eliminato 2026-09-25**: con la PK composita `(OwnerId, Id)` le FK composite referenziano direttamente la chiave primaria. **Il requisito non esiste più** — `CONTEXT.md` §5.3 va rimossa, non quantificata |
| **Totali multi-valuta nel Cost Management** — nuovo dal domain model: senza tassi di cambio un totale unico non esiste, e il mockup di `FEATURES.md` §6 non è realizzabile. Owner: @pixel | `product/FEATURES.md` §6 | ⬜ |
| **`TimeProvider` iniettato dal primo commit** — nuovo da ADR-0009: `TESTING.md` non lo richiedeva, e senza di esso ricorrenze e scadenze (`NextDueAtUtc`, finestra di 30 giorni dell'erasure) non sono testabili senza attese reali. Reintrodurlo dopo significa toccare ogni handler | `architecture/TESTING.md` | ✅ **chiuso 2026-09-25** → **R34** + `DateTime.UtcNow` in `BannedSymbols.txt` |
| **POCO strutturali delle entità Phase 2/3/4** — nuovo da ADR-0009: il test dello schema completo non è scrivibile senza di essi (~150 righe, ~2 h). ❌ `CONTEXT.md` §2.6 lasciava intendere il contrario | `architecture/CONTEXT.md` §2.6 | ✅ **chiuso 2026-09-25**: vivono in `Roamly.Domain`, registrati **solo** in `FullSchemaDbContext` |

---

## Rischi di regressione monitorati

| Rischio | Origine | Mitigazione | Stato mitigazione |
|---|---|---|---|
| Codice generato su PostgreSQL/Npgsql | C1 | Documentazione corretta prima della prima slice | ✅ applicata |
| IDOR su risorse di altri utenti | C3 | Tre livelli (filtro in lettura, interceptor in scrittura, analyzer sulle vie di fuga) + FK composite + R1-R7 con verificatori automatici | ✅ mitigato — ADR-0003 |
| Routing irrealizzabile o fuori budget | C2 | Spike time-boxed prima della Phase 3 | ⬜ decisione #3 |
| Duplicazione incontrollata tra slice | Vertical Slice senza soglia | Regola della terza ripetizione → `Common/` | ✅ regola scritta |
| Migrazioni divergenti dev/prod | I10 | Migration bundle, niente migrate all'avvio | ✅ regola scritta |
| Refactor totale della UI | I9 | Decisione #7 chiusa: codice shadcn copiato nel repo, costo di inversione **per-componente** e non globale. ⚠️ **Mitigazione condizionata**: vale solo se i verificatori R11 (correttivi anti-look applicati prima del primo componente), R12 (nessun colore letterale) e R13 (`components/ui/` senza dominio) sono **attivi in CI**. Senza di essi il rischio torna intero | 🟡 mitigato **condizionatamente** — ADR-0007 |
| La matrice di contrasto a due temi degrada in silenzio | ADR-0007 (dark mode in Phase 1) | R14: verificatore automatico sulle 47 coppie, bloccante in CI, più `axe` eseguito su entrambi i temi | ⬜ da implementare in Phase 0 |
| Il `Ribbon` è l'elemento identitario portante ma **non è validato** | ADR-0007 | Prototipo statico prima di costruirlo; fallback R25 sul solo asse temporale se la lettura del contachilometri non esiste nel modello | ⬜ aperto |
| Rework su dati personali | C4 | Topologia FK fissata prima della prima migration + R8/R9/R10 con verificatori automatici | ✅ mitigato — ADR-0004 |
| Modifica di una relazione padre-figlio che rompe silenziosamente la catena di erasure | ADR-0004 | Test di completezza R8/R9 + grafo di cancellazione documentato in `DATA.md` §6 | ✅ mitigato |
| Copia locale dei dati su dispositivo (PWA) che sopravvive alla cancellazione | ADR-0004 | Policy di purge del client, da definire **se** la #10 entra in scope | ⬜ decisione #10 |
| Sottostima del lavoro di upgrade ai tipi spaziali in Phase 3 | ADR-0001 | Trigger e percorso di migration documentati e pre-approvati nell'ADR-0001 | ✅ mitigato |
| **Rework sulla forma della chiave primaria** — il costo di inversione più alto del progetto: cambiarla dopo la prima migration significa riscrivere ogni FK composita | domain model §3.8 | Decisione chiusa **prima** della prima migration, con Piano B in tre passi se EF Core generasse le colonne `REFERENCES` in ordine diverso; R26-R33 con verificatori automatici | ✅ mitigato — ADR-0008 |
| **Test flaky per stato condiviso tra test** — un solo database con Respawn è la scelta più economica, ma rende lo stato condiviso il modo naturale di sbagliare | ADR-0009 (isolamento G1) | Collection xUnit già predisposte per salire a DB-per-collection in ~15 righe; **nessun retry** (R37): un test che passa al secondo tentativo nasconde una race invece di segnalarla | 🟡 mitigato **con via di fuga pronta** |
| **Un verificatore che non fallisce mai** — 25 regole con verificatore automatico, nessuna garanzia che verifichino davvero | ADR-0009 | **R38**: ogni verificatore va visto fallire almeno una volta su una violazione deliberata, prima di essere considerato attivo | ⬜ da applicare a ogni regola |

> **Nota di sequenza.** La decisione **#3** (provider geo) va presa **prima** che scatti il trigger 1 dell'ADR-0001 (provider che restituisce poligoni da persistire), non dopo.

> **Nota di sequenza 2.** Le decisioni **#6** (DB di test) e **#9** (target di deploy) erano state promosse di fatto a prerequisiti dell'implementazione dall'ADR-0003, pur restando MEDIUM. La **#6 è ora chiusa** ([`ADR-0009`](adr/0009-test-strategy.md)); la **#9** resta aperta perché il vincolo same-site va confermato prima di scrivere l'autenticazione, e perché ADR-0009 vi aggiunge un requisito: il target scelto deve poter eseguire migration da un **bundle** verificato in CI.
