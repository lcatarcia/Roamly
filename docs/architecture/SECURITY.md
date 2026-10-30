# Roamly — Sicurezza, ownership e privacy

Stato: §1-§3 **chiuse** da [`ADR-0003`](../adr/0003-auth-and-ownership.md) e [`ADR-0004`](../adr/0004-privacy-and-erasure.md) · §4-§5 aperte · Ultimo aggiornamento: 2026-10-20
Origine: `docs/archive/Roamly_Planning_2026-09-24.md` §22 — esteso secondo review C3 e C4
Owner: **@sentinel**, con **@archimedes** per l'ownership

---

## 1. Baseline

- HTTPS;
- **autenticazione: ASP.NET Core Identity con cookie `httpOnly` + `Secure` + `SameSite=Lax`** (`Strict` romperebbe il ritorno dai link di conferma email);
- endpoint di auth scritti come slice in `Features/Authentication/`, conformi a `API-CONVENTIONS.md` — **non** `MapIdentityApi`;
- **antiforgery** obbligatorio sulle richieste non-GET, più requisito di header custom (`X-Roamly-Request`) come seconda barriera — **implementato al passo 6** (`Common/Security/CsrfProtectionMiddleware.cs`); reperto: i token sono legati all'identità della richiesta, il client deve rifetchare `GET /api/v1/csrf-token` dopo ogni login/logout (dettagli in `TESTING.md` §8.10);
- **CORS con allowlist esplicita** e `AllowCredentials`. Una configurazione CORS sbagliata riapre il CSRF **silenziosamente**: è il punto da presidiare;
- `SecurityStampValidationInterval` a **5 minuti** (revoca globale quasi immediata);
- password hashing gestito da ASP.NET Identity;
- secrets fuori dal repository, environment variables per la configurazione sensibile;
- validation dell'input;
- rate limiting dove necessario: il login usa il servizio SQL condiviso descritto in §1.2; export e cancellazione account mantengono il precedente `IPerAccountRateLimiter` in-memory, senza variazioni di soglia o semantica;
- protezione degli upload (tipo, dimensione, scansione);
- aggiungere che export e cancellazione sono **azioni sensibili**: re-autenticazione con password e rate limit dedicato;
- logging senza dati sensibili, ma **con** eventi di sicurezza dedicati (`OwnershipViolationException`, `ownership_miss`), che vivono sulla **pipeline di logging strutturato** con retention configurata nel sink — nessuna tabella su database.

> ⚠️ **Postura temporanea senza provider email:** non è configurato né integrato alcun mittente; conferma email e recupero/reset password automatizzati non sono disponibili. La scelta del provider, il costo e la deliverability restano aperti in `OPEN-DECISIONS.md`. L'uso previsto è un solo account, al massimo due.
>
> **Registrazione pubblica chiusa:** l'interfaccia e `POST /api/v1/auth/register` sono disponibili solo in `Development`; in `Production` l'endpoint non è mappato. Non riaprire la registrazione in Production prima di avere un provider reale e una revisione del flusso. `LoggingEmailSender` non consegna messaggi e registra il body completo, incluso il token Identity di conferma.
>
> **Perimetro di deploy scelto (2026-10-06):** ingress pubblico ACA diretto, senza WAF/gateway gestito, con accesso al prodotto tramite login Roamly. Login e API saranno quindi raggiungibili da Internet, pur senza registrazione pubblica. L'hardening applicativo approvato è implementato in §1.2 e coperto dai test HTTP/SQL; per ACA si usa il solo elemento finale di `X-Forwarded-For`, se valido. Queste scelte non equivalgono a una garanzia contro attacchi volumetrici né a una dichiarazione di production readiness.

### 1.2 Rate limit login distribuito

Il limite di capacità e le soglie per-IP vengono applicati **prima** di interrogare Identity o verificare la password; il limite per-account richiede invece l'ID dell'utente già risolto:

