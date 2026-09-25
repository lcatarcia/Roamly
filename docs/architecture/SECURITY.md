# Roamly — Sicurezza, ownership e privacy

Stato: §1-§3 **chiuse** da [`ADR-0003`](../adr/0003-auth-and-ownership.md) e [`ADR-0004`](../adr/0004-privacy-and-erasure.md) · §4-§5 aperte · Ultimo aggiornamento: 2026-09-24
Origine: `docs/archive/Roamly_Planning_2026-09-24.md` §22 — esteso secondo review C3 e C4
Owner: **@sentinel**, con **@archimedes** per l'ownership

---

## 1. Baseline

- HTTPS;
- **autenticazione: ASP.NET Core Identity con cookie `httpOnly` + `Secure` + `SameSite=Lax`** (`Strict` romperebbe il ritorno dai link di conferma email);
- endpoint di auth scritti come slice in `Features/Authentication/`, conformi a `API-CONVENTIONS.md` — **non** `MapIdentityApi`;
- **antiforgery** obbligatorio sulle richieste non-GET, più requisito di header custom (`X-Roamly-Request`) come seconda barriera;
- **CORS con allowlist esplicita** e `AllowCredentials`. Una configurazione CORS sbagliata riapre il CSRF **silenziosamente**: è il punto da presidiare;
- `SecurityStampValidationInterval` a **5 minuti** (revoca globale quasi immediata);
- password hashing gestito da ASP.NET Identity;
- secrets fuori dal repository, environment variables per la configurazione sensibile;
- validation dell'input;
- rate limiting dove necessario, con priorità su login e password reset;
- protezione degli upload (tipo, dimensione, scansione);
- aggiungere che export e cancellazione sono **azioni sensibili**: re-autenticazione con password e rate limit dedicato;
- logging senza dati sensibili, ma **con** eventi di sicurezza dedicati (`OwnershipViolationException`, `ownership_miss`), che vivono sulla **pipeline di logging strutturato** con retention configurata nel sink — nessuna tabella su database.

> ⚠️ **Dipendenza non risolta: provider email.** Conferma account e reset password non funzionano senza un provider SMTP e senza reputazione di dominio. Costo e deliverability non indagati. Tracciata in `OPEN-DECISIONS.md`.

---

## 2. Ownership — regola architetturale

> ✅ **Decisione #2 chiusa il 2026-09-24** → [`ADR-0003`](../adr/0003-auth-and-ownership.md). Chiude il finding C3.

Il dominio è `User → Camper → Trips`: **ogni singola query è per definizione owner-scoped**.

In Vertical Slice questa regola è particolarmente a rischio, perché ogni slice scrive la propria query. È esattamente lo scenario che produce **IDOR**: un `GET /api/v1/campers/{id}` che restituisce il camper di un altro utente.

### 2.1 Il punto in cui ci si illude

> **Il global query filter è un meccanismo di lettura. Non è un meccanismo di autorizzazione.**

| Percorso | Filtro applicato? | Conseguenza |
|---|---|---|
| Query LINQ, `Include`, lazy loading | ✅ sì | protetto |
| **`Find`/`FindAsync` con hit nel change tracker** | ❌ no | l'entità torna così com'è |
| **`Attach` / `Update` / `Remove` / `Entry().State`** | ❌ no | **scrittura su entità altrui possibile** |
| **`SaveChanges` su entità modificate** | ❌ no | il filtro non tocca l'UPDATE generato |
| **`ExecuteUpdate` / `ExecuteDelete`** | ❌ no | **cancellazione di massa di dati altrui possibile** |
| **`INSERT`** | ❌ non applicabile | se `OwnerId` arriva dal client, è forgiabile |

Il filtro rende sicuro il default del ~90% del codice (le letture). Il restante 10% — le scritture — richiede un meccanismo diverso. Fermarsi al filtro e chiamarlo "protezione IDOR" è precisamente l'errore che C3 teme.

### 2.2 La regola invariante — R1-R10

