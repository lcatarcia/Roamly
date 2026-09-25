# ADR-0003 — Autenticazione a cookie e ownership owner-scoped verificata dalla macchina

- **Stato:** Accettato
- **Data:** 2026-09-24
- **Owner:** @sentinel
- **Decisore:** utente
- **Severità:** HIGH
- **Chiude:** decisione #2 · finding C3 della review
- **Emendato da:** [`ADR-0004`](0004-privacy-and-erasure.md) §Emendamento — **due correzioni tecniche e un'aggiunta**

> ⚠️ **Leggere insieme all'emendamento.** Questo ADR contiene due errori tecnici, corretti in ADR-0004 e non modificati qui perché un ADR accettato non si riscrive:
> 1. `IgnoreQueryFilters("OwnerScope")` **non compila** — la forma corretta è `IgnoreQueryFilters(["OwnerScope"])`;
> 2. la descrizione di `RunAsSystem` come "attiva `IgnoreQueryFilters` solo dentro quello scope" è **falsa**: `IgnoreQueryFilters` è un operatore per-query, non uno stato ambientale.
>
> Aggiunta: `ICurrentUser` acquisisce un **quarto** modo, `RunAsUser(userId, "motivo")`. Le regole invarianti passano da **R1-R7** a **R1-R10**.

---

## Contesto

SPA React separata, API ASP.NET Core 10, EF Core, SQL Server, **Vertical Slice Architecture**, sviluppatore singolo senza review esterna. MVP 1.0: Login → Create Camper → Camper Dashboard → Add Maintenance → Maintenance reminder.

Due problemi distinti convergono in questa decisione.

**Autenticazione.** Roamly è quasi interamente contenuto **generato dall'utente e ri-visualizzato**: journal, note, didascalie foto, nomi di luoghi. La superficie XSS è strutturalmente presente, non ipotetica.

**Ownership.** Il dominio è `User → Camper → Trips`: ogni query è owner-scoped per definizione. In Vertical Slice **ogni slice scrive la propria query**, ed è esattamente lo scenario che produce IDOR su ~60-80 handler.

### Fatti verificati e non verificati

| Fatto | Esito |
|---|---|
| EF Core 10 introduce i **named query filters**: più filtri per entità, disattivabili singolarmente con `IgnoreQueryFilters("Nome")` | ✅ verificato |
| Il query filter **non** si applica a `Attach`/`Update`/`Remove` né al change tracker | ✅ verificato |
| Il query filter **non** si applica a `ExecuteUpdate`/`ExecuteDelete` | ✅ verificato (Microsoft Learn) |
| `MapIdentityApi` è stabile e usabile in produzione; emette **token opachi, non JWT**; `/refresh` ruota il token | ✅ verificato |
| `MapIdentityApi`: revoca solo via **security stamp**; **nessuna reuse/family detection**; nessun supporto a provider esterni; endpoint poco personalizzabili | ✅ verificato |
| Entra External ID: ~50.000 MAU gratis, poi ~$0,03/MAU | ⚠️ da riconfermare sulla pricing page |
| Tier gratuiti Auth0 / Clerk / Supabase Auth | ❌ **non verificati** — fonti aggregatrici, dati discordanti |
| `Find`/`FindAsync`: su hit nel change tracker il filtro è **certamente** bypassato; sul fallthrough a DB le fonti sono discordanti | ❌ **non verificato** — irrilevante ai fini della decisione: R5 lo vieta comunque |
| `BannedApiAnalyzers` può bandire extension method generici come `IgnoreQueryFilters` | ⚠️ alta confidenza, **da provare nello spike di setup (~30 min)** |
| Costo e deliverability dell'invio email (conferma account + reset password) | ❌ **non indagato** — dipendenza reale, oggi non tracciata in nessun documento |

---

## Opzioni considerate

### Parte A — Autenticazione

#### A1 — Identity con `MapIdentityApi` in modalità bearer token

**Pro:** una riga espone register/login/refresh/confirmEmail/forgotPassword/resetPassword/2fa; refresh con rotazione già pronta; il modo più veloce in assoluto per arrivare al login; naturale per una futura app mobile.