| Ambito | Soglia | Finestra | Cosa viene contato |
|---|---:|---:|---|
| Lockout ASP.NET Identity | 10 fallimenti consecutivi | 5 minuti | Errori di password per account esistente |
| Account Identity esistente | 10 credenziali errate | 15 minuti fissi | Credenziali errate dell'utente risolto |
| Username Identity inesistente | 10 tentativi | 15 minuti fissi | Tentativi aggregati in un unico bucket sintetico |
| IP client | 30 richieste | 5 minuti fissi | Ogni richiesta login, anche con credenziali corrette |
| IP client | 100 richieste | 1 ora fissa | Ogni richiesta login, anche con credenziali corrette |
| Identità IP attive | 10.000 distinte | Finestra oraria fissa | Ammissioni IP con contatori non ancora scaduti |

Il controllo cap e soglie IP avviene prima del lookup Identity; solo una richiesta ammessa interroga l'utente, poi applica la soglia del bucket account. Le finestre sono allineate all'orologio UTC, non scorrevoli. Le due soglie IP hanno namespace distinti. Per utenti esistenti, il bucket account è HMAC-SHA-256 dell'ID Identity stabile, non dell'email fornita. Per username inesistenti, il rate limiter usa un unico bucket sintetico condiviso, derivato con HMAC da un namespace distinto da quello degli ID Identity: nomi diversi non generano bucket separati e nessun username/IP in chiaro viene persistito. La chiave sintetica è distinta dai bucket reali e non interagisce con i lockout Identity. Lo stato è in `LoginAttemptCounters` su SQL Server/Azure SQL: `sp_getapplock` serializza gli incrementi sul singolo bucket e usa un lock globale soltanto per ammettere nuove identità IP; dopo il lock, un `UPDATE Count = Count + 1` SQL atomico o un inserimento iniziale sostituiscono read-modify-save. Le prenotazioni account restano nella transazione fino alla verifica fittizia o reale; una credenziale reale valida rimuove la prenotazione nella stessa transazione.

Il tetto di 10.000 identità IP ammesse con finestre IP ancora attive è condiviso fra repliche e rappresentato da marker HMAC con scadenza allineata alla finestra oraria. Gli IP già ammessi possono continuare a usare le soglie 30/5 minuti e 100/ora; una nuova identità oltre il tetto fallisce chiusa con `503` generico prima del controllo password e genera l'evento strutturato `login_ip_capacity_exhausted`, senza dati identificativi o fallback in-memory. È accettata la temporanea indisponibilità per nuovi IP quando il tetto è raggiunto.

Le righe contengono HMAC, tipo contatore e scadenza, mai email o IP in chiaro. La pulizia opportunistica può eliminare fino a 100 righe ogni 5 minuti. La pulizia completa viene eseguita dal job ACA one-shot giornaliero già previsto, tramite lo stesso image/entrypoint `--job account-erasure`: elimina le righe scadute in batch SQL da 1.000 fino a esaurimento. Un errore di pulizia viene loggato e riportato nel fallimento del job dopo aver lasciato proseguire i candidati di cancellazione account; una nuova esecuzione può riprendere la pulizia. La tabella è infrastrutturale, non ha `OwnerId`, FK o query filter e resta fuori da export e cancellazione.

Il bucket per-account usa l'HMAC dell'ID Identity stabile, assegnato dopo il lookup; varianti di maiuscole/minuscole risolte da ASP.NET Identity (`NormalizeName`) conducono quindi allo stesso account e bucket. L'IP viene canonizzato prima dell'HMAC. In Production si legge **solo l'ultimo elemento** di `X-Forwarded-For`, cioè quello aggiunto in coda dall'ingress ACA; i valori precedenti non influenzano il bucket. Un ultimo elemento assente o non valido non autorizza a ripiegare su un valore precedente. In Development e Testing si usa `Connection.RemoteIpAddress`; i test possono sostituire il provider senza fidarsi di header client arbitrari.

**Mitigazione dell'enumerazione account:** per l'utente assente il login applica la stessa soglia e finestra account in un bucket sintetico HMAC condiviso, poi esegue `VerifyHashedPassword` contro un hash fittizio generato dal `IPasswordHasher<ApplicationUser>` configurato e memorizzato lazy una sola volta per host. La verifica fittizia viene eseguita anche quando il bucket sintetico è pieno o il limiter non è disponibile; il primo caso restituisce il `401` generico, il secondo un `503` generico. Anche quando il bucket reale è esaurito, il login esegue la verifica fittizia e restituisce lo stesso `401` generico. Il solo esaurimento del limite IP continua a restituire `429` prima del lookup Identity; le soglie IP non cambiano. Nessun `Retry-After` è restituito per il limite account. Il ramo fittizio non chiama `SignInManager`, non incrementa né azzera `AccessFailedCount` o lockout Identity degli account reali e non registra email, username, IP, password o altri input. Il test d'integrazione verifica che username diversi condividano il bucket, che il limite non alteri il bucket o il lockout di un account reale, e che l'hash fittizio venga verificato anche oltre soglia, senza misurare elapsed time. Il lavoro divergente viene ridotto, ma non si garantisce un tempo costante: lookup Identity, SQL, rete e gli esiti diversi del limiter possono ancora introdurre variazioni osservabili.

