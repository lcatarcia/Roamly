# Roamly — Decisioni aperte

Stato: attivo · Ultimo aggiornamento: 2026-10-20
Origine: review `docs/archive/Roamly_Planning_Review_2026-09-24.md` §6

Ogni decisione va trattata **una alla volta** con il formato **Decision Required** dell'orchestratore (opzioni + raccomandazione + tradeoff), come richiesto dalla HUMAN-IN-THE-LOOP POLICY.

Quando una decisione è chiusa: si scrive l'ADR corrispondente in `docs/adr/`, si aggiorna il documento di riferimento e si segna ✅ qui.

---

## Bloccanti — nessuna implementazione prima di queste

> **Gate alle decisioni iniziali superato; checkpoint infrastrutturali 1-5 chiusi.** La **#3** (provider geo) resta aperta per scelta dell'utente: è differita insieme alla Phase 3, non blocca l'MVP 1.0 e blocca Maps & Places finché non saranno completati spike e ADR. I prerequisiti originari — semantica di `Place`, modellazione di `Expense`, chiave primaria e infrastruttura di test — sono chiusi. Per la roadmap prodotto aggiornata, vedere `docs/product/ROADMAP.md`.

| # | Decisione | Severità | Owner | Documento | Stato |
|---|---|---|---|---|---|
| 1 | Conferma SQL Server + libreria spatial — chiude C1 | HIGH | @oracle | [`adr/0001-use-sql-server.md`](adr/0001-use-sql-server.md) | ✅ **chiusa 2026-09-24** |
| 2 | Modello di autenticazione e autorizzazione owner-scoped — chiude C3 | HIGH | @sentinel | [`adr/0003-auth-and-ownership.md`](adr/0003-auth-and-ownership.md) | ✅ **chiusa 2026-09-24** |
| 3 | Provider dati geografici e routing — chiude C2 | HIGH | @archimedes | `product/FEATURES.md` §4 | ⏸ **differita 2026-10-20** — nessun provider selezionato |
| 4 | Requisiti privacy/GDPR minimi per l'MVP — chiude C4 | HIGH | @sentinel | [`adr/0004-privacy-and-erasure.md`](adr/0004-privacy-and-erasure.md) | ✅ **chiusa 2026-09-24** |

> **Esito #1:** SQL Server confermato; **NetTopologySuite e tipi spaziali rimandati**, coordinate come `decimal` WGS84 in **complex type** `Coordinates`, upgrade a `geography` pre-approvato su 4 trigger documentati. *Emendato il 2026-09-24 dal domain model: complex type invece di owned type, perché un value object non deve comparire nei verificatori di ownership ed erasure.*

> **Esito #2:** ASP.NET Core Identity con **cookie httpOnly** (endpoint come slice, non `MapIdentityApi`); ownership a **tre livelli** — query filter nominato `"OwnerScope"` in lettura, `SaveChangesInterceptor` in scrittura, `BannedApiAnalyzers` sulle vie di fuga; `OwnerId` denormalizzato su ogni entità con **FK composite**; **`404` uniforme**. Regole invarianti **R1-R7**, ognuna con un verificatore automatico. *Emendato da ADR-0004: due correzioni tecniche e `RunAsUser` come quarto modo di `ICurrentUser`.*

### Vincoli derivati dalla #2 su decisioni ancora aperte

| Decisione | Vincolo |
|---|---|
| **#9** deploy | ✅ **same-site implementato localmente**: bundle SPA al root della stessa app e API sotto `/api`; i cookie restano `Secure` + `SameSite=Lax`. Domini separati → Parte A dell'ADR-0003 da rinegoziare. Serve inoltre un **key ring di Data Protection** condiviso e persistito |
| **#8** storage file | ✅ chiusa il 2026-10-09 → [`ADR-0010`](adr/0010-journal-image-storage.md). Il controllo owner-scoped precede ogni URL firmato; R1-R7 si fermano al database |
| **#6** DB di test | ✅ **chiusa** → [`ADR-0009`](adr/0009-test-strategy.md). ❌ **La formulazione originale era sbagliata**: diceva «R3/R4/R7 verificabili solo con un database reale». **R7 è un contratto HTTP** (`WebApplicationFactory`, nessun DB) e **R4 è in gran parte convenzione** sul modello. Dei dieci verificatori, **solo R3 e R9 richiedono davvero SQL Server** |

