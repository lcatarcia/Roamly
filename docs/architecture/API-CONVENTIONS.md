# Roamly — Convenzioni API

Stato: **convenzioni chiuse da [`ADR-0005`](../adr/0005-api-conventions.md)** · Ultimo aggiornamento: 2026-10-21
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
GET    /api/v1/me/deletion/preview
POST   /api/v1/me/deletion
DELETE /api/v1/me/deletion
GET    /api/v1/me/deletion

POST   /api/v1/campers
GET    /api/v1/campers?limit=&after=
GET    /api/v1/campers/{id}
PUT    /api/v1/campers/{id}
GET    /api/v1/campers/{camperId}/dashboard

POST   /api/v1/campers/{id}/maintenance
GET    /api/v1/campers/{id}/maintenance
POST   /api/v1/campers/{camperId}/maintenance/{maintenanceItemId}/logs
GET    /api/v1/campers/{camperId}/maintenance/{maintenanceItemId}/logs?limit=&after=

POST   /api/v1/campers/{camperId}/expenses
GET    /api/v1/campers/{camperId}/expenses?limit=&after=&tripId=
GET    /api/v1/expenses/{expenseId}
PUT    /api/v1/expenses/{expenseId}
DELETE /api/v1/expenses/{expenseId}

POST   /api/v1/campers/{camperId}/trips
GET    /api/v1/trips?limit=&after=
GET    /api/v1/trips/{tripId}
PUT    /api/v1/trips/{tripId}
POST   /api/v1/trips/{tripId}/transitions
GET    /api/v1/campers/{camperId}/trips/{tripId}
GET    /api/v1/trips/{tripId}/checklists?limit=&after=

```

Gli endpoint di auth sono **slice normali** in `Features/Authentication/`, non `MapIdentityApi`: devono rispettare le stesse convenzioni di tutti gli altri (vedi [`ADR-0003`](../adr/0003-auth-and-ownership.md)).


I POST `/api/v1/auth/register`, `/api/v1/auth/email/confirm`, `/api/v1/auth/login` e `/api/v1/auth/logout` dichiarano `NotRequired` come metadata di idempotenza. `POST /api/v1/campers` usa `IdentityBased`: il browser invia nel body l’ID COMB; la prima creazione restituisce `201 Created` con `Location`; per lo stesso `(OwnerId, Id)`, contenuto normalizzato equivalente restituisce `200`, contenuto diverso `409 Conflict` con `ProblemDetails`. La normalizzazione è definita in ADR-0005 §5/R50.

### 1.1 Requisiti di read model introdotti da ADR-0007

Il design system impone alcune cose alla forma delle risposte, non solo alla loro struttura:

| Requisito | Motivo |
|---|---|
| `GET /api/v1/campers/{camperId}/dashboard` espone il camper, la lettura datata più recente (con indicazione `isStale`) e stati distinti per asse temporale e chilometrico | Le scadenze sono risolte dal server: `DueSoon` entro 30 giorni o 1.000 km; `Overdue` a soglia raggiunta. Se la lettura manca o supera 90 giorni l'asse km è `Unknown`. Non esistono `bindingAxis`, previsioni o uno stato complessivo |
| Le **date di cancellazione** (fine del periodo di grazia) arrivano **dal server** e non vengono mai ricalcolate dal client (**R23**) | Un client con l'orologio sfasato o un fuso diverso mostrerebbe all'utente una data diversa da quella su cui agirà il job. Su un'azione irreversibile è un problema di sicurezza |
| Gli endpoint che precedono una cancellazione espongono un **conteggio delle entità** che verranno eliminate | La schermata "Elimina account" deve dire cosa si perde in concreto, non genericamente |
| Gli importi viaggiano come **`Money` = importo + valuta**, mai come numero nudo | Il registro neutro di R21 richiede che la UI non deduca né formatti una valuta implicita |

> ✅ **Meccanismo approvato dall'utente il 2026-10-06 (HIGH):** login Identity con password fresca e claim di sessione limitata durante la grazia; policy middleware che ammette tale sessione solo alla revoca o al logout. Il middleware controlla lo stato dell'account dal database a ogni richiesta protetta, senza cache condivisa, bloccando anche i cookie normali precedentemente emessi dopo il commit della richiesta.
>
> [`ADR-0005`](../adr/0005-api-conventions.md) fissa il contratto: `DELETE /api/v1/me/deletion` restituisce sempre **`204`** quando la policy di sessione consente la richiesta, sia con richiesta attiva sia senza (idempotente, metadata `NotRequired` per R50), **nessun `404`** e **nessun `If-Match`**. La revoca è un UPDATE condizionale sulla stessa riga account usata dal futuro job; il job dovrà acquisire il lock e ricontrollare lo stato nella stessa transazione. Il dettaglio implementativo e il limite dovuto al job non ancora disponibile sono in `SECURITY.md` §3.

`GET /api/v1/me/export` richiede un claim UTC firmato nel ticket cookie Identity: è aggiunto soltanto dopo la verifica password riuscita e resta valido per cinque minuti. Claim mancante, duplicato, malformato, futuro o scaduto produce `401 ProblemDetails` prima di avviare il body. La risposta è `application/zip`, `Cache-Control: no-store`, filename neutro `roamly-export.zip`; ogni file entity è un array JSON in `entities/<EntityType>.json`, con proprietà scalari persistite e valori complex annidati, senza navigation. `manifest.json` ha `formatVersion`, `generatedAtUtc` e `files` (`entityType`, `path`, `count`); il conteggio è quello scritto nello stream. Il body ZIP va direttamente alla risposta HTTP senza staging: se un errore avviene dopo l'inizio, la connessione viene interrotta e il client deve scartare il download e ritentare l'intero export.

`GET /api/v1/me/deletion/preview` risponde con `{ generatedAtUtc, entityCounts: [{ entityType, count }], totalCount }`. È una fotografia informativa, non uno snapshot tra tipi né una garanzia dei numeri che ricalcolerà il job fra 30 giorni. `GET /api/v1/me/deletion` è accessibile anche alla sessione limitata e restituisce solo `{ status, deletionRequestedAtUtc, deletionScheduledForUtc }`; in assenza di richiesta attiva risponde `204` senza corpo, senza rivelare ID, owned records o conteggi.

`GET /api/v1/campers` è owner-scoped e segue R52: `Page<T>` (`items`, `nextCursor`), `limit` predefinito 50 e massimo 200, cursore keyset opaco COMB e `400` su limite o cursore malformato (il cursore usa `https://roamly.dev/problems/invalid-cursor`). Il dashboard è una lettura dedicata al camper; id inesistente e non posseduto producono lo stesso `404`. I widget di spesa e viaggio restano fuori da questa slice.