Una chiave HMAC assente, non Base64 o inferiore a 32 byte casuali impedisce l'avvio dell'host (fail-fast). La configurazione è provider-agnostic e viene letta da `RateLimiting:Login:HmacSecret`, ad esempio dall'environment variable `RateLimiting__Login__HmacSecret`; non esiste un valore di default e la chiave non deve essere inserita in repository o `appsettings`. Per lo sviluppo locale si può salvarla in User Secrets dopo averla generata localmente:

```powershell
$secret = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "RateLimiting:Login:HmacSecret" $secret --project src/Roamly.Api
```

**Decisione approvata (2026-10-06) per il futuro deploy:** conservare i segreti runtime in Azure Key Vault e consentire alle istanze ACA di leggerli tramite managed identity, con privilegi minimi limitati ai soli segreti runtime necessari. GitHub Actions OIDC non deve poter leggere i valori dei segreti. Questa è una scelta documentale per il deploy futuro: non implementare ora wiring Key Vault/ACA, provisioning o modifiche ai workflow.

La stessa chiave HMAC deve essere fornita a tutte le istanze e mantenuta stabile. Una rotazione cambia gli identificatori HMAC e rende irraggiungibili i bucket esistenti; prima di abilitarla va definita e approvata una strategia di transizione compatibile con le finestre attive, senza presumere che basti sostituire il valore. Gli override numerici, se usati per test o configurazione, devono restare coerenti con le soglie approvate sopra.

Se IP o contatori SQL non sono verificabili, il login restituisce un `503` generico prima della verifica password; non usa fallback in-memory. Viene emesso un evento di errore costante, senza email, IP, password, hash o eccezione SQL nei campi applicativi. L'alerting effettivo dipende dal sink/monitoraggio operativo: nessuna regola Azure o risorsa di deploy viene creata qui.

> **Stato review del login (2026-10-07):** Janus ha approvato i gate d'implementazione IP-first e retry limitato all'apertura SQL; suite completa 195/195. L'utente accetta il rischio temporale residuo per uso personale con registrazione Production chiusa. Rivalutare l'accettazione prima di consentire altri account o registrazione pubblica; l'accettazione non certifica la production readiness.
>
> **Gate ancora aperti:** implementare nel futuro deploy il collegamento ACA managed identity → Key Vault con accesso minimo ai soli segreti runtime; definire la strategia di rotazione HMAC; configurare alert operativi per errori SQL/pulizia e `login_ip_capacity_exhausted`; verificare che il job one-shot ACA sia pianificato giornalmente e validare ingress ACA/XFF e SQL nel deploy effettivo. GitHub Actions OIDC resta escluso dalla lettura dei valori segreti. Nessun wiring o provisioning Azure è incluso. Questa implementazione non protegge da attacchi volumetrici e non dichiara il sistema production-ready.

### 1.1 Provisioning controllato temporaneo

La creazione dell'account iniziale è un'operazione locale amministrativa, non un endpoint HTTP né un seed all'avvio:

```powershell
dotnet run --project src/Roamly.Api -- --job provision-user
```

Il comando usa la connection string `ConnectionStrings:Roamly` e lo store Identity già configurati. Richiede una console interattiva, legge email e password da prompt e chiede due volte la password senza eco; password vuote o non coincidenti e input reindirizzato vengono rifiutati. Non passare password come argomento, non inserirle in script/log e non registrarle. Le regole di formato, unicità, hashing e qualità della password restano affidate a `UserManager<ApplicationUser>` e ai validator Identity. La creazione dell'utente e del contrassegno Identity che autorizza questa eccezione sono atomici.