> **Esito #4:** la parte irreversibile è la **topologia delle FK**, non il codice. `OwnerId → AspNetUsers` sempre **`NO ACTION`** (errore 1785), cascade solo gerarchico sulle FK composite già esistenti, **una sola cascade path** per coppia di tabelle, **`CreatedAtUtc` obbligatorio**. Export **ZIP model-driven**, **hard delete** con **30 giorni** di grazia, `ErasureReceipt` non owned. Regole **R8-R10**. Vincoli **F1-F7** dichiarati per la #8. Emendamento ad ADR-0003: `RunAsUser` come quarto modo di `ICurrentUser` e due correzioni tecniche.

### Vincoli derivati dalla #4 su decisioni ancora aperte

| Decisione | Vincolo |
|---|---|
| **#8** storage file | ✅ **Strategia scelta in ADR-0010.** La configurazione effettiva è stata verificata prima del test cloud del 2026-10-20; il test d'accettazione API ha coperto upload, download SAS, export, delete/outbox ed erasure su owner sintetico. La prova non sostituisce la verifica di versioning, retention e backup; F7 richiede sweep confermato prima della ricevuta |
| **#9** deploy | scheduler affidabile per `AccountErasureJob`; **health check** su richieste scadute non processate; **retention del sink di logging**; finestra di **backup** da misurare (F5); ⚠️ hosting fuori dallo SEE → trigger |
| **#6** DB di test | 🔴 anche **R9** richiede un database reale; e i **test data builder devono coprire ogni entità owned**, altrimenti "erase and sweep" è parziale |
| **#10** offline/PWA | ✅ decisione chiusa il 2026-10-21: nessuna modalità offline, PWA installabile o cache applicativa dei dati utente nella v1; UX esplicita quando manca la rete. Se una futura decisione riapre l'offline, serviranno policy di purge al logout/cancellazione e informativa sulla copia locale |
| **#3** provider geo | inviare **posizioni dell'utente** (non query generiche) a un provider esterno è un trasferimento a terzi → attiva il trigger della tabella consensi. La distinzione tra i due casi va fatta **nella #3** |
| **#7** design system | due schermate obbligatorie — "Scarica i miei dati" ed "Elimina account" — con conferma inequivocabile su **30 giorni di grazia e irreversibilità dopo**. Una conferma ambigua su un'azione irreversibile è un problema di sicurezza, non di UX |

> **Esito #8 (2026-10-09):** immagini solo sul Journal, come `JournalEntryAttachment`; Azure Blob Standard LRS Hot in Italy North, container privato e accesso del backend tramite managed identity. L'upload è mediato dall'API; i download usano URL firmati read-only di 5 minuti, emessi dopo verifica owner-scoped. Limiti: 10 MiB per immagine, 10 per voce; JPEG, PNG e WebP. In locale/test si usa Azurite. Per semplicità sono stati accettati due rischi espliciti: nessuna scansione antimalware e conservazione degli EXIF originali, incluse eventuali coordinate GPS. Non si mantengono versioni, soft delete o backup Blob recuperabili; la configurazione effettiva va verificata separatamente. Le immagini entrano nello ZIP di export; l'erasure esegue prefix sweep e non emette `ErasureReceipt` se lo sweep fallisce. L'ADR non autorizza provisioning né deployment.
>
> **Stato operativo (2026-10-20):** l'utente ha autorizzato separatamente il provisioning dello storage account `storoamlywse7wit7fzpja` e del container privato `roamly-journal-images`; le proprietà effettive dell'account e del Blob service sono state verificate. Il test d'accettazione API Azure con dati sintetici è passato; il cleanup ha lasciato zero blob e le assegnazioni RBAC temporanee sono state revocate. Nessun dato personale è stato caricato e l'app non è stata pubblicata.

> La #3 non blocca l'MVP 1.0 **solo** perché il Trip Planner v1 è dichiarato non vehicle-aware (`FEATURES.md` §3). Blocca la Phase 3 e richiede uno spike time-boxed di max 3 giorni.