**Contro:** i token li conserva la SPA → `localStorage` (esfiltrabile da qualsiasi XSS, e un refresh token esfiltrato vale settimane di accesso) o in memoria (sessione persa a ogni reload). Token opachi, non ispezionabili. Revoca solo via security stamp con validation interval (default 30 min): "logout ovunque" non è istantaneo. Nessuna reuse detection. Gli endpoint **violano `API-CONVENTIONS.md`**: fuori da `/api/v1`, niente `ProblemDetails` coerente, non versionabili, e non sono slice — `Features/Authentication/` resterebbe una cartella vuota, cioè un buco nell'architettura dichiarata.

**Costo di inversione:** MEDIO. Migrare a cookie riscrive la gestione sessione lato SPA ma **non tocca le password**: lo store Identity resta identico.

#### A3 — JWT firmati custom

**Contro decisivo:** si riscrivono a mano, da soli e senza review, esattamente le parti che sbagliano tutti — tabella refresh token, rotazione, reuse detection, blocklist per la revoca (un JWT è valido finché non scade, *per definizione*), token di reset monouso, lockout. È l'area in cui un bug di un singolo sviluppatore non è un difetto ma una violazione di dati. E non dà nulla che A1/A2 non diano già, se non "controllo". **Scartata: non è peggiore ma giustificata, è peggiore e non giustificata.**

#### A4 — Provider esterno (Entra External ID / Auth0 / Clerk / Supabase Auth)