L'account creato mantiene intenzionalmente `EmailConfirmed=false`: il fatto di averlo creato manualmente non prova il possesso dell'indirizzo. Solo gli account creati dal job possono autenticarsi non confermati; gli utenti registrati in Development restano bloccati fino alla conferma. L'eccezione è marcata nello store Identity, non altera il dato di verifica e non abilita la registrazione pubblica. `RequireConfirmedEmail=false` è un'impostazione globale di Identity: `LoginHandler` è il presidio effettivo che controlla il marker prima di emettere il cookie. Ogni futuro percorso di login o di autenticazione esterna deve conservare lo stesso controllo, oppure ripristinare il requisito Identity quando sarà disponibile la verifica reale. Non esiste ancora un flusso di reset/recupero password: se la password viene persa, serve una procedura operativa amministrativa fuori banda; non esporre un reset self-service fittizio.

**Quando si configura un provider reale:** integrare invio e verifica email reali e un flusso sicuro di reset password, verificare il ciclo di vita e i test; riattivare `RequireConfirmedEmail`, rimuovere l'eccezione di login per gli account non confermati e confermare gli account solo tramite verifica reale. Gli account esistenti non confermati dovranno completare quel flusso prima che la policy venga riattivata. Nessun provider o servizio Azure risulta integrato o distribuito da questa decisione.

### 1.2 Alpha privata: cambio password iniziale

Decisione dell'utente (2026-10-22), implementata: gli account dell'alpha privata, provisionati manualmente, devono cambiare la password al primo accesso riuscito prima di usare le funzionalità protette. `ApplicationUser.RequiresPasswordChange` è persistito; gli account provisionati nuovi lo impostano a `true` e la migrazione approvata marca quelli esistenti riconosciuti da `ProvisionedAccountClaims.UnconfirmedLogin`. Il middleware controlla il flag persistito per ogni richiesta autenticata a un endpoint protetto: solo `GET /api/v1/me` (stato canonico `mustChangePassword`), `POST /api/v1/auth/password/change` e logout restano utilizzabili. L'eccezione del cambio password è distinta dai metadati delle sessioni limitate per cancellazione; le autorizzazioni si intersecano e non si estendono a endpoint adiacenti. La modifica richiede la password attuale, una nuova password diversa da quella corrente, rispetta la policy ASP.NET Identity e pulisce il flag solo se l'aggiornamento riesce. Un tentativo di riutilizzo viene rifiutato sul campo `newPassword` senza sbloccare la sessione. La UI mostra un flusso dedicato anche dopo il reload, non richiede funzionalità prodotto finché il server non conferma il flag azzerato e applica le convenzioni italiane e di accessibilità.

Le password temporanee iniziali saranno casuali e univoche, consegnate tramite password manager con condivisione sicura; non vanno inviate via chat/email in chiaro né inserite in log o repository. Registrazione e reset password Production restano chiusi/non disponibili; se un tester perde la password definitiva è necessario un intervento amministrativo fuori banda. I test di integrazione coprono restrizioni, lettura `/me`, esito riuscito, password attuale errata, policy password, account ordinari e logout. La creazione degli account alpha resta un'operazione manuale separata e non fa parte dell'implementazione.

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
| **`RunAsSystem("motivo")`** | lavoro non riconducibile a un utente: polling di `AspNetUsers`, seed, scrittura della `ErasureReceipt` e delete finale di Identity | nessuna identità → le query owner-scoped **lanciano** |

| **Throwing** | design-time, migration | lancia sempre |

`ICurrentUserScope` è uno stato **scoped DI esplicito**, con callback `RunAsUserAsync` / `RunAsSystemAsync`; non usa `AsyncLocal`, identità `Guid.Empty` o fallback fail-open. Fuori da un callback worker, `ICurrentUser` continua a leggere l'identità HTTP dai claim. Dentro `RunAsSystem`, la lettura di un owner id lancia; dentro `RunAsUser`, il query filter resta vincolato all'utente passato.

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