> **Perimetro deciso della #9 (2026-10-06, aggiornato 2026-10-07):** Azure Container Apps Consumption, API/job scale-to-zero, Azure SQL Serverless, Italy North; cold-start accettati. È previsto un solo deployment Azure, usato per il servizio reale con `ASPNETCORE_ENVIRONMENT=Production`; lo sviluppo locale resta `Development`. `AccountErasureJob` usa lo stesso eseguibile/API image, `--job account-erasure`, one-shot con scheduler ACA esterno (nessun `HostedService` always-on). Il key ring è approvato nella tabella `DataProtectionKeys` dello stesso Azure SQL, con TDE gestita da Microsoft, ed è implementato via `PersistKeysToDbContext` con application name stabile `Roamly`; la migration è `AddDataProtectionKeys`. I backup automatici sono approvati con PITR di 7 giorni, senza LTR, e storage LRS (meno costoso, senza geo-restore); dopo un restore vanno rieseguite le erasure successive al punto ripristinato. **L'utente ha scelto ingress pubblico ACA diretto, senza WAF/gateway gestito, con accesso tramite login Roamly**: l'endpoint sarà raggiungibile da Internet, ma la registrazione Production resta chiusa. Il costo e la complessità edge sono evitati accettando il rischio residuo; il limiter applicativo è implementato. La mitigazione dell'enumerazione account tramite hash fittizio e risposta uniforme è implementata con test deterministici e resta in attesa di riconferma Janus. L'utente ha scelto **contatori condivisi in Azure SQL** per il rate limiting, con **finestre fisse** e profilo **Balanced**: Identity 10 fallimenti/lockout di 5 minuti, 10 credenziali errate per account/15 minuti solo per utenti esistenti, 30 richieste per IP/5 minuti e 100/ora. In caso di contatore SQL non verificabile o IP ACA mancante/non valido, il login fallisce chiuso con 503 e allerta, senza fallback locale; per l'IP si usa solo il valore valido più a destra di `X-Forwarded-For` fornito dall'ingress ACA, ignorando i valori precedenti. La pulizia giornaliera dei contatori è stata approvata riusando il job ACA one-shot `account-erasure`; il cap scelto è 10.000 IP attivi, con 503 e allerta se raggiunto; la cadenza di pulizia è giornaliera. Il limite account risponde con `401` generico come gli username inesistenti; il `429` è riservato al limite IP. Per l'uso personale l'utente ha approvato Azure SQL Serverless General Purpose Gen5 con minimo **0,5 vCore**, massimo **1 vCore**, auto-pause dopo **15 minuti** e dimensione massima dati **1 GB**; General Purpose fattura il massimo storage configurato e aggiunge il 30% per il log, quindi va configurato un alert di capacità e rivalutato il limite prima di memorizzare file binari nel database. La chiave HMAC runtime resta provider-agnostic nel codice e sarà letta da Azure Key Vault via managed identity ACA, senza accesso OIDC ai valori. **Strategia IaC scelta dopo consultazione Vulcan: Azure Bicep + GitHub Actions**, con autenticazione federata OIDC e ambiente Production protetto da approvazione; la strategia non ha creato workflow né risorse e non autorizza un deploy. Prima di creare un secondo ambiente Azure vanno isolati database o discriminator. Queste sono decisioni e implementazione locale, **non un deploy effettuato**. Restano aperti/da configurare: IaC e workflow, Key Vault e managed identity, risorse ACA/Azure SQL, verifica finale Janus, alert effettivi (incluso lo spazio SQL), rilascio controllato delle migration (applicare `AddDataProtectionKeys` e `AddLoginAttemptCounters` prima del runtime), verifica TDE e permessi SQL minimi, configurazione dei backup LRS (sovrascrivere il default GRS), retention dei log e soglia/configurazione dell'allerta sulle cancellazioni scadute non processate.

---

## Da chiudere prima della Phase 1

> **Aggiornamento #9 (2026-10-07):** Janus ha approvato i gate d'implementazione IP-first e retry SQL limitato all'apertura. L'utente accetta il rischio residuo di timing dell'enumerazione account per l'uso personale, con un solo account provisionato e registrazione Production chiusa. Questa decisione supera la precedente nota di riconferma Janus ancora aperta; non approva il deployment. Rivalutare prima di consentire altri account o registrazione pubblica. Per contenere i costi, l'utente ha scelto Azure SQL Serverless General Purpose Gen5, minimo 0,5 vCore, massimo 1 vCore e auto-pause a 15 minuti, con dimensione massima dati di 1 GB; il massimo storage configurato determina l'addebito e al log si aggiunge il 30%. Il retry SqlClient è limitato all'apertura, senza retry su query o transazioni. La suite completa aggiornata è passata: 195/195 test.

