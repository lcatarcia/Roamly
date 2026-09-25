# Roamly — Convenzioni API

Stato: **bozza — da completare prima del primo endpoint** · Ultimo aggiornamento: 2026-09-24
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

---

## 2. Convenzioni da definire prima del primo endpoint

Il planning elencava gli endpoint ma non ciò che rende un'API mantenibile. Queste convenzioni vanno chiuse **prima** di scrivere il primo `MapPost`, perché cambiarle dopo è un refactor trasversale.

| Area | Requisito | Stato |
|---|---|---|
| **Error model** | `ProblemDetails` (RFC 9457) come formato unico di errore | ⬜ |
| **Validation errors** | formato `errors[]` per campo, generato da FluentValidation | ⬜ |
| **Paginazione** | `GET /trips`, `GET /expenses` cresceranno: cursor vs offset, scelta unica | ⬜ |
| **Filtering / sorting** | convenzione unica per tutte le collection | ⬜ |
| **Concorrenza** | ETag / `If-Match` sugli update, coerente con il `rowversion` di `DATA.md` | ⬜ |
| **Idempotency** | idempotency key sui POST che generano costi/spese | ⬜ |
| **Versioning** | `/api/v1` è in URL, ma manca la policy di breaking change e deprecazione | ⬜ |
| **Status code di dominio** | esito negativo di dominio ≠ errore HTTP | ⬜ |
| **Ownership** | risorsa non posseduta → **`404`** con `ProblemDetails` **byte-identico** a quello di una risorsa inesistente; `403` riservato alle capability future | ✅ [`ADR-0003`](../adr/0003-auth-and-ownership.md) |
| **Auth** | cookie `httpOnly`/`Secure`/`SameSite=Lax`, antiforgery su non-GET, CORS allowlist con credenziali | ✅ [`ADR-0003`](../adr/0003-auth-and-ownership.md) |

### Nota sull'ownership: perché 404 e non 403

Un `403` **confermerebbe che l'id esiste** — un oracolo di esistenza gratuito, anche con `Guid`. E con il query filter attivo l'handler **non è nemmeno in grado** di distinguere "inesistente" da "di un altro": rispondere `403` richiederebbe di disattivare il filtro per scoprirlo, cioè indebolire la protezione per migliorare un messaggio d'errore.

Vincoli che rendono la scelta reale invece che dichiarata: `type` e `title` identici, nessun dettaglio aggiuntivo, e nessuna differenza osservabile nel tempo di risposta. La distinzione vive **solo nei log** (`ownership_miss` con `requestedId` e `actualOwnerId`).

### Nota sugli status code di dominio

Caso di riferimento: **camper non compatibile con il percorso**.

Non è un errore del client: è una **risposta di dominio valida**. Deve essere `200` con esito negativo strutturato, **non** `400`.

Questa distinzione va applicata in modo sistematico: `4xx` per input/permessi sbagliati, `200` per risposte di dominio negative.

---

## 3. Regola non negoziabile

Ogni endpoint che espone un `{id}` è owner-scoped. Il meccanismo non è a carico della slice: vedi **R1-R10** in `SECURITY.md` §2.2, [`ADR-0003`](../adr/0003-auth-and-ownership.md) e [`ADR-0004`](../adr/0004-privacy-and-erasure.md).

> **`{id}` è sempre un `Guid`** ([`ADR-0008`](../adr/0008-primary-key-strategy.md)), mai un intero. L'URL resta a **un solo segmento** — `OwnerId` **non compare mai nel path**: viene dal claim, e il lookup lato server è sempre sulla chiave completa `(OwnerId, Id)`. Un client non può nemmeno esprimere una richiesta per un altro proprietario, e il `404` uniforme di R7 è invariato.

Resta a carico della slice il **test di ownership** (lettura *e* scrittura), parte della definition of done.