> ✅ **Meccanismo di richiesta e revoca approvato dall'utente (decisione HIGH, 2026-10-06).** Usa il cookie Identity esistente: un login con password corretta durante la grazia emette il claim `roamly:session-mode=deletion-limited`; il claim resta limitato anche al rinnovo Identity del security stamp ogni 5 minuti, ed è preservato nel principal rinnovato. Credenziali errate restano indistinguibili da quelle di un account inesistente. Il cookie limitato può revocare la richiesta o fare logout, non accedere alle API ordinarie. Dopo la revoca resta limitato: serve un nuovo login per ottenere una sessione normale.
>
> Ogni endpoint protetto verifica lo stato dell'account direttamente su SQL Server a ogni richiesta, senza cache condivisa. Dopo il commit della richiesta, anche i cookie normali già emessi sono quindi bloccati alla richiesta protetta successiva; non si interrompono richieste già autorizzate.
>
> `POST /api/v1/me/deletion` richiede la password, usa `TimeProvider` e persiste l'istante UTC e la scadenza a 30 giorni. L'UPDATE condizionale conserva la scadenza in caso di replay; il POST dichiara `IdempotencyPolicy.NotRequired`. `DELETE /api/v1/me/deletion` è `204` idempotente, senza `404` né `If-Match`, con metadata `NotRequired`.
>
> Richiesta, revoca e futuro job devono serializzarsi sulla stessa riga account tramite UPDATE/lock condizionale: il job, nella propria transazione, deve acquisire il lock della riga e ricontrollare la richiesta/scadenza prima di cancellare. Se revoca e job competono, vince chi acquisisce per primo il lock; se vince la revoca, il job salta l'account, se vince il job, la revoca resta un no-op `204`.
>
> ✅ **Slice hard-delete implementata; scheduler e deploy non configurati.** `AccountErasureJob.RunOnceAsync` è invocabile dal processo API con `--job account-erasure`. Polling candidati e receipt/delete Identity usano `RunAsSystem`; il solo sweep owned usa `RunAsUser`, con `OwnerScope` attivo e `OwnerId` esplicito. Per ogni candidato la transazione EF inizia prima del lock SQL Server `UPDLOCK, HOLDLOCK`, poi il job rilegge e ricontrolla la scadenza con `TimeProvider`. Lo sweep percorre tutte le FK fra tipi owned in ordine inverso derivato da `IModel`, verifica zero righe e scrive receipt pseudonimizzata prima del delete Identity, tutto sulla stessa connessione/transazione. R9 è verificato su SQL Server reale per il modello migrato Phase 1; il flusso non è production-ready finché scheduler, deploy e controlli operativi non sono configurati e Janus non lo valida.

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
| **Cancellazione** | `POST /api/v1/me/deletion` → richiesta con **30 giorni** di grazia; account immediatamente non accessibile alle API ordinarie, revoca con `DELETE` da sessione limitata. Richiesta e revoca usano UPDATE condizionali sulla riga account. `AccountErasureJob.RunOnceAsync` esegue l'hard-delete one-shot; il worker usa il lock della riga, ricalcola la scadenza sotto lock e cancella in ordine topologico **calcolato dal modello**. L'invocazione CLI è `--job account-erasure`; scheduler e deploy restano da configurare |
| **`ErasureReceipt`** | entità **non owned**, senza FK verso `AspNetUsers`, con SHA-256 dell'ID nel formato deterministico GUID `D` minuscolo, timestamp `TimeProvider`, conteggi effettivi per tipo, `SchemaVersion = "1"` e nessun identificatore grezzo. Viene salvata prima del delete Identity ma nella stessa transazione; `StorageSweepConfirmed = true` perché Phase 1 non ha storage di file |
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
| `StoredFile` generico e documenti del camper | restano fuori dalla Phase 1; un nuovo tipo di file oltre a `JournalEntryAttachment` richiede una decisione su relazioni e policy |
| Anonimizzazione al posto della cancellazione | esistono dati aggregati che si vuole preservare |
| Audit trail delle modifiche | backoffice o secondo operatore |
| **Soft delete globale** | **mai**, respinto nel merito. Resta ammesso il soft delete *locale* a una singola entità, con filtro nominato |

### 3.5 Base giuridica e retention

> ⚠️ **Non è consulenza legale.** Le posizioni qui sotto sono difendibili, non certificate. Dettaglio e caveat in [`ADR-0004`](../adr/0004-privacy-and-erasure.md).