> **Topologia frontend/API approvata e implementata localmente (2026-10-07):** una sola app ASP.NET Core serve il build React/Vite sul root, le API restano sotto `/api`, e le route SPA dirette ricevono `index.html`; i percorsi API sconosciuti restano 404 e non ricevono HTML. Il container multi-stage unisce build frontend e publish API. I cookie restano `Secure` + `SameSite=Lax`. Questa è implementazione locale verificabile, **non** provisioning Azure né deployment; non sono stati aggiunti workflow di rilascio o parametri Azure.
>
> ✅ **Politica immagini Docker approvata (2026-10-07):** mantenere i tag mobili Node 22 e .NET 10, con rebuild e rilascio almeno mensili e prima per avvisi di sicurezza rilevanti. I tag mobili non rendono riproducibili i build nel tempo e non aggiornano un'immagine già in esecuzione; la cadenza resta manuale finché la pipeline release non sarà configurata. `global.json` resta pinning locale; lo stage Docker usa l'SDK corrente dal tag `10.0` perché il pin `10.0.204` non compare nei feed ufficiali consultati.

| # | Decisione | Severità | Owner | Documento | Stato |
|---|---|---|---|---|---|
| 5 | MediatR sì/no in Vertical Slice (tecnico + licenza) | MEDIUM | @archimedes | `architecture/ARCHITECTURE.md` §1 e §4 | ✅ **chiusa 2026-09-28** — nessun mediator, handler diretti + endpoint filter, vincolata a **R44** |
| 6 | Strategia integration test e provisioning DB di test | MEDIUM | @argus | [`adr/0009-test-strategy.md`](adr/0009-test-strategy.md) | ✅ **chiusa 2026-09-25** |
| 7 | Base del design system: headless + token vs da zero | MEDIUM | @pixel | [`adr/0007-design-system.md`](adr/0007-design-system.md) | ✅ **chiusa 2026-09-24** |
| 8 | Storage file/foto | MEDIUM | @vulcan | [`adr/0010-journal-image-storage.md`](adr/0010-journal-image-storage.md) | ✅ **chiusa 2026-10-09; provisioning e verifica completati, test d'accettazione Azure superato 2026-10-20** |
| 9 | Target di deploy, strategia migration e configurazione operativa | MEDIUM | @vulcan | `architecture/DEVOPS.md` §2.2 | ◐ **target, confine di accesso e strategia IaC scelti 2026-10-06; topologia same-site implementata localmente 2026-10-07; provisioning/configurazione e deploy ancora aperti** |
| 10 | Offline/PWA: dentro o fuori scope | MEDIUM | @archimedes | `product/UX.md` §5 | ✅ **chiusa 2026-10-21** — niente modalità offline, PWA installabile o cache applicativa dei dati utente nella v1; stato offline esplicito |

> **Esito #5:** **nessun mediator.** Le slice espongono **handler diretti** — classi semplici risolte da DI e invocate dall'endpoint — e i concern trasversali vivono negli **endpoint filter**. Il motivo dominante non è la licenza di MediatR (v14.2.0, RPL-1.5 o commerciale) ma il fatto che un comando **senza handler registrato compila verde**; e che mappando le otto voci di `API-CONVENTIONS.md` §2 **cinque sono concern HTTP**, irraggiungibili da un mediator senza portarsi un header dentro il command, mentre le altre due vivono nell'handler. **I behavior di dominio richiesti da ADR-0005 sono zero** (verificato voce per voce istruendo quell'ADR). La decisione è **vincolata a R44** (`ARCHITECTURE.md` §4), il verificatore L0 che rende bloccante l'handler non registrato: senza R44 questa opzione sarebbe la peggiore fra quelle valutate, non la migliore. È stata presa subito **perché invertirla costa poco** (🟢 verso `martinothamar/Mediator`, ~10 min per slice), non perché rimandarla fosse caro.

### Vincoli derivati dalla #5 su decisioni ancora aperte

| Decisione | Vincolo |
|---|---|
| **#9** target di deploy | la #5 **rimuove un ostacolo** anziché aggiungerne uno: senza MediatR, che è ostile ad AOT per via della risoluzione a runtime, la pubblicazione **Native AOT** dell'API resta praticabile. Se la #9 la considerasse, va verificato che EF Core e il resto dello stack la sostengano — ma la forma degli handler non la impedisce |