Le collection di manutenzione sono owner-scoped e seguono R52: `GET /api/v1/campers/{camperId}/maintenance` e `GET /api/v1/campers/{camperId}/maintenance/{maintenanceItemId}/logs` restituiscono `Page<T>`, ordinate per `Id`, con limite predefinito 50 e massimo 200. La creazione usa `POST /api/v1/campers/{camperId}/maintenance`; la registrazione di un intervento usa `POST /api/v1/campers/{camperId}/maintenance/{maintenanceItemId}/logs`. Entrambi i `POST` usano ID COMB client-generated e `IdentityBased` secondo R50. Il costo supporta al massimo quattro cifre decimali, coerentemente con `decimal(19,4)`. Un replay identico restituisce `200`, un ID riutilizzato con contenuto diverso `409`; la collisione con una lettura giornaliera di km discordanti restituisce il problema `https://roamly.dev/problems/odometer-reading-conflict` con `409` e annulla l'intera scrittura. Una lettura odometrica dello stesso giorno e valore è riutilizzata senza cambiarne la provenienza; se assente viene inserita con `Source = Service`. Se il calcolo della scadenza supera `DateOnly.MaxValue` o `int.MaxValue`, il POST restituisce `400` con `https://roamly.dev/problems/maintenance-log-projection-out-of-range` e annulla log e lettura. Un conflitto `RowVersion` su log distinti viene ricaricato e riprovato una volta; se persiste, il POST restituisce `409` con `https://roamly.dev/problems/maintenance-state-conflict`, ritentabile con lo stesso ID e contenuto.