Il principio di `VISION.md` §2.6 ("trasparente, modificabile e sotto il controllo dell'utente") è ora tradotto in **tre verificatori automatici** (R8, R9, R10) invece che in un proposito. L'unica parte non verificabile da un test è la **retention del sink di logging**, che è configurazione di deploy: dichiarata come debolezza, non nascosta (decisione #9, @vulcan).

**Informativa**: contenuto statico versionato, da pubblicare prima del primo utente reale. `PrivacyPolicyVersionSeen` traccia quale testo l'utente ha visto — **non è un consenso**.

---

### 3.6 Export e conteggi di conferma

Il login/password riuscito aggiunge al ticket cookie Identity il claim UTC firmato `roamly:password-authenticated-at-utc`. La verifica dell'export rifiuta claim assenti, duplicati, malformati, futuri o più vecchi di **5 minuti**. Il rinnovo del security stamp ricopia i claim di sessione dal ticket corrente senza ricalcolare la data: un rinnovo non equivale a una nuova verifica password. Il claim è generato solo dal flusso login dopo `CheckPasswordSignInAsync` riuscito; il login emette un solo cookie. Anche il cookie a sessione limitata riceve il claim, ma l'export resta vietato dalla policy di sessione.

Il walker condiviso in `Common/Ownership` legge `RoamlyDbContext.Model` runtime e seleziona soltanto i tipi `IOwnedResource`. Ogni query applica `AsNoTracking()` e il predicato esplicito `OwnerId == currentUserId`, mantenendo attivo il query filter `OwnerScope`; le query di conteggio ed export sono sequenziali sullo stesso `DbContext`. L'export della Phase 1 contiene solo le entità owned effettivamente presenti nel contesto runtime: `FullSchemaDbContext` resta un verificatore, non è usato dall'API. Identity e `ErasureReceipt` sono escluse dal walker perché non sono `IOwnedResource`; navigation e cicli non sono serializzati, mentre i valori complex persistiti sono annidati nel JSON.

La risposta ZIP usa `application/zip`, `Cache-Control: no-store` e il nome non identificativo `roamly-export.zip`. Il flusso è scritto direttamente in `HttpResponse.BodyWriter`: non si costruiscono ZIP, liste di entità o JSON proporzionali alla dimensione del dataset in memoria, e non si creano file temporanei. `manifest.json` (versione 1) registra istante server, file, tipo e conteggio effettivamente scritto. L'endpoint dichiara metadata `PerAccount` e applica `IPerAccountRateLimiter` sulla chiave prefissata `account-export:{userId}`. Un errore dopo l'avvio della risposta abortisce la connessione e può lasciare un archivio incompleto senza manifest/central directory; il client deve scartarlo e ritentare l'intero download. Errori di autenticazione fresca e rate limit sono invece `ProblemDetails` prima dell'avvio dello streaming.

`GET /api/v1/me/deletion/preview` restituisce istante di generazione, conteggi per tipo owned e totale, ma è una fotografia **informativa**: le query non formano uno snapshot transazionale tra tipi e i valori non sono una promessa per il job futuro. Il job ricalcola i conteggi reali nella propria transazione; non emette receipt in caso di utente non dovuto/revocato, sweep incompleto o rollback. La disponibilità di preview, export, revoca e job **non afferma** che un scheduler sia attivo o che ogni cancellazione sia stata eseguita; privacy e deploy non sono production-ready.

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

> ✅ **Decisione #8 chiusa il 2026-10-09** → [`ADR-0010`](../adr/0010-journal-image-storage.md). La decisione non autorizzava provisioning o deploy; l'utente ha autorizzato separatamente il provisioning dello storage Blob il 2026-10-20. Non è stato autorizzato il deploy dell'app.

Nel Journal Phase 2 le foto sono `JournalEntryAttachment`, figlie di `JournalEntry`; non riusano `Document`, che resta nella Phase 4. Azure Blob Standard LRS Hot in Italy North è lo storage scelto. In locale/test si usa Azurite via Docker. L'upload è mediato dall'API in streaming; il backend accede a Blob con managed identity, il container è privato e l'accesso anonimo è disabilitato. Il download usa un URL firmato read-only, limitato al blob e valido per **5 minuti**, emesso solo dopo il controllo owner-scoped; i byte non sono serviti dall'API.