> **Esito #7:** **Tailwind v4 + shadcn/ui come generatore + token proprietari + componenti di dominio custom.** L'identità dell'MVP non poggia su fotografia e mappe — che non esistono prima della Phase 2-3 — ma su tre elementi realizzabili subito: il **Ribbon** (la route come asse astratto: in Phase 1 è l'asse km/tempo della manutenzione, in Phase 3 diventa la route geografica), il **dato numerico come superficie primaria** al posto della fotografia, e **sand, contour e texture generati in CSS/SVG**. Regole invarianti **R11-R25**, ognuna con un verificatore. **Dark mode in Phase 1** per scelta esplicita dell'utente. Palette corretta per accessibilità: il CTA del documento di input (clay + bianco, 3.20:1) **non passava WCAG AA** ed è stato sostituito da `clay-500` + `ink` (5.03:1); introdotto un token **`danger` separato da `clay`**.

### Vincoli derivati dalla #7 su decisioni ancora aperte

| Decisione | Vincolo |
|---|---|
| **#8** storage file | ✅ le foto del Journal entrano in Phase 2 secondo ADR-0010 e si aggiungono al linguaggio visivo senza sostituire `Ribbon` e dato numerico |
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
| **Provider email** (conferma account, reset password): scelta, costo e deliverability ancora aperti. Postura temporanea approvata: registrazione solo in Development, provisioning locale controllato con `--job provision-user`, account provisionato non confermato ma autorizzato al login; niente reset/recupero self-service. Non riaprire la registrazione Production. Alla configurazione del provider, implementare invio/verifica e reset reali, riattivare `RequireConfirmedEmail` e rimuovere l'eccezione per account non confermati. Owner: @vulcan | `architecture/SECURITY.md` §1 | ⬜ provider non scelto |
| **Retention del sink di logging** — nuovo da ADR-0004: unico requisito dell'impianto privacy **non verificabile da un test**. Scelta: Log Analytics Analytics, 30 giorni configurati; workspace/sink non ancora configurati. Owner: @vulcan | `architecture/DEVOPS.md` §2.3 | ◐ |
| **Informativa privacy** da scrivere e pubblicare prima del primo utente reale | `architecture/SECURITY.md` §3.5 | ⬜ |
| **Health check** su richieste di cancellazione scadute non processate | `architecture/DEVOPS.md` §5 | ⬜ |
| Convenzioni API complete | `architecture/API-CONVENTIONS.md` §2 | ⬜ |
| **Revoca della cancellazione account** — la decisione e l'interfaccia sono chiuse: la sessione limitata può revocare tramite `DELETE /api/v1/me/deletion` (`SECURITY.md` §3; UI in `frontend/roamly-web/src/features/privacy/PrivacyPage.tsx`). I test HTTP coprono il flusso. Scheduler e allerta dell'erasure restano gate operativi del deploy #9. | `architecture/SECURITY.md` §3 | ✅ **chiusa e implementata** |
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
| **Rework sulla forma della chiave primaria** — il costo di inversione più alto del progetto: cambiarla dopo la prima migration significa riscrivere ogni FK composita | domain model §3.8 | Decisione chiusa **prima** della prima migration, con Piano B in tre passi se EF Core generasse le colonne `REFERENCES` in ordine diverso; R26-R29 e R40-R43 con verificatori automatici | ✅ mitigato — ADR-0008 |
| **Test flaky per stato condiviso tra test** — un solo database con Respawn è la scelta più economica, ma rende lo stato condiviso il modo naturale di sbagliare | ADR-0009 (isolamento G1) | Collection xUnit già predisposte per salire a DB-per-collection in ~15 righe; **nessun retry** (R37): un test che passa al secondo tentativo nasconde una race invece di segnalarla | 🟡 mitigato **con via di fuga pronta** |
| **Un verificatore che non fallisce mai** — 25 regole con verificatore automatico, nessuna garanzia che verifichino davvero | ADR-0009 | **R38**: ogni verificatore va visto fallire almeno una volta su una violazione deliberata, prima di essere considerato attivo | ⬜ da applicare a ogni regola |

> **Nota di sequenza.** La decisione **#3** (provider geo) va presa **prima** che scatti il trigger 1 dell'ADR-0001 (provider che restituisce poligoni da persistire), non dopo.

> **Nota di sequenza 2.** Le decisioni **#6** (DB di test) e **#9** (target di deploy) erano state promosse di fatto a prerequisiti dell'implementazione dall'ADR-0003, pur restando MEDIUM. La **#6 è ora chiusa** ([`ADR-0009`](adr/0009-test-strategy.md)); per la **#9** il vincolo same-site è stato confermato e implementato localmente il 2026-10-07. Restano aperti i gate di provisioning/rilascio e il requisito di eseguire le migration da un **bundle** verificato in CI prima del runtime.
