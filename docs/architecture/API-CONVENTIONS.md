# Roamly — Convenzioni API

Stato: **convenzioni chiuse da [`ADR-0005`](../adr/0005-api-conventions.md)** · Ultimo aggiornamento: 2026-09-28
Origine: `docs/archive/Roamly_Planning_2026-09-24.md` §17 — esteso secondo review I4
Owner: **@hermes**

---

## 1. Principi

Le API sono progettate intorno alle **feature verticali**, con endpoint semplici e coerenti. Il mapping endpoint → command/query è esplicito. Il contratto è documentato tramite OpenAPI.

```text
POST   /api/v1/auth/register
POST   /api/v1/auth/login
POST   /api/v1/auth/logout
POST   /api/v1/auth/password/forgot
POST   /api/v1/auth/password/reset
POST   /api/v1/auth/email/confirm
GET    /api/v1/csrf-token

GET    /api/v1/me
GET    /api/v1/me/export
POST   /api/v1/me/deletion
DELETE /api/v1/me/deletion

POST   /api/v1/campers
GET    /api/v1/campers/{id}
PUT    /api/v1/campers/{id}

POST   /api/v1/campers/{id}/maintenance
GET    /api/v1/campers/{id}/maintenance

POST   /api/v1/trips
GET    /api/v1/trips/{id}
POST   /api/v1/trips/{id}/stops
POST   /api/v1/trips/{id}/expenses

GET    /api/v1/dashboard
```

Gli endpoint di auth sono **slice normali** in `Features/Authentication/`, non `MapIdentityApi`: devono rispettare le stesse convenzioni di tutti gli altri (vedi [`ADR-0003`](../adr/0003-auth-and-ownership.md)).

### 1.1 Requisiti di read model introdotti da ADR-0007

Il design system impone alcune cose alla forma delle risposte, non solo alla loro struttura:

| Requisito | Motivo |
|---|---|
| Il read model del `Ribbon` espone **`from`, `to`, `current`** e **quale soglia scade prima** (km o tempo), già risolti | Stabilire quale delle due soglie vincola è **logica di dominio**: se finisce nel frontend, `VISION.md` §2.5 è violato dal componente più visibile dell'MVP. ⚠️ Dipende dal prerequisito aperto di `CONTEXT.md` §3.9 |
| Le **date di cancellazione** (fine del periodo di grazia) arrivano **dal server** e non vengono mai ricalcolate dal client (**R23**) | Un client con l'orologio sfasato o un fuso diverso mostrerebbe all'utente una data diversa da quella su cui agirà il job. Su un'azione irreversibile è un problema di sicurezza |
| Gli endpoint che precedono una cancellazione espongono un **conteggio delle entità** che verranno eliminate | La schermata "Elimina account" deve dire cosa si perde in concreto, non genericamente |
| Gli importi viaggiano come **`Money` = importo + valuta**, mai come numero nudo | Il registro neutro di R21 richiede che la UI non deduca né formatti una valuta implicita |

> ⚠️ **Endpoint mancante.** Il gap sulla revoca della cancellazione (`SECURITY.md` §3) implica che `DELETE /api/v1/me/deletion` debba restare raggiungibile da un utente **che non può più autenticarsi normalmente**. La forma di questo endpoint è in carico a @sentinel e @hermes.
>
> [`ADR-0005`](../adr/0005-api-conventions.md) fissa già la parte che è convenzione API — `DELETE /api/v1/me/deletion`, **`204` sia se una cancellazione era programmata sia se non lo era** (idempotente, `NotRequired` per R50), **nessun `404`** se non c'è nulla da revocare (sarebbe un oracolo sullo stato dell'account, vietato da R46), **nessun `If-Match`** — e **rinvia a @sentinel il meccanismo di accesso**, che è una decisione di autenticazione e non di contratto. Blocca la schermata «Elimina account», non il passo 6 della ROADMAP.

---

## 2. Convenzioni da definire prima del primo endpoint

Il planning elencava gli endpoint ma non ciò che rende un'API mantenibile. Queste convenzioni andavano chiuse **prima** di scrivere il primo `MapPost`, perché cambiarle dopo è un refactor trasversale. **Sono tutte chiuse**: le otto voci da [`ADR-0005`](../adr/0005-api-conventions.md) (regole **R45-R53**), le due di ownership e auth da [`ADR-0003`](../adr/0003-auth-and-ownership.md).

| Area | Requisito | Stato |
|---|---|---|
| **Error model** | `ProblemDetails` (RFC 9457) **costruito** da una factory unica in `Common/Http/`, mai ereditato dal default; `detail`/`instance`/`errors` mai popolati sui 404 — **R45, R46** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Validation errors** | `400` + `HttpValidationProblemDetails` con `errors` **chiavizzato sui nomi JSON** dei campi; validazione **nativa .NET 10** (`AddValidation()`), **non** FluentValidation — **R48** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Paginazione** | cursore keyset **opaco** (`?limit=&after=`), risposta sempre `{ items, nextCursor }`, servito dalla clustering key `(OwnerId, Id)` — **R52** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Filtering / sorting** | nessuna grammatica generica: ordinamento fisso per collection, filtri whitelist tipizzata nel request model — **R53** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Concorrenza** | ETag debole dal `rowversion`, `If-Match` **obbligatorio** sugli update: assente → `428`, stantio → `412`. Il «dove» del `rowversion` è nella *Nota sulla concorrenza* qui sotto — **R49** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Idempotency** | nessun header: idempotenza dall'`Id` COMB client-side ([`ADR-0008`](../adr/0008-primary-key-strategy.md)); ogni `POST` **dichiara** la sua politica come metadata — **R50** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Versioning** | dentro `v1` si può solo **aggiungere**; rimozione/rinomina/restrizione ⇒ `/api/v2`, applicato da snapshot OpenAPI committato con diff bloccante — **R51** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Status code di dominio** | esito negativo di dominio ≠ errore HTTP: `200` + envelope `rejected`, handler che restituisce `DomainResult<T>` — **R47** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Ownership** | risorsa non posseduta → **`404`** con `ProblemDetails` **byte-identico** a quello di una risorsa inesistente; `403` riservato alle capability future | ✅ [`ADR-0003`](../adr/0003-auth-and-ownership.md) |
| **Auth** | cookie `httpOnly`/`Secure`/`SameSite=Lax`, antiforgery su non-GET, CORS allowlist con credenziali | ✅ [`ADR-0003`](../adr/0003-auth-and-ownership.md) |