Limiti approvati: JPEG, PNG e WebP verificati lato server; massimo **10 MiB** per immagine e **10** immagini per `JournalEntry`. Gli originali sono conservati senza riscrittura: gli EXIF, incluse eventuali coordinate GPS, restano nei file. È stata accettata esplicitamente l'assenza di scansione antimalware, per semplicità e uso personale attuale; è un rischio residuo da rivalutare prima di consentire altri utenti o registrazione pubblica.

La decisione di non abilitare soft delete, versioning e backup Blob recuperabili implica che un blob cancellato non è recuperabile dall'utente. Il 2026-10-20 è stato creato e verificato lo storage account `storoamlywse7wit7fzpja` in Italy North: LRS Hot, TLS 1.2, shared key e accesso anonimo disabilitati, soft delete/versioning/change feed disabilitati e container privato. La managed identity runtime ha `Storage Blob Delegator` a livello account e `Storage Blob Data Contributor` sul solo container. Nella stessa data il test d'accettazione Azure è passato con owner e immagini sintetici, verificando via API upload, download SAS, export, delete/outbox e account-erasure; il test non verifica versioning, retention o backup, che vanno controllati separatamente prima dell'esecuzione. La verifica delle proprietà effettive dell'account e del Blob service è stata eseguita separatamente prima del test. Il cleanup ha lasciato zero blob e le assegnazioni RBAC temporanee del principal operatore sono state revocate. I backup Azure SQL restano separati (PITR 7 giorni); dopo un restore vanno rieseguite le erasure successive al punto ripristinato.

Le foto di viaggio **non sono il cuore dell'esperienza premium**: ADR-0007 mantiene il `Ribbon`, il dato numerico come superficie primaria e sand/contour generati in CSS. Le fotografie si aggiungono al Journal, non sostituiscono questo linguaggio visivo.

> **R19 — nessuna CDN di terzi**, da [`ADR-0007`](../adr/0007-design-system.md). Font e asset sono **self-hosted**. Il motivo è di privacy, non di performance: una richiesta a una CDN esterna espone l'indirizzo IP di ogni utente a un terzo, a ogni caricamento di pagina, senza base giuridica dichiarata nell'informativa. **Verificato in CI.**

Il provider locale è Azurite; il provider di produzione scelto è Azure Blob. Non usare filesystem locale o un secondo provider.

> **Vincolo derivato da [`ADR-0003`](../adr/0003-auth-and-ownership.md):** la decisione #8 **deve** includere il controllo di ownership sugli URL firmati. Le regole R1-R7 non arrivano allo storage.

### 5.1 Requisiti di accettazione F1-F7 (da [`ADR-0004`](../adr/0004-privacy-and-erasure.md))

`Document` resta fuori dalla Phase 1 e non viene riusato per il Journal. `JournalEntryAttachment` introduce invece i file immagine nella Phase 2; F1-F7 sono i criteri di accettazione per questo confine tra SQL e Blob.

| # | Vincolo |
|---|---|
| **F1** | **Chiavi owner-prefixed**: `{ownerId}/{tipoEntità}/{entityId}/{fileId}`. La cancellazione dev'essere esprimibile come **prefix-delete**, non come scansione del bucket |
| **F2** | **Riconciliazione bidirezionale**: un verificatore che rilevi sia oggetti senza riga sia righe senza oggetto. La coerenza tra due persistenze senza transazione comune o è verificata, o è sperata |
| **F3** | **Export incluso**: i binari entrano nello stesso ZIP, oppure l'export dichiara URL a tempo con scadenza nel manifest. Non due meccanismi diversi |
| **F4** | **Hard delete reale lato provider**: per Blob sono stati scelti soft delete, versioning e backup recuperabili disabilitati. Impostazioni effettive, snapshot e retention vanno comunque verificate sull'account prima del primo upload reale |
| **F5** | **Backup e retention dichiarati**: per Blob non si mantengono backup applicativi recuperabili; si dichiara la durabilità LRS del provider e l'irreversibilità del delete. Per Azure SQL restano PITR 7 giorni e replay delle erasure dopo restore |
| **F6** | **URL firmati owner-checked**, scadenza ≪ del periodo di grazia, mai serving diretto dall'API |
| **F7** | **Cancellazione idempotente e osservabile**: la `ErasureReceipt` **non viene emessa** finché lo sweep dello storage non è confermato |