> Le regole **R11-R25** ([`ADR-0007`](../adr/0007-design-system.md)) riguardano il design system, ma due hanno rilevanza di sicurezza diretta e vanno lette insieme a §3: **R17** (il colore non è mai l'unico segnale di distruttività — in scala di grigi `danger` e `clay` sono indistinguibili) e **R23** (le date di cancellazione arrivano dal server, il client non le ricalcola).

| # | Regola | Verificata da |
|---|---|---|
| **R1** | Ogni entità persistente implementa `IOwnedResource` con `OwnerId`, o è in whitelist esplicita | test di convenzione sul modello EF (**bloccante**) |
| **R2** | Ogni entità owned porta il query filter nominato `"OwnerScope"` | test di convenzione sul modello EF |
| **R3** | Nessun insert/update/delete se l'`OwnerId` **originale** ≠ identità corrente; `OwnerId` assegnato dal server, mai bindato da un request model | `SaveChangesInterceptor` + test per slice |
| **R4** | L'`OwnerId` di un figlio non può divergere da quello del padre | **foreign key composite** nel database |
| **R5** | `Find`, `Attach`, `ExecuteUpdate`, `ExecuteDelete`, `FromSqlRaw`, `IgnoreQueryFilters` vietati fuori da `Common/Ownership/` | `BannedApiAnalyzers` (build) + test sulla whitelist delle deroghe |
| **R6** | Identità assente = **eccezione**, mai insieme vuoto | test su `ThrowingCurrentUser` |
| **R7** | Risorsa non posseduta indistinguibile da inesistente: `404` con `ProblemDetails` identico | test di ownership per slice |
| **R8** | **Copertura universale.** Ogni entità owned in `DbContext.Model` è raggiungibile sia dal grafo di **export** sia dal grafo di **cancellazione**; entrambi derivati dal modello a runtime, mai da liste letterali. Esclusione solo via whitelist motivata | test di convenzione sul modello EF (**bloccante**) |
| **R9** | **Cancellazione effettiva.** Dopo `AccountErasureJob`, nessuna riga con quel `OwnerId` sopravvive in alcuna tabella owned e nessuna FK resta pendente | integration test "erase and sweep" su **database reale** (dipende dalla #6) |
| **R10** | **Residuo non personale.** Sopravvive solo ciò che è non-owned e privo di identificatori diretti: `ErasureReceipt` (pseudonimizzata) e i log di sicurezza, con retention nel sink | test di convenzione sulla whitelist delle entità non-owned |

**Nessuno dei dieci verificatori dipende dalla memoria di chi scrive la slice.** È questo il criterio che ha guidato la scelta.

> ✅ **Precisazione di costo, dal 2026-09-25** ([`ADR-0009`](../adr/0009-test-strategy.md)): **R1, R2, R8 e R10 non richiedono alcun database.** Si verificano ispezionando `DbContext.Model`, che EF Core costruisce offline con `UseSqlServer` senza mai aprire una connessione: sono test **L0**, sotto i 5 secondi, eseguibili senza Docker.
> ❌ **Correzione**: la riga di R7 qui sopra poteva far credere che servisse un database. **Non serve.** R7 è un **contratto HTTP** — due risposte devono essere byte-identiche — e si verifica con `WebApplicationFactory`. Dei dieci verificatori, **solo R3 e R9 richiedono davvero SQL Server**.

R1-R7 vengono da [`ADR-0003`](../adr/0003-auth-and-ownership.md), R8-R10 da [`ADR-0004`](../adr/0004-privacy-and-erasure.md). Sono **un unico impianto**, non due elenchi: R8 riusa lo stesso loop su `DbContext.Model` che applica il filtro di R2.

### 2.3 I tre livelli

1. **Lettura** — `IOwnedResource` + loop in `OnModelCreating` → `HasQueryFilter("OwnerScope", ...)` con `ICurrentUser` come **campo di istanza** del DbContext (mai variabile catturata o statica).
2. **Scrittura** — `SaveChangesInterceptor`: assegna `OwnerId` sugli `Added`, confronta `entry.OriginalValues` sui `Modified`/`Deleted`, **vieta sempre** la modifica di `OwnerId`. Violazione → `500` + evento di sicurezza loggato, non `403`: se accade è un bug, non un utente cattivo.
3. **Vie di fuga** — `BannedApiAnalyzers` con errore di compilazione. Deroghe solo in `Common/Ownership/` con `#pragma`, **contate da un test** contro una whitelist dichiarata.

> L'escape hatch resta un'eccezione perché **è contata, e il contatore è un test**: aggiungere una deroga obbliga a modificare la whitelist, quindi l'atto è visibile nel diff invece di sparire dentro un handler.

### 2.4 `OwnerId` denormalizzato e FK composite

`OwnerId` sta su **ogni** entità, anche sui figli (`TripStop`, `Expense`, `MaintenanceItem`), ed è la **prima colonna** di ogni indice.

La divergenza tra figlio e padre — dato corrotto che nessun filtro rileverebbe — è resa **impossibile dal database**: indice unique su `Trip(Id, OwnerId)` e foreign key composita da `TripStop(TripId, OwnerId)`. La ridondanza smette di essere un rischio e diventa un vincolo.

> La stessa FK composita serve un **secondo** scopo, senza costo aggiuntivo: è l'arco su cui viaggia il `CASCADE` della cancellazione dell'account ([`ADR-0004`](../adr/0004-privacy-and-erasure.md)). Per contro, `OwnerId → AspNetUsers` è **sempre `ON DELETE NO ACTION`**: due percorsi di cascade verso la stessa tabella fanno fallire la migration con l'errore SQL Server **1785**. Vedi `DATA.md` §6.

Conseguenza positiva: `Expense` **smette di avere ownership polimorfa** ai fini della sicurezza. Resta polimorfa l'appartenenza (camper *o* trip), che è un problema di modellazione di dominio, non di sicurezza.

### 2.5 `ICurrentUser` fuori da HTTP

**Quattro** modi:

| Modo | Uso | Filtro |
|---|---|---|
| **HTTP** | middleware dai claim | attivo, sull'utente della richiesta |
| **`RunAsUser(userId, "motivo")`** | lavoro per conto di **un** utente identificato fuori da HTTP: `MaintenanceReminderJob`, `AccountErasureJob` | **attivo e pieno**, sull'utente impersonato |
| **`RunAsSystem("motivo")`** | lavoro non riconducibile a un utente: polling di `AspNetUsers`, seed, scrittura della `ErasureReceipt` | nessuna identità → le query owner-scoped **lanciano** |
| **Throwing** | design-time, migration | lancia sempre |

> ⚠️ **Correzione ad ADR-0003.** `RunAsSystem` **non attiva `IgnoreQueryFilters` nello scope**: quella descrizione era tecnicamente falsa, perché `IgnoreQueryFilters` è un operatore *per-query*, non uno stato ambientale. `RunAsSystem` dichiara un'identità di tipo sistema; l'unico modo di allentare il filtro resta una chiamata esplicita a `IgnoreQueryFilters(["OwnerScope"])` dentro `Common/Ownership/`, sotto deroga `#pragma` contata dalla whitelist di R5.
>
> È stata **respinta** la variante "predicato con disgiunzione" (`e => _currentUser.IsSystem || e.OwnerId == ...`): è **fail-open**, globale, e soprattutto **invisibile nel diff** — cioè non contabile, mentre l'intero impianto si regge sul fatto che le eccezioni siano contate. Vedi [`ADR-0004`](../adr/0004-privacy-and-erasure.md) §Emendamento.

`RunAsUser` capovolge il rischio nei job: un bug produce una cancellazione **incompleta** — che R9 rileva — non una cancellazione dei dati di qualcun altro. Un job di cancellazione è l'ultimo posto in cui si vuole un'identità con accesso a tutto.

**Fail-closed:** identità assente non significa "nessun proprietario, zero righe". Deve lanciare. Le zero righe silenziose producono bug diagnosticati male e abituano a disattivare il filtro.

### 2.6 Il confine della regola

> ⚠️ **R1-R10 si fermano al confine del database.**
>
> L'upload di file **non è coperto**: un URL firmato mal generato bypassa l'intero impianto, perché lo storage non conosce `OwnerId`. Lo stesso vale per la cancellazione: R9 verifica che nessuna **riga** sopravviva, non che nessun **file** sopravviva.
> Oltre quel confine valgono i requisiti **F1-F7** (§5), che sono requisiti di accettazione della **decisione #8**, non verificatori automatici.
> Va detto esplicitamente, altrimenti si crede di essere coperti e non lo si è.

---

## 3. Privacy ed erasure

> ✅ **Decisione #4 chiusa il 2026-09-24** → [`ADR-0004`](../adr/0004-privacy-and-erasure.md). Chiude il finding C4.

> 🔴 **Gap aperto scoperto da ADR-0007 — da quale interfaccia si revoca la cancellazione.**
> ADR-0004 rende l'account **immediatamente inaccessibile** alla richiesta di cancellazione, ma concede **30 giorni** di ripensamento. Le due cose insieme non stanno in piedi: se l'utente non può più autenticarsi, la revoca non ha una porta d'ingresso.
> Le opzioni sul tavolo sono un **login limitato** che conduce a una sola pagina ("revoca la cancellazione" oppure "esci"), un **link firmato** inviato via email al momento della richiesta, o il solo contatto umano. Ognuna ha implicazioni di sicurezza diverse: la prima riapre una superficie di autenticazione su un account in cancellazione, la seconda crea un token a lunga vita in una casella di posta.
> **Owner: @sentinel + @hermes.** Non è una scelta di UX. **Blocca il rilascio** della schermata "Elimina account", non il suo disegno.

Roamly tratta: **posizione geografica storica, foto, documenti del veicolo, spese, abitudini di viaggio**, e costruisce un **profilo di preferenze inferito** (`FEATURES.md` §9).

Senza giri di parole: è un **profilo comportamentale geolocalizzato**.

### 3.1 La riformulazione che ha deciso tutto

> **La parte irreversibile non è il codice di export e cancellazione. È la topologia delle foreign key.**

Il codice si scrive quando serve. La topologia va fissata **prima della prima migration**, perché cambiarla dopo significa droppare e ricreare ogni constraint dello schema su dati reali.

### 3.2 Cosa entra nell'MVP

| Elemento | Forma |
|---|---|
| **Topologia FK** | `OwnerId → AspNetUsers` sempre **`NO ACTION`**; cascade **solo** lungo la gerarchia di dominio, sulle FK composite di R4; **una sola cascade path** per coppia di tabelle. Vedi `DATA.md` §6 |
| **`CreatedAtUtc`** | obbligatorio, NOT NULL, UTC, su **ogni** entità owned. Aggiungerlo dopo costa un backfill su dati per cui il valore vero non esiste più |
| **Export** | `GET /api/v1/me/export` → **ZIP** con un JSON per tipo di entità + `manifest.json`. L'insieme delle entità è **derivato da `DbContext.Model` a runtime** |
| **Cancellazione** | `POST /api/v1/me/deletion` → **hard delete** con **30 giorni** di grazia, account immediatamente inaccessibile, revocabile con `DELETE`. `AccountErasureJob` cancella in ordine topologico **calcolato dal modello** |
| **`ErasureReceipt`** | entità **non owned**, senza FK verso `AspNetUsers`, con `SubjectHash` pseudonimizzato. Emessa **solo** dopo conferma dello sweep (F7) |
| **Campi su `User`** | `DeletionRequestedAtUtc`, `DeletionScheduledForUtc` (persistita, non calcolata), `PrivacyPolicyVersionSeen` |
| **Regole** | **R8, R9, R10** in §2.2 |

Gli endpoint di export e cancellazione **non sono nell'MVP 1.0 stretto** (utente singolo): sono **criterio di uscita della Phase 1**, prima del primo utente reale. Ciò che entra nell'MVP è lo **schema**.

### 3.3 Conseguenza operativa da non dimenticare

`NO ACTION` significa che `DELETE FROM AspNetUsers WHERE Id = @x` **fallisce** finché esistono righe owned. La cancellazione di un account non è una `DELETE`: è una sequenza ordinata. `AccountErasureJob` è quindi un **requisito**, non una comodità.

### 3.4 Cosa NON entra nell'MVP, e cosa lo renderebbe obbligatorio

| Non si fa | Trigger |
|---|---|
| Tabella dei consensi | primo servizio terzo oltre al provider email |
| Tabella `SecurityEvent` su DB e job di purge | serve interrogare gli eventi di sicurezza dall'applicazione |
| DPIA · ROPA formale | numero di utenti non banale |
| `StoredFile` e storage | upload in Phase 1 → F1-F7 bloccanti, #8 da chiudere |
| Anonimizzazione al posto della cancellazione | esistono dati aggregati che si vuole preservare |
| Audit trail delle modifiche | backoffice o secondo operatore |
| **Soft delete globale** | **mai**, respinto nel merito. Resta ammesso il soft delete *locale* a una singola entità, con filtro nominato |

### 3.5 Base giuridica e retention

> ⚠️ **Non è consulenza legale.** Le posizioni qui sotto sono difendibili, non certificate. Dettaglio e caveat in [`ADR-0004`](../adr/0004-privacy-and-erasure.md).

Il principio di `VISION.md` §2.6 ("trasparente, modificabile e sotto il controllo dell'utente") è ora tradotto in **tre verificatori automatici** (R8, R9, R10) invece che in un proposito. L'unica parte non verificabile da un test è la **retention del sink di logging**, che è configurazione di deploy: dichiarata come debolezza, non nascosta (decisione #9, @vulcan).

**Informativa**: contenuto statico versionato, da pubblicare prima del primo utente reale. `PrivacyPolicyVersionSeen` traccia quale testo l'utente ha visto — **non è un consenso**.

---

## 4. Confine dei dati verso l'AI

Cosa viene inviato a un modello di terze parti è un trattamento di dati personali.

Requisiti:

- dichiarare **esplicitamente** quali campi lasciano il sistema;
- nessun invio di documenti del veicolo, foto o posizione precisa senza base giuridica e consenso;
- possibilità di usare Roamly con l'Intelligence disattivata (feature flag, vedi `FEATURES.md` §8);
- provider, costo per richiesta e latenza attesa da dichiarare (I11).

> **Stato reale oggi:** questo confine è **teorico**. Nessun servizio terzo è previsto prima della Phase 4, oltre al provider email (che serve all'esecuzione del contratto, non è una scelta dell'utente). Il **primo invio effettivo di dati a un modello di terze parti attiva il trigger 1** di [`ADR-0004`](../adr/0004-privacy-and-erasure.md): da quel momento serve una tabella dei consensi.
>
> Attenzione: **feature flag e consenso sono assi ortogonali.** Il flag dice se la funzionalità esiste; il consenso dice se quell'utente ha autorizzato il trasferimento. La condizione effettiva è `flag ∧ consenso`.

---

## 5. Upload di file

> ⚠️ **Decisione aperta #8 (storage file), severità MEDIUM. Owner: @vulcan.**

`Document` e `Photos` implicano upload, storage, thumbnail, quota, scansione antimalware e URL firmati. Lo stack non nominava alcuno storage.

Le foto di viaggio **non sono più il cuore dell'esperienza premium**: ADR-0007 ha spostato l'identità visiva su elementi che non dipendono dalla fotografia (il `Ribbon`, il dato numerico come superficie primaria, sand e contour generati in CSS), e ha dichiarato la fotografia **assente fino alla Phase 2**. Questo **abbassa l'urgenza** della #8, non la sua importanza: restano interamente validi i requisiti F1-F7 e il controllo di ownership sugli URL firmati.

> **R19 — nessuna CDN di terzi**, da [`ADR-0007`](../adr/0007-design-system.md). Font e asset sono **self-hosted**. Il motivo è di privacy, non di performance: una richiesta a una CDN esterna espone l'indirizzo IP di ogni utente a un terzo, a ogni caricamento di pagina, senza base giuridica dichiarata nell'informativa. **Verificato in CI.**

Opzione di partenza: filesystem locale in dev, Azure Blob / S3-compatible in prod, con URL firmati e mai serving diretto dall'API.

> **Vincolo derivato da [`ADR-0003`](../adr/0003-auth-and-ownership.md):** la decisione #8 **deve** includere il controllo di ownership sugli URL firmati. Le regole R1-R7 non arrivano allo storage.

### 5.1 Requisiti di accettazione F1-F7 (da [`ADR-0004`](../adr/0004-privacy-and-erasure.md))

> `StoredFile` **non è nello schema MVP**: i Documenti del camper sono fuori dalla Phase 1. F1-F7 sono dichiarati **adesso** perché il momento in cui verrebbero ignorati è precisamente quello in cui si scriverà il codice di upload senza averli davanti.

| # | Vincolo |
|---|---|
| **F1** | **Chiavi owner-prefixed**: `{ownerId}/{tipoEntità}/{entityId}/{fileId}`. La cancellazione dev'essere esprimibile come **prefix-delete**, non come scansione del bucket |
| **F2** | **Riconciliazione bidirezionale**: un verificatore che rilevi sia oggetti senza riga sia righe senza oggetto. La coerenza tra due persistenze senza transazione comune o è verificata, o è sperata |
| **F3** | **Export incluso**: i binari entrano nello stesso ZIP, oppure l'export dichiara URL a tempo con scadenza nel manifest. Non due meccanismi diversi |
| **F4** | ⚠️ **Hard delete reale lato provider** — requisito di *verifica*, non affermazione: soft delete, versioning e recycle bin variano per provider e sono spesso attivi per default. La #8 deve verificarlo e documentarlo |
| **F5** | ⚠️ **Backup e retention dichiarati**: i backup sopravvivono alla cancellazione. La finestra va **misurata** e allineata alla grazia |
| **F6** | **URL firmati owner-checked**, scadenza ≪ del periodo di grazia, mai serving diretto dall'API |
| **F7** | **Cancellazione idempotente e osservabile**: la `ErasureReceipt` **non viene emessa** finché lo sweep dello storage non è confermato |