### Nota sull'ownership: perché 404 e non 403

Un `403` **confermerebbe che l'id esiste** — un oracolo di esistenza gratuito, anche con `Guid`. E con il query filter attivo l'handler **non è nemmeno in grado** di distinguere "inesistente" da "di un altro": rispondere `403` richiederebbe di disattivare il filtro per scoprirlo, cioè indebolire la protezione per migliorare un messaggio d'errore.

Vincoli che rendono la scelta reale invece che dichiarata: `type` e `title` identici, nessun dettaglio aggiuntivo, e nessuna differenza osservabile nel tempo di risposta. La distinzione vive **solo nei log** (`ownership_miss` con `requestedId` e `actualOwnerId`).

> Questa nota resta valida e non è stata modificata. È ora **sistematizzata da R46, [`ADR-0005`](../adr/0005-api-conventions.md)**, che nomina i campi da cui l'oracolo rientrerebbe: `detail` e `instance` non sono mai popolati sui 4xx di lookup, `errors` non compare mai in un `404`, e l'insieme delle chiavi di `Extensions` è una lista chiusa dichiarata in un punto unico. È ciò che rende **R7** verificabile invece che promessa.

### Nota sugli status code di dominio

Caso di riferimento: **camper non compatibile con il percorso**.

Non è un errore del client: è una **risposta di dominio valida**. Deve essere `200` con esito negativo strutturato, **non** `400`.

Questa distinzione va applicata in modo sistematico: `4xx` per input/permessi sbagliati, `200` per risposte di dominio negative.

> Il **principio** qui sopra resta invariato: [`ADR-0005`](../adr/0005-api-conventions.md) ne fissa la **forma** ed è ora normato da **R47**. Ogni `HandleAsync` sotto `Features/` restituisce `DomainResult<T>`; l'esito negativo viaggia su `200` con l'envelope `{ "outcome": "rejected", "reasons": [{ "code", "message" }] }`; nessun handler usa un'eccezione per esprimere un esito di dominio previsto. La traduzione esito → HTTP avviene solo nell'endpoint.

### Nota sulla concorrenza: dove vive il `rowversion`

Questa tabella rimandava al «`rowversion` di `DATA.md`», e `DATA.md` §2 rimandava agli ETag dell'API: **nessuno dei due diceva dove**. Il «dove» è questo, verificato nel codice e fissato da [`ADR-0005`](../adr/0005-api-conventions.md):

| Entità | Proprietà | Configurazione | Migration `InitialPhase1` |
|---|---|---|---|
| `Camper` | `byte[]? RowVersion` | `IsRowVersion()` in `CamperConfiguration` | ✅ colonna `rowversion` |
| `MaintenanceItem` | `byte[]? RowVersion` | `IsRowVersion()` in `MaintenanceItemConfiguration` | ✅ colonna `rowversion` |
| `Trip` | `byte[]? RowVersion` | `IsRowVersion()` in `TripConfiguration` | ❌ **assente, e correttamente** |

`Trip` è **Phase 2+**: è registrata solo in `FullSchemaDbContext` e non nel `RoamlyDbContext` di runtime, quindi la colonna esiste nel modello completo ma **non** in `InitialPhase1`. È una **fasatura deliberata per R32, [`ADR-0009`](../adr/0009-test-strategy.md)**, non una divergenza fra modello e migration.

Le altre entità owned **non** hanno token di concorrenza, e oggi nessun endpoint le aggiorna. **R49, [`ADR-0005`](../adr/0005-api-conventions.md)** fa di questo un invariante verificato: un endpoint `PUT`/`PATCH`/`DELETE` su un'entità priva di token è rosso a L0: serve **prima** una migration che glielo aggiunga.

⚠️ `RowVersion` è `byte[]?` **nullable**: SQL Server valorizza sempre la colonna, ma il codice che genera l'ETag deve trattare il null come «nessun ETag» invece di emettere `W/""`.

---

## 3. Regola non negoziabile

Ogni endpoint che espone un `{id}` è owner-scoped. Il meccanismo non è a carico della slice: vedi **R1-R10** in `SECURITY.md` §2.2, [`ADR-0003`](../adr/0003-auth-and-ownership.md) e [`ADR-0004`](../adr/0004-privacy-and-erasure.md).

> **`{id}` è sempre un `Guid`** ([`ADR-0008`](../adr/0008-primary-key-strategy.md)), mai un intero. L'URL resta a **un solo segmento** — `OwnerId` **non compare mai nel path**: viene dal claim, e il lookup lato server è sempre sulla chiave completa `(OwnerId, Id)`. Un client non può nemmeno esprimere una richiesta per un altro proprietario, e il `404` uniforme di R7 è invariato.

Resta a carico della slice il **test di ownership** (lettura *e* scrittura), parte della definition of done.