La gestione spese è owner-scoped e camper-first: `POST /api/v1/campers/{camperId}/expenses` usa ID COMB client-generated e `IdentityBased`; replay con contenuto business normalizzato uguale (descrizione trim, importo/valuta/categoria/data/odometro/viaggio esatti) restituisce `200` e contenuto diverso `409`. `201` e replay `200` restituiscono `ETag`; la creazione include anche `Location`. `GET /api/v1/campers/{camperId}/expenses` restituisce `Page<T>` ordinata per `Id`, con limite 50/200 e filtro whitelist tipizzato `tripId`; il camper viene verificato prima della lettura pagina. I dettagli espongono sempre `Money` annidato `{ amount, currency }`; l'importo è firmato (negativo = rimborso), non zero, compreso tra `-999999999999999.9999` e `999999999999999.9999`, con al massimo 4 decimali (`decimal(19,4)`), senza conversione. `CamperId` non è modificabile: `PUT /api/v1/expenses/{expenseId}` sostituisce i campi business e richiede `If-Match`; `DELETE` è definitivo, richiede `If-Match` e restituisce `204`. Lookup mancanti o non owned → `404`; token assente → `428`; token malformato, stantio o conflitto DB → `412`. Il Trip deve appartenere allo stesso owner e camper; un Trip cancellato non riceve nuove associazioni (`409`), mentre una spesa già collegata può essere modificata mantenendo tale `TripId`.

`GET /api/v1/trips` è owner-scoped e segue R52: restituisce `Page<T>` ordinata per `Trip.Id`, con limite predefinito 50 e massimo 200 e cursore keyset opaco. Gli elementi espongono `Id`, `CamperId`, `Name` e `Status`, senza le tappe; un cursore non valido restituisce `400` con `type` `https://roamly.dev/problems/invalid-cursor`. Entrambe le GET del dettaglio restituiscono `ETag` e le date effettive. `PUT /api/v1/trips/{tripId}` è la sostituzione completa atomica dell'aggregato Trip: `If-Match` è obbligatorio, l'array `stops` determina l'ordine, gli ID conservati mantengono la propria identità e quelli omessi vengono rimossi. `estimatedDistanceKm` (stringa decimale esatta in km, memorizzata come `decimal(18,2)`) e `estimatedDurationMinutes` (minuti, `int`) sono totali manuali dell'intero viaggio, indipendenti e facoltativi: valori nulli o proprietà omesse nel PUT li azzerano; la creazione li lascia null. La distanza accetta una stringa con da 1 a 16 cifre intere e fino a 2 decimali, da 0 a `9999999999999999.99`; questa forma evita perdita di precisione nei client JavaScript. La durata è non negativa e nel range `int32`. `fuelPricePerLiter` è un `Money` facoltativo annidato `{ amount, currency }`, persistito sul Trip; null o proprietà omessa nel PUT lo azzerano, mentre la creazione lo lascia null. L'importo deve essere positivo, entro `decimal(19,4)` e con massimo 4 decimali; la valuta richiede 3 lettere ASCII maiuscole. L'interfaccia web limita l'importo a `9999.9999` per evitare perdita di precisione nella conversione JavaScript; il contratto API continua ad accettare il range `decimal(19,4)`. Un prezzo più alto già presente viene segnalato nell'editor, anche per Trip completati o annullati, e, per non arrotondarlo durante il PUT, deve essere aggiornato entro il limite o cancellato prima di salvare. Questi campi non cambiano stato/date effettive. Il costo carburante è calcolato solo nella UI, da distanza, consumo medio del profilo Camper e prezzo, nella valuta inserita, senza conversione; non è restituito né persistito dall'API. Input fuori limite restituisce `400` senza scrittura parziale. Un figlio o un dato modificabile avanzano il rowversion del Trip e il PUT restituisce il nuovo ETag; il PUT non accetta né modifica stato o date effettive e non espone `SavedPlaceId`. Se una tappa omessa è referenziata da una voce di diario, l'intera scrittura fallisce con `409`.

`POST /api/v1/trips/{tripId}/transitions` richiede `If-Match`, dichiara `IdempotencyPolicy(NotRequired)` e riceve `{ "status": 0|1|2|3 }` (`Planned`, `Active`, `Completed`, `Cancelled`). La matrice di `CONTEXT.md` §2.3 è l'unica fonte delle transizioni ammesse; una transizione illegale o verso lo stato corrente restituisce `409` (`https://roamly.dev/problems/invalid-trip-transition`). Entrambi i codici `ActualStartOnUtc` e `ActualEndOnUtc` sono assegnati dal server in UTC: avvio e completamento usano la data UTC corrente, mentre la riapertura `Completed → Active` conserva la data iniziale e azzera quella finale. La risposta `200` contiene il dettaglio corrente e il nuovo `ETag`; input mancante/non valido → `400`, Trip assente o non posseduto → `404`, `If-Match` assente → `428`, ETag malformato/stantio o conflitto di concorrenza → `412`.

La creazione della regola non crea uno storico: senza `MaintenanceLog` le scadenze restano nulle e il dashboard indica `NotScheduled`. I log sono fatti immutabili; la proiezione seleziona il massimo `(PerformedOnUtc, CreatedAtUtc, Id)` e non può essere spostata all'indietro da inserimenti retroattivi.