**Pro:** password, reset, conferma email, MFA, social login, rate limiting e deliverability escono dal perimetro di Roamly; nessuna password custodita → perimetro GDPR ridotto (rilevante per la #4); ottimo per mobile.

**Contro:** dipendenza su un fornitore per **l'identità degli utenti, che è il dato meno migrabile che esista** — uscire significa quasi sempre forzare un reset password a tutta la base utenti. Frizione in sviluppo locale e nei test di integrazione. Serve comunque una tabella `User` locale mappata su `sub`, con la sua sincronizzazione. Setup di tenant sproporzionato per un progetto personale. I tier gratuiti sono generosi **oggi**. Con Supabase Auth si introdurrebbe di fatto un secondo database, in contraddizione con ADR-0001.

**Costo di inversione: ALTO e asimmetrico** — il più alto della Parte A.

**Quando sarebbe la scelta giusta:** social login al day-1, MFA subito, o app mobile nell'MVP. Nessuno dei tre è nell'MVP 1.0.

### Parte B — Ownership

#### B1 — Solo global query filter + `ICurrentUser`

**Pro:** una riga per entità, protegge tutte le letture, impossibile dimenticarsene su una SELECT.

**Contro:** non protegge **nessuna scrittura**. Induce falsa sicurezza, che è peggio dell'assenza di sicurezza **perché chiude la discussione**.

#### B2 — Filtro esplicito in ogni handler

**Pro:** leggibile, greppabile, uniforme tra letture e scritture, nessuna magia, nessun problema con migration/seed/background.

**Contro:** **è disciplina.** Una singola omissione su ~60-80 handler è una IDOR, e le omissioni non falliscono in modo rumoroso: falliscono **restituendo dati**. Su un progetto a sviluppatore singolo senza review esterna, scommettere la sicurezza sulla costanza personale va dichiarato per quello che è.

#### B3 — Resource-based authorization (`IAuthorizationService.AuthorizeAsync`)

**Pro:** semanticamente corretto ("questo utente può fare questa operazione su questa risorsa", non "questa riga è mia"); **è l'unico che scala** a camper condiviso con il partner, viaggio in sola lettura per un amico, ruoli admin.

**Contro:** richiede di caricare la risorsa **senza filtro** e poi autorizzarla — e il primo passo è quello che si dimentica di proteggere. Si può ancora dimenticare di chiamare `AuthorizeAsync`, esattamente come in B2. Oggi, con un proprietario e nessun ruolo, è **struttura senza contenuto**: un handler che verifica `resource.OwnerId == user.Id` è cerimonia per un confronto di uguaglianza.

**Non scartato: datato.** Si adotta al trigger della condivisione.

#### Rifiutato esplicitamente — NetArchTest / ArchUnitNET per verificare il filtro

Questi strumenti ragionano su **tipi e dipendenze**, non sul contenuto delle espressioni LINQ: **non possono stabilire se un handler ha usato `DbSet` senza filtro.** L'unica cosa che potrebbero fare — vietare a `Features/` di referenziare `DbContext` — contraddice `ARCHITECTURE.md` §4 ("EF Core usato direttamente dalle feature", "evitare `GenericRepository<T>`"). Il lavoro lo fanno il test di convenzione sul modello EF e l'analyzer, meglio e senza contraddire l'architettura.

---

## Decisione

1. **Autenticazione:** ASP.NET Core Identity con **cookie `httpOnly` + `Secure` + `SameSite=Lax`**. Gli endpoint (`POST /api/v1/auth/login|register|logout|password/forgot|password/reset|email/confirm`) si scrivono in `Features/Authentication/` come **slice normali**, conformi a `API-CONVENTIONS.md`. Antiforgery sulle richieste non-GET, CORS con allowlist esplicita e `AllowCredentials`, `SecurityStampValidationInterval` a 5 minuti.
   **Condizionata a un deploy same-site** (vedi trigger 1).
2. **Identità applicativa:** un solo `ICurrentUser` con tre modi — HTTP (popolato da middleware dai claim), **system scope tracciato** (`RunAsSystem("motivo")`), **throwing** a design-time. Nessun accesso a `IHttpContextAccessor` fuori dal middleware che lo popola.
3. **Ownership (B4):** `IOwnedResource` con `OwnerId` su **ogni** entità; **filtro nominato `"OwnerScope"`** in lettura; **`SaveChangesInterceptor`** in scrittura; **`BannedApiAnalyzers`** sulle vie di fuga; **FK composite** verso il padre; `OwnerId` come **prima colonna** di ogni indice.
4. **Contratto API:** risorsa non posseduta → **`404` con `ProblemDetails` byte-identico** a quello di una risorsa inesistente. `403` riservato alle capability future.
5. **Verifica:** test di convenzione sul modello EF (bloccante) + test di ownership per slice table-driven (DoD) + analyzer a build-time.
6. **Rimandati con trigger:** resource-based authorization, schema bearer, 2FA, social login.

---

## Motivazione

### Parte A — perché il cookie

**Il CSRF si chiude con configurazione; l'XSS si chiude con disciplina.**

Il cookie `httpOnly` elimina alla radice la classe di attacco più probabile per questa app — l'esfiltrazione del token via XSS — perché il token **non è raggiungibile da JavaScript, punto**. Il CSRF è il prezzo da pagare, ed è mitigabile in modo *deterministico e verificabile*; la mitigazione dell'XSS è *probabilistica*, dipende dal non sbagliare mai una sanitizzazione, per sempre, da soli.

Tra un rischio che si chiude con configurazione e un rischio che si chiude con disciplina permanente, si sceglie il primo.

Vantaggi collaterali concreti: refresh implicito (sliding expiration, nessun refresh token da gestire), logout = invalidazione server-side immediata, e **l'upload di file funziona senza lavoro extra** — il cookie viaggia con il `multipart/form-data`, nessun token da iniettare nel form.

**L'obiezione mobile, disinnescata:** aggiungere in seguito uno schema bearer **accanto** a quello cookie è additivo — stesso `UserManager`, stesso database, stesse password, un policy scheme selector che sceglie in base all'header. Non è un refactor, è una feature. Per questo "ma domani forse un'app" non giustifica pagare oggi il rischio XSS.

### Parte B — cosa protegge davvero il query filter

Questa tabella è il contenuto informativo principale dell'ADR.

| Percorso | Filtro applicato? | Conseguenza |
|---|---|---|
| `_db.Campers.Where(...).ToListAsync()` | ✅ sì | protetto |
| `Include` / `ThenInclude` di entità filtrate | ✅ sì | protetto |
| Lazy / explicit loading di navigazioni filtrate | ✅ sì | protetto |
| `FromSql` composabile | ✅ sì | protetto, ma vietato per altri motivi |
| **`Find`/`FindAsync` con hit nel change tracker** | ❌ **no** | l'entità torna così com'è |
| `Find`/`FindAsync` con fallthrough a DB | ⚠️ non verificato | bandito a prescindere |
| **`Attach` / `Update` / `Remove` / `Entry().State`** | ❌ **no** | **scrittura su entità altrui possibile** |
| **`SaveChanges` su entità modificate** | ❌ **no** | il filtro non tocca l'UPDATE generato |
| **`ExecuteUpdate` / `ExecuteDelete`** | ❌ **no** | **cancellazione di massa di dati altrui possibile** |
| **`INSERT` di una nuova entità** | ❌ non applicabile | se `OwnerId` arriva dal client, è forgiabile |
| SQL raw non composabile / stored procedure | ❌ no | fuori dal modello |

> **Il global query filter è un meccanismo di lettura. Non è un meccanismo di autorizzazione.**
> Chiamarlo "protezione IDOR" e fermarsi lì è precisamente l'errore che la review C3 teme.
> Il suo valore resta alto ma limitato: rende sicuro il default del ~90% del codice, che sono query di lettura. Il restante 10% — le scritture — richiede un meccanismo diverso.

**Il fallimento tipico da intercettare:** il pattern sicuro di update è `SELECT` filtrata → modifica in memoria → `SaveChanges`. È sicuro **solo perché la SELECT è filtrata**. Se qualcuno sostituisce la SELECT con un `Attach` per "evitare il round-trip", la protezione sparisce in silenzio: nessun errore, nessun test rosso.

**Perché B4 e non B2:** in B4 nessuno dei tre livelli dipende dalla memoria dello sviluppatore. È la sola combinazione in cui la regola invariante è **applicata dalla macchina** invece che **promessa dall'uomo**. Costo di setup ~1 giorno, una tantum, prima della prima slice; a regime le slice successive non pagano nulla.

### Perché denormalizzare `OwnerId`

L'alternativa — filtrare per navigazione (`e => e.Trip.OwnerId == ...`) — evita la ridondanza ma: genera subquery correlate o join su **ogni** query di ogni entità figlia; su `Expense` (ownership polimorfa) non è esprimibile in un predicato pulito; e soprattutto **non aiuta per niente sulle scritture**, perché l'interceptor dovrebbe caricare il padre per sapere chi è il proprietario — una query extra per ogni entità salvata.

Denormalizzando: predicato identico e banale ovunque, `OwnerId` indicizzabile come prima colonna (seek invece di join), interceptor uniforme e senza I/O, `ExecuteUpdate` filtrabile in una riga.

Il rischio della ridondanza — un `TripStop` con `OwnerId` diverso da quello del suo `Trip`, dato corrotto che nessun filtro rileva — **si chiude a livello di database**: indice unique su `Trip(Id, OwnerId)` e **foreign key composita** da `TripStop(TripId, OwnerId)`. Il database rifiuta fisicamente la divergenza. La ridondanza smette di essere un rischio e diventa un vincolo.

Stessa logica già accettata in ADR-0001 (ridondanza deliberata con un unico punto di presidio), ma qui il presidio è più forte: è il motore relazionale, non il codice applicativo.

### Perché 404 e non 403

1. **Enumeration disclosure:** un `403` conferma che l'id esiste. Anche con `Guid` è un oracolo di esistenza gratuito.
2. **È l'esito naturale del meccanismo:** con il filtro attivo l'handler **non è in grado** di distinguere "inesistente" da "di un altro". Rispondere `403` richiederebbe di disattivare il filtro per scoprirlo — indebolire la protezione per migliorare un messaggio d'errore.
3. Coerenza con `API-CONVENTIONS.md` §3.

Vincoli che rendono la scelta reale invece che dichiarata: il `ProblemDetails` deve essere **byte-identico** a quello del 404 genuino (stesso `type`, `title`, nessun dettaglio aggiuntivo) e non deve differire in tempo di risposta in modo osservabile.

---

## La regola invariante — R1-R7

> **R1 — Ownership universale.** Ogni entità persistente implementa `IOwnedResource` ed espone `OwnerId`, oppure è in una **whitelist esplicita** di entità globali.
> *Verificata da: test di convenzione sul modello EF (bloccante).*
>
> **R2 — Lettura.** Ogni entità owned porta il query filter nominato `"OwnerScope"`.
> *Verificata da: test di convenzione sul modello EF.*
>
> **R3 — Scrittura.** Nessuna entità owned può essere inserita, modificata o cancellata se il suo `OwnerId` **originale** non coincide con l'identità corrente. `OwnerId` è assegnato dal server in creazione, non è mai modificabile né bindabile da un request model.
> *Verificata da: `SaveChangesInterceptor` a runtime + test di ownership per slice.*
>
> **R4 — Coerenza gerarchica.** L'`OwnerId` di un'entità figlia non può divergere da quello del padre.
> *Verificata da: foreign key composite nel database.*
>
> **R5 — Vie di fuga.** `Find`, `Attach`, `ExecuteUpdate`, `ExecuteDelete`, `FromSqlRaw` e `IgnoreQueryFilters` sono vietati fuori da `Common/Ownership/`. Ogni deroga è nominata, loggata e **conteggiata**.
> *Verificata da: `BannedApiAnalyzers` a build-time + test sulla whitelist delle deroghe.*
>
> **R6 — Identità sempre presente.** Nessun percorso esegue una query owner-scoped senza identità: assenza di identità = **eccezione**, mai insieme vuoto.
> *Verificata da: test su `ThrowingCurrentUser`.*
>
> **R7 — Indistinguibilità.** Una risorsa non posseduta è indistinguibile da una inesistente: `404` con `ProblemDetails` identico.
> *Verificata da: test di ownership per slice.*

La regola è soddisfatta quando **tutti e sette** i verificatori sono verdi, e **nessuno dei sette dipende dalla memoria di chi scrive la slice**.

### Dettaglio dei tre livelli

**Livello 1 — lettura.** Interfaccia marker `IOwnedResource { Guid OwnerId { get; } }`. In `OnModelCreating`, loop su tutte le entità che la implementano → `HasQueryFilter("OwnerScope", e => e.OwnerId == _currentUser.UserId)`, dove `_currentUser` è un **campo di istanza del DbContext** (EF parametrizza il valore per query; mai una variabile catturata o statica).

**Livello 2 — scrittura.** `SaveChangesInterceptor` che scorre il `ChangeTracker`:
- `Added` → `OwnerId` `default` viene assegnato da `ICurrentUser`; se valorizzato e diverso → eccezione;
- `Modified`/`Deleted` → confronta **`entry.OriginalValues[nameof(OwnerId)]`** con l'utente corrente; diverso → `OwnershipViolationException`;
- `Modified` → se `OwnerId` è tra le proprietà modificate → eccezione **sempre** (l'ownership non si trasferisce via update);
- l'eccezione produce `500` + **evento di sicurezza loggato**, non `403`: se accade è un bug, non un utente cattivo.

Chiude `Attach`, `Update`, `Remove`, il create forgiato e il trasferimento di ownership. **Non** chiude `ExecuteUpdate`/`ExecuteDelete`, che non passano dal change tracker → livello 3.

**Livello 3 — vie di fuga.** `Microsoft.CodeAnalysis.BannedApiAnalyzers` con `BannedSymbols.txt`, errore di compilazione e messaggio che rimanda a questo ADR. Nessun analyzer custom da scrivere. Le deroghe si concedono con `#pragma warning disable` **solo dentro `Common/Ownership/`**, e un test di convenzione **conta** le occorrenze confrontandole con una whitelist dichiarata.

> È così che l'escape hatch resta un'eccezione invece di diventare la norma: **l'eccezione è consentita ma è contata, e il contatore è un test.** Aggiungere una deroga richiede di modificare la whitelist, quindi l'atto diventa visibile nel diff invece di sparire dentro un handler.

### `ICurrentUser` fuori da HTTP

Non legge `IHttpContextAccessor` direttamente. Servizio scoped con tre modi:

- **HTTP:** popolato da middleware dai claim.
- **Sistema** (background service, job dei reminder manutenzione — che è **nell'MVP 1.0**): `using var _ = currentUser.RunAsSystem("MaintenanceReminderJob");`, che attiva `IgnoreQueryFilters("OwnerScope")` **solo dentro quello scope**; il nome del chiamante finisce nei log.
- **Design-time / migration:** `ThrowingCurrentUser` che **lancia** se interrogata. Il seed usa `RunAsSystem`.

**Fail-closed obbligatorio:** identità assente ≠ "nessun proprietario" con zero righe in silenzio. Deve **lanciare**. Le zero righe silenziose producono bug diagnosticati male e abituano a disattivare il filtro.

---

## Conseguenze

### Positive

- Il default del codice è sicuro: le slice non scrivono `.Where(x => x.OwnerId == ...)` 80 volte.
- I fallimenti sono **rumorosi** (build rotta o eccezione), non silenziosi.
- Aggiungere un'entità senza ownership **rompe la build dei test**.
- Nessuna dipendenza esterna, nessun costo ricorrente, nessuna password fuori dal perimetro.
- Upload file già coperto lato trasporto dal cookie.
- `Expense` **smette di avere ownership polimorfa**: con `OwnerId` proprio, il filtro è identico a quello di ogni altra entità.

### Negative — accettate consapevolmente

1. **Si introduce magia in un'architettura che dichiara di rifiutarla.** Filtro implicito e interceptor sono comportamento a distanza, contro lo spirito "no magia" di Vertical Slice. Accettato perché l'alternativa affida la sicurezza alla costanza di una persona sola, per anni. Il prezzo è che **serve documentazione, e la documentazione invecchia**.
2. **Tre meccanismi invece di uno.** Più superficie da capire, e il rischio concreto che tra sei mesi non si ricordi *quale* dei tre ha bloccato una query. Mitigato solo parzialmente da messaggi d'errore che citano l'ADR.
3. **`OwnerId` denormalizzato è ridondanza permanente**: storage, indici più larghi, FK composite che appesantiscono lo schema. **Costo di inversione MEDIO-ALTO** (migration su tutte le tabelle figlie): è la parte meno reversibile dell'intera decisione.
4. **Il `404` uniforme peggiora diagnostica e supporto.** Nessuna mitigazione lato client, solo lato log (`ownership_miss` con `requestedId` e `actualOwnerId`).
5. **Il cookie vincola la #9** e sposta rischio dall'XSS al CSRF. Il CSRF è *più facile da chiudere*, non *inesistente*: una configurazione CORS sbagliata lo riapre, silenziosamente.
6. **Identity con endpoint custom costa 1,5-2,5 giorni che `MapIdentityApi` costerebbe in mezz'ora.** È lavoro pagato per conformità a `API-CONVENTIONS.md` e coerenza architetturale, non per funzionalità. **Se la priorità fosse arrivare all'MVP nel minor tempo assoluto, A1 sarebbe la scelta giusta e questa decisione sarebbe quella sbagliata.**
7. **Dipendenza non risolta: l'invio email.** Conferma account e reset password non funzionano senza provider SMTP e reputazione di dominio. Non tracciata prima d'ora, non indagata.
8. **La scelta del tipo di chiave primaria diventa più vincolante** per via delle FK composite (con `Guid` la coppia è 32 byte; `Guid` casuale come PK clusterizzata frammenta). Questa decisione la tocca senza poterla chiudere: è dominio di @oracle.
9. **Nulla di tutto questo protegge l'upload di file.** Un URL firmato mal generato bypassa l'intero impianto, perché lo storage non conosce `OwnerId`. **La regola invariante si ferma al confine del database**: oltre, vale la decisione #8, ancora aperta.

---

## Trigger di revisione pre-approvati

1. La **#9 sceglie un deploy non same-site** → la Parte A va rinegoziata immediatamente (verso A1 o A4): il vantaggio del cookie verrebbe eroso da `SameSite=None` e dalle restrizioni sui cookie di terze parti.
2. Arriva la **condivisione** di un camper o di un viaggio tra utenti → si adotta B3 (resource-based): il filtro `"OwnerScope"` da solo diventa **insufficiente e sbagliato**, perché nasconderebbe le risorse condivise.
3. Arriva un'**app mobile** → si aggiunge uno schema bearer accanto al cookie (additivo).
4. Arrivano **`Place` pubblici o condivisi** (Phase 6) → quelle entità escono da `IOwnedResource` ed entrano nella whitelist esplicita; gli indici con `OwnerId` in testa vanno rivisti.
5. Una query owner-scoped **misurata** degrada per via degli indici composite → @oracle.

---

## Vincoli derivati per altre decisioni

| Decisione | Vincolo |
|---|---|
| **#9 deploy** | SPA e API **same-site**, altrimenti trigger 1. Key ring di data protection **condiviso e persistito** se il target è multi-istanza, altrimenti i cookie si invalidano a ogni deploy o scale |
| **#8 storage file** | deve includere il controllo di ownership sugli URL firmati: R1-R7 non arrivano lì |
| **#6 test DB** | diventa **prerequisito pratico**: senza un DB di test i test di ownership non esistono |
| **CONTEXT §3.3 `Expense`** | deve portare `OwnerId`; entrambe le FK opzionali devono essere composite + `CHECK` "esattamente uno valorizzato". Vale identico se @archimedes scegliesse "una tabella per contesto". **La #2 non pregiudica la scelta di modellazione** |
| **Tipo di PK** | da decidere con @oracle alla luce delle FK composite |

---

## Riferimenti

Documenti aggiornati da questo ADR: `architecture/SECURITY.md`, `architecture/API-CONVENTIONS.md`, `architecture/CONTEXT.md`, `architecture/TESTING.md`, `architecture/ARCHITECTURE.md`, `architecture/DEVOPS.md`, `OPEN-DECISIONS.md`.