---

## 2. Convenzioni da definire prima del primo endpoint

Il planning elencava gli endpoint ma non ciò che rende un'API mantenibile. Queste convenzioni andavano chiuse **prima** di scrivere il primo `MapPost`, perché cambiarle dopo è un refactor trasversale. **Sono tutte chiuse**: le otto voci da [`ADR-0005`](../adr/0005-api-conventions.md) (regole **R45-R53**), le due di ownership e auth da [`ADR-0003`](../adr/0003-auth-and-ownership.md).

| Area | Requisito | Stato |
|---|---|---|
| **Error model** | `ProblemDetails` (RFC 9457) **costruito** da una factory unica in `Common/Http/`, mai ereditato dal default; `detail`/`instance`/`errors` mai popolati sui 404 — **R45, R46** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Validation errors** | `400` + `HttpValidationProblemDetails` con `errors` **chiavizzato sui nomi JSON** dei campi; validazione **nativa .NET 10** (`AddValidation()`), **non** FluentValidation — **R48** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Paginazione** | cursore keyset **opaco** (`?limit=&after=`), risposta sempre `{ items, nextCursor }`, servito dalla clustering key `(OwnerId, Id)` — **R52** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Filtering / sorting** | nessuna grammatica generica: ordinamento fisso per collection, filtri whitelist tipizzata nel request model — **R53** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Concorrenza** | ETag forte dal `rowversion` nel formato `"<base64url RowVersion>"`; `If-Match` forte **obbligatorio** sugli update: assente → `428`, debole/malformato/stantio → `412`. Il «dove» del `rowversion` è nella *Nota sulla concorrenza* qui sotto — **R49** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md), emendamento utente 2026-10-08 |
| **Idempotency** | nessun header: ogni `POST` dichiara un metadata esplicito — **R50**. I POST di Authentication e `POST /api/v1/me/deletion` sono `NotRequired`; quest'ultimo non resetta la scadenza persistita grazie all'UPDATE condizionale sulla riga account. `POST /api/v1/campers` è `IdentityBased`, con ID COMB generato dal browser e inviato nel body. Esiti e normalizzazione: ADR-0005 §5/R50 | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Versioning** | dentro `v1` si può solo **aggiungere**; rimozione/rinomina/restrizione ⇒ `/api/v2`, applicato da snapshot OpenAPI committato con diff bloccante — **R51** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
| **Status code di dominio** | rifiuto di dominio: `200` + envelope `rejected`, handler `DomainResult<T>`; esiti di protocollo dichiarati esplicitamente e tradotti secondo il contratto della slice (`R50`: mismatch d'idempotenza → `409`) — **R47** | ✅ [`ADR-0005`](../adr/0005-api-conventions.md) |
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

> Il **principio** qui sopra resta invariato: [`ADR-0005`](../adr/0005-api-conventions.md) ne fissa la **forma** ed è ora normato da **R47**. Gli handler che restituiscono rifiuti di dominio usano `DomainResult<T>`; questi viaggiano su `200` con l'envelope `{ "outcome": "rejected", "reasons": [{ "code", "message" }] }`. Un esito di protocollo con contratto HTTP distinto deve essere dichiarato sul handler con `[ProtocolOutcomeHandler("motivazione")]` e segue il contratto della slice; ad esempio il mismatch idempotente di R50 restituisce `409 ProblemDetails`. Nessun handler usa un'eccezione per esprimere un esito previsto. La traduzione esito → HTTP avviene solo nell'endpoint.

### Nota sulla concorrenza: dove vive il `rowversion`

Questa tabella rimandava al «`rowversion` di `DATA.md`», e `DATA.md` §2 rimandava agli ETag dell'API: **nessuno dei due diceva dove**. Il «dove» è questo, verificato nel codice e fissato da [`ADR-0005`](../adr/0005-api-conventions.md):

| Entità | Proprietà | Configurazione | Migration `InitialPhase1` |
|---|---|---|---|
| `Camper` | `byte[]? RowVersion` | `IsRowVersion()` in `CamperConfiguration` | ✅ colonna `rowversion` |
| `MaintenanceItem` | `byte[]? RowVersion` | `IsRowVersion()` in `MaintenanceItemConfiguration` | ✅ colonna `rowversion` |
| `Trip` | `byte[]? RowVersion` | `IsRowVersion()` in `TripConfiguration` | ❌ assente in `InitialPhase1`; presente nella migration Phase 2 `AddTripsAndTripStops` |
| `Expense` | `byte[]? RowVersion` | `IsRowVersion()` in `ExpenseConfiguration` | migration additiva `ActivateExpenseManagement` |
| `TripStop` | — | nessun token autonomo; aggiornato solo tramite il root Trip | N/A: protetto dal rowversion del padre |
| `Checklist` | — | figlia modificabile solo tramite il root Trip | migration additiva `ActivateTripChecklists` |
| `ChecklistItem` | — | figlia modificabile solo tramite Checklist/Trip | migration additiva `ActivateTripChecklists` |

`Trip` e `TripStop` appartengono alla **Phase 2** e sono nel `RoamlyDbContext` applicativo; `Trip.RowVersion` è creato dalla migration `AddTripsAndTripStops`, non da `InitialPhase1`. È una fasatura deliberata per R32, [`ADR-0009`](../adr/0009-test-strategy.md), non una divergenza fra modello e migration.

R49, [`ADR-0005`](../adr/0005-api-conventions.md), verifica il token sul **confine di concorrenza**: ogni aggregate root esposto da un endpoint di update ha un `rowversion`; i figli modificati esclusivamente attraverso quel root possono non avere token autonomo, purché ogni modifica aggiorni il padre nella stessa transazione `SaveChanges`. `TripStop` segue questa regola: `PUT /api/v1/trips/{tripId}` aggiorna sempre `Trip.UpdatedAtUtc`, anche se cambia solo un campo della tappa. Un endpoint indipendente su `TripStop` richiederebbe invece una sua migration/token prima della pubblicazione.

La stessa regola si applica alle checklist manuali: `Checklist` e `ChecklistItem` sono figli
dell'aggregate Trip, non hanno `RowVersion` e non espongono update autonomi fuori dal percorso
`/api/v1/trips/{tripId}/checklists/...`. `POST`, `PUT` e `DELETE` usano l'ETag del Trip; ogni
mutazione imposta `Trip.UpdatedAtUtc` nello stesso `SaveChanges`, così il token del padre avanza
atomicamente. Il `GET` della collection, il `GET` della checklist e
`GET /api/v1/trips/{tripId}/checklists/{checklistId}/items/{itemId}` restituiscono l'ETag del Trip
per le operazioni successive. La collection è `Page<ChecklistDetailsResponse>`, owner-scoped e
ordinata per `Checklist.Id`, con limite predefinito 50, massimo 200 e cursore keyset opaco; cursore
non valido → `400` con `type` `https://roamly.dev/problems/invalid-cursor`. La `Location` del `POST .../items`
identifica questa risorsa item e risolve nel relativo GET owner-scoped.

Le checklist manuali accettano i momenti numerici `0 Departure`, `1 Arrival`, `2 Storage`.
Le voci vengono ordinate da `SortOrder`; il riordino sostituisce l'elenco completo degli ID e
rifiuta duplicati o ID esterni alla checklist senza applicare modifiche. Entrambi i POST
(`.../checklists` e `.../items`) dichiarano `IdentityBased` (R50): stesso ID e contenuto
normalizzato restituiscono `200`, contenuto diverso `409`.

⚠️ `RowVersion` è `byte[]?` **nullable**: SQL Server valorizza sempre la colonna, ma il codice che genera l'ETag deve trattare il null come «nessun ETag» invece di emettere un header vuoto. Il token restituito è un ETag forte `"<base64url RowVersion>"`; tutte le risorse e gli aggregate root che usano `RowVersionEtag` condividono questo formato.

---

## 3. Regola non negoziabile

Ogni endpoint che espone un `{id}` è owner-scoped. Il meccanismo non è a carico della slice: vedi **R1-R10** in `SECURITY.md` §2.2, [`ADR-0003`](../adr/0003-auth-and-ownership.md) e [`ADR-0004`](../adr/0004-privacy-and-erasure.md).

> **`{id}` è sempre un `Guid`** ([`ADR-0008`](../adr/0008-primary-key-strategy.md)), mai un intero. L'URL resta a **un solo segmento** — `OwnerId` **non compare mai nel path**: viene dal claim, e il lookup lato server è sempre sulla chiave completa `(OwnerId, Id)`. Un client non può nemmeno esprimere una richiesta per un altro proprietario, e il `404` uniforme di R7 è invariato.

Resta a carico della slice il **test di ownership** (lettura *e* scrittura), parte della definition of done.
