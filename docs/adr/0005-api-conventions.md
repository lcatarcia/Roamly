# ADR-0005 — Convenzioni API: error model, esiti di dominio, concorrenza, versioning, paginazione

- **Stato:** Accettato
- **Data:** 2026-09-28
- **Owner:** @hermes
- **Decisore:** utente (conferma esplicita sulle tre voci HIGH: *Error model*, *Concorrenza*, *Versioning*; accettazione in blocco delle quattro MEDIUM e della sotto-decisione sulla validazione)
- **Severità:** HIGH (breaking change, contratto di scrittura, superficie di sicurezza)
- **Chiude:** le **otto voci** `⬜` di [`API-CONVENTIONS.md`](../architecture/API-CONVENTIONS.md) §2 · la sotto-decisione *FluentValidation vs validazione nativa* lasciata aperta fra `API-CONVENTIONS.md` §2 e `ARCHITECTURE.md` §1 · l'anello aperto di `API-CONVENTIONS.md` §2 ↔ `DATA.md` §2 sul «dove» del `rowversion`
- **Non emenda nessun ADR precedente.** Usa ADR-0003, ADR-0004, ADR-0007, ADR-0008 e ADR-0009 come vincoli.

---

## Contesto

`API-CONVENTIONS.md` §2 elencava otto convenzioni da chiudere «prima del primo `MapPost`». Non erano otto scelte indipendenti: sono un unico contratto, e alcune vincolano le altre in modo che decidere nell'ordine sbagliato costringe a riscrivere la prima.

L'ordine in cui sono state istruite, e in cui compaiono qui, è quello delle dipendenze reali:

1. **Error model** — è la radice: definisce la forma di ogni risposta ≥ 400.
2. **Status code di dominio** — definisce il **perimetro** dell'error model, cioè quali esiti negativi *non* lo attraversano. Deciderlo dopo significherebbe cancellare metà dei `type` appena definiti.
3. **Validation errors** — è una specializzazione della 1 dentro il perimetro della 2.
4. **Concorrenza** — introduce due status (`412`, `428`) e due `type`: dipende da 1 e 2.
5. **Idempotency** — stesso punto della pipeline e stesso tipo di conflitto della 4.
6. **Versioning** — governa l'evoluzione di tutto ciò che sta sopra: decidere per prima una policy su un contratto che non esiste sarebbe teoria.
7. **Paginazione** — indipendente, salvo la forma d'errore su cursore invalido (dipende da 1).
8. **Filtering / sorting** — è la grammatica dei parametri della collection paginata: dipende da 7.

### Perché ora, e non dopo il primo endpoint

`src/Roamly.Api/Program.cs` è oggi un *Hello World* di cinque righe: nessun `AddProblemDetails`, nessun `UseExceptionHandler`, nessuna autenticazione, nessun `/api/v1`. **Tutto ciò che questo ADR prescrive costa zero adesso**, perché non c'è un endpoint da riscrivere. Tre delle otto voci hanno invece un costo di inversione asimmetrico e brutale: `If-Match` obbligatorio introdotto dopo che esistono client rompe ogni client (cioè, per la policy della voce 6, richiede `/api/v2`); una collection restituita come array nudo non diventa paginata senza `v2`; uno snapshot OpenAPI introdotto dopo N endpoint costringe ad accettare in blocco un contratto che nessuno ha mai revisionato pezzo per pezzo.

### Vincoli reali

Sviluppatore singolo, nessuna review esterna, **un solo client** (PWA, stesso repository, distribuita insieme al server), nessun consumatore di terze parti. Il costo che conta non è la CPU: è il numero di regole che una persona deve ricordare tra sei mesi. Per questo ogni voce di questo ADR porta con sé un **verificatore**, e per questo tre delle nove regole sono scritte per fallire su una **omissione** e non su un errore.

### Il reperto che ha semplificato la voce più delicata

L'istruttoria partiva dall'ipotesi che il `rowversion` fosse «solo dichiarato». **È falsa.** Verificato leggendo `src/`:

| Entità | POCO | `IsRowVersion()` | Migration `20260928095239_InitialPhase1` |
|---|---|---|---|
| `Camper` | `byte[]? RowVersion` | ✅ `CamperConfiguration` | ✅ colonna `rowversion` |
| `MaintenanceItem` | `byte[]? RowVersion` | ✅ `MaintenanceItemConfiguration` | ✅ colonna `rowversion` |
| `Trip` | `byte[]? RowVersion` | ✅ `TripConfiguration` | ❌ assente, **e correttamente** |
| le altre 11 entità owned | — | ❌ | ❌ |

`Trip` non è nella migration perché non è nel `RoamlyDbContext` di runtime: è registrata **solo** in `FullSchemaDbContext` (Phase 2+, **R32, ADR-0009**). Non è una migration disallineata dal modello: è la Phase 1 come progettata. Conseguenza operativa: **questo ADR non tocca `InitialPhase1`**, perché l'unico update di Phase 1 è `PUT /api/v1/campers/{id}` e `Campers.RowVersion` esiste già.

⚠️ `RowVersion` è `byte[]?` **nullable**. SQL Server valorizza sempre la colonna, quindi il null si osserva solo su un'entità mai persistita — ma il codice che genera l'ETag deve trattarlo come «nessun ETag» invece di emettere `W/""`.

---

## Fatti verificati e non verificati

Questa sezione è il contenuto informativo principale tanto quanto la decisione. Ciò che non è stato verificato è marcato come tale e **non** è usato per giustificare una scelta.

### ✅ Verificati

| Fatto | Conseguenza |
|---|---|
| `ProblemDetails`, `IProblemDetailsService`, `AddProblemDetails()`, `IProblemDetailsWriter`, `ProblemDetailsOptions.CustomizeProblemDetails`, `UseExceptionHandler()`, `UseStatusCodePages()`, `IExceptionHandler`, `TypedResults` esistono e sono documentati per `aspnetcore-10.0` | **Nessun pacchetto da aggiungere** per l'error model |
| Il `DefaultProblemDetailsWriter` risponde solo a `application/json`, `application/problem+json` e wildcard; su `application/xml` o `text/html` non scrive e attiva il fallback | Il fallback va previsto, non scoperto |
| La documentazione .NET 10 cita **RFC 9457** per `IProblemDetailsService` e in altri punti ancora RFC 7807 | Non è una contraddizione sostanziale (9457 obsoleta 7807, i membri sul filo sono gli stessi), ma **il verificatore deve asserire la forma sul filo, non «l'RFC»**: l'RFC non è osservabile in una risposta HTTP |
| `AddValidation()` (`Microsoft.Extensions.Validation`) esiste, usa DataAnnotations + `IValidatableObject` + un source generator, valida **prima** dell'handler e risponde `400` + `ProblemDetails` con `errors` per membro e path annidati (`Customer.ShippingAddress.Street`, `OrderItems[0].Description`) | È la base della voce 3 |
| 🔴 Modo di fallimento documentato da Microsoft: se il generatore non scopre il tipo, o si omette `AddValidation()`, «*No automatic validation runs. Invalid input reaches the endpoint handler*» e «*Missing metadata doesn't produce a runtime exception or log entry*» | È **la ragione per cui R48 è la condizione della voce 3**, non un contorno |
| Limitazione nota .NET 10 (risolta in .NET 11): gli attributi di validazione sono ignorati sui **value type nullable** quando si passa `null` (`dotnet/aspnetcore#67033`) | Tradeoff negativo accettato, vedi §*Tradeoff* |
| FluentValidation 12.1.1 è **Apache-2.0**; la sponsorship commerciale è richiesta ma **volontaria e non contrattuale** | L'alternativa B della sotto-decisione **non** era bloccata da una licenza: è stata scartata nel merito |
| .NET 10 **non ha alcun supporto built-in** per ETag/`If-Match` nelle Minimal API. Esistono `TypedResults.PreconditionFailed()` (412) e `TypedResults.PreconditionRequired()` (428) | La voce 4 va scritta a mano: ~60 righe di endpoint filter |
| `Id` è un `Guid` COMB, PK composita **`(OwnerId, Id)` clustered**, e il COMB **ordina monotonicamente nell'ordinamento SQL Server** ([`ADR-0008`](0008-primary-key-strategy.md)) | Un cursore keyset su `(OwnerId, Id)` è servito dall'indice clustered: **nessun indice nuovo**, quindi **R42, ADR-0008** è soddisfatta senza deroghe |

### ⚠️ Non verificati — dichiarati, mai spacciati per fatti

| Affermazione | Come va trattata | Chi la chiude |
|---|---|---|
| Il `DefaultProblemDetailsWriter` aggiunge `traceId`/`requestId` alle `Extensions` **senza** `CustomizeProblemDetails` | **Fonti discordanti.** La decisione della voce 1 è costruita in modo da **non dipendere dall'esito**: il corpo si costruisce esplicitamente, quindi il default è irrilevante | — (neutralizzata da R46) |
| La generazione **offline** del documento OpenAPI in CI (`Microsoft.Extensions.ApiDescription.Server`) è praticabile in questa configurazione | **Non verificata.** Per questo **R51 è L0 se praticabile, L2 altrimenti**, e il fallback non cambia la decisione, solo il costo del verificatore | **@vulcan** |
| `Microsoft.Extensions.Validation` è già nel framework condiviso di `Microsoft.NET.Sdk.Web` su `net10.0` | Alta confidenza, **non confermata con un build** | **@vulcan**, con il primo build del passo 6 |

> Nessuna delle tre, se smentita, cambia una decisione di questo ADR. Cambiano dettagli di implementazione o il livello di un verificatore.

---

## Le otto decisioni

Per ciascuna: **cosa è stato deciso**, **il motivo dominante** (perché le alternative perdono, non l'elenco dei pro e contro), **dove si implementa**, **la regola** con verificatore e livello.

> I livelli usano la tassonomia di [`ADR-0009`](0009-test-strategy.md): **L0** = nessun database, millisecondi; **L1** = SQL Server reale; **L2** = `WebApplicationFactory` sopra il container di L1. Cinque delle nove regole hanno il loro verificatore osservabile **solo** su una risposta HTTP reale: forzarle a L1 violerebbe **R31, ADR-0009** («il livello più economico che può diventare rosso»). **Sette delle nove hanno comunque una componente L0 bloccante**, che è la parte che impedisce la regressione prima della CI.

---

### 1. Error model — `ProblemDetails` **costruito**, non ereditato

**Decisione.** `AddProblemDetails(CustomizeProblemDetails)` + `IExceptionHandler` tipizzati + una **factory esplicita** in `Common/Http/` che è l'unico luogo che costruisce un corpo d'errore. `detail`, `instance` e `errors` **non sono mai popolati** su un `404`.

**Motivo dominante: R7 (ADR-0003) non è verificabile su un corpo che non controlliamo.** ADR-0003 chiede che il `404` di ownership sia indistinguibile da quello di una risorsa inesistente. Ereditare quel corpo da un default significa fondare una regola di sicurezza su un comportamento che oggi non è verificato (§*Fatti*) e che può cambiare in un minor version. Costruire il corpo in un punto solo trasforma una promessa in un invariante — lo stesso ragionamento che ha prodotto R44 in `ARCHITECTURE.md` §4.

**Alternative scartate**

| | Perché perde |
|---|---|
| **A — solo `AddProblemDetails()` + `UseExceptionHandler()` + `UseStatusCodePages()`, default** | `UseStatusCodePages` riempie **solo le risposte prive di corpo**. Un `TypedResults.NotFound()` senza corpo diventa `ProblemDetails`; uno *con* corpo resta com'è. Risultato: **due forme di 404 nella stessa applicazione**, e quale delle due si ottiene dipende da come è scritto l'endpoint. È precisamente l'oracolo che **R7, ADR-0003** vieta — non nell'intenzione, ma nell'osservabile. In più il corpo dipenderebbe da un default non verificato |
| **C — formato proprietario `{ code, message, fields }`** | Contraddice `API-CONVENTIONS.md` §2, perde l'integrazione con `IProblemDetailsService` e con la validazione nativa (che emette `HttpValidationProblemDetails`). Nessun beneficio in cambio |
| **D — `ProblemDetails` senza `type`** | `type` è il solo campo machine-readable di RFC 9457. Senza, il client discrimina sul testo di `title`, che è localizzato: si rompe alla prima traduzione (**R21, ADR-0007**) |

I tre campi vietati non sono pignoleria: sono le tre porte da cui l'oracolo rientra. `instance` è innocuo se contiene il path, ma diventa un oracolo il giorno in cui qualcuno ci mette l'id canonico della risorsa *trovata*; `detail` è il campo che invita a scrivere «camper 3f2a… non trovato» oppure «non sei il proprietario»; `errors` presente in un caso e assente nell'altro è un oracolo nel campo più facile da non notare.

```csharp
// Common/Http/ProblemFactory.cs — unico luogo che costruisce un corpo d'errore.
public static ProblemDetails NotFound() => new()
{
    Type   = "https://roamly.app/problems/not-found", // stabile, mai localizzato
    Title  = "Risorsa non trovata",                   // costante, non dipende dal caso
    Status = StatusCodes.Status404NotFound,
    // NIENTE Detail, NIENTE Instance: entrambi rifletterebbero l'id richiesto.
};
```

**Dove si implementa**

| Pezzo | Luogo |
|---|---|
| Registrazione e opzioni | `Program.cs` — `AddProblemDetails(...)`, `UseExceptionHandler()`, `UseStatusCodePages()` |
| Eccezioni non gestite e `OwnershipViolationException` → `500` + evento di sicurezza | **`IExceptionHandler`** in `Common/Http/` |
| Costruzione dei corpi 4xx | **factory** in `Common/Http/`, chiamata dall'endpoint |
| Handler | **niente.** L'handler non conosce `ProblemDetails` |

**Regole**

| Regola | Enunciato | Verificatore | Livello |
|---|---|---|---|
| **R45, ADR-0005** | **Un solo corpo d'errore.** Ogni risposta con status ≥ 400 emessa dall'API ha `Content-Type: application/problem+json` e un corpo `ProblemDetails`. Nessun endpoint restituisce un corpo d'errore di altra forma, né un 4xx/5xx senza corpo | **L2 (bloccante):** `[Theory]` che percorre ogni endpoint mappato (`EndpointDataSource`) con input che ne forzano 400/401/404 e asserisce content-type e schema. **+ L0:** nessun tipo sotto `Features/` dichiara `Produces`/`ProducesProblem` con un tipo d'errore diverso da `ProblemDetails` | **L2 + L0** |
| **R46, ADR-0005** | **Nessun corpo d'errore è funzione dello stato del database.** `detail` e `instance` non sono mai popolati sui 4xx di lookup; l'insieme delle chiavi di `Extensions` è una **lista chiusa dichiarata in un punto unico** e identica per tutti i 404 | **L0:** unit test sulla factory — `NotFound()` produce `Detail == null`, `Instance == null`, `Extensions.Keys` uguale alla costante dichiarata. Nessun database, ~1 ms. **+ L2:** il confronto di R7 avviene sul corpo privato di quella lista chiusa | **L0 + L2** |

> **R46 è la regola che rende R7 (ADR-0003) verificabile invece che promessa.** R7 dice «byte-identico» senza dire *di cosa*; R46 nomina i tre campi da cui l'oracolo entrerebbe e chiude l'insieme delle extension.
>
> ⚠️ **Nota per @sentinel, non risolta qui.** Un `traceId` derivato da `Activity.Current` non è un oracolo di esistenza — è per-richiesta, identico in forma fra un 404 genuino e uno di ownership, e non correlabile a un id di risorsa. Ma **viola la formulazione letterale** di R7, perché due risposte consecutive differiscono comunque. R7 andrebbe letta come «identica a meno della lista chiusa di extension per-richiesta», e il test L2 deve confrontare il corpo **dopo** aver rimosso quella lista. Se confrontasse i byte grezzi sarebbe rosso per un motivo che non è quello che deve intercettare, e verrebbe indebolito invece che capito. **La riformulazione di R7 è di @sentinel: questo ADR non la scrive.**

---

### 2. Status code di dominio — `DomainResult<T>` nel ritorno dell'handler

**Decisione.** Un `DomainResult<T>` di dominio come tipo di ritorno di ogni `HandleAsync`, più un envelope uniforme per gli esiti negativi: `{ "outcome": "rejected", "reasons": [{ "code": "...", "message": "..." }] }`, servito con HTTP `200`.

**Motivo dominante: senza un tipo di ritorno che lo imponga, questa regola non è verificabile.** È l'unica delle otto in cui la convenzione riguarda il **codice dell'handler** e non l'HTTP, e quindi l'unica in cui un documento da solo non basta: il *principio* («camper non compatibile con il percorso» → `200`, non `400`) è già in vigore in `API-CONVENTIONS.md` §2 e non viene riaperto; è la *forma* che mancava. `DomainResult<T>` è il minimo che rende la regola automatizzabile, e non introduce indirezione: è un tipo di ritorno, non un layer.

**Alternative scartate**

| | Perché perde |
|---|---|
| **A — ogni slice inventa il suo response model di esito** | È letteralmente il caso che produce tre forme diverse in tre slice. La regola resterebbe una nota in un documento |
| **C — `200` con il response model normale e un campo `warnings[]` opzionale** | Un esito negativo diventa un campo che il client può ignorare. Su «camper non compatibile con il percorso» significa mostrare un piano di viaggio impraticabile senza dirlo. Perde anche **R17/R25, ADR-0007**: un esito che il design system deve rendere visibile non può essere opzionale |
| **D — `422 Unprocessable Content`** | Contraddice `API-CONVENTIONS.md` §2, già in vigore. E un `4xx` in TanStack Query finisce nel ramo `error`: un esito di dominio legittimo diventerebbe un toast d'errore |

```csharp
public sealed class CheckRouteCompatibilityHandler(RoamlyDbContext db)
{
    public async Task<DomainResult<RouteCompatibilityResponse>> HandleAsync(
        CheckRouteCompatibilityQuery q, CancellationToken ct) { /* ... */ }
}

// Endpoint.cs — l'unica traduzione esito → HTTP
return result switch
{
    { IsSuccess: true }  => TypedResults.Ok(result.Value),
    { IsRejected: true } => TypedResults.Ok(Envelope.Rejected(result.Reasons)), // 200, non 400
    _                    => TypedResults.NotFound(ProblemFactory.NotFound()),
};
```

**Dove si implementa.** **Handler** (tipo di ritorno) + **endpoint** (traduzione a `TypedResults`). **Non** un endpoint filter: un filter non conosce la semantica dell'esito. **Non** il modello EF. È una delle due voci che `ARCHITECTURE.md` §1 conta fra i concern HTTP ma che vive nell'handler.

**Regola**

| Regola | Enunciato | Verificatore | Livello |
|---|---|---|---|
| **R47, ADR-0005** | **Un esito di dominio non è un'eccezione e non è un `4xx`.** Ogni metodo `HandleAsync` sotto `Features/` restituisce `Task<DomainResult<T>>`; nessun handler lancia un'eccezione per esprimere un esito di dominio previsto. Gli esiti negativi viaggiano su `200` con l'envelope dichiarato | **L0 (bloccante)** in `Roamly.Model.Tests/Conventions/`: riflessione su tutti i tipi `*Handler` sotto `Features/` — il tipo di ritorno di `HandleAsync` è `Task<DomainResult<...>>`. **Riusa la stessa scoperta per riflessione di R44** (`ARCHITECTURE.md` §4): costa ~10 righe in più, non un test nuovo. **+ L2** su un caso di riferimento: esito negativo → `200`, non `400` | **L0 + L2** |

---

### 3. Validation errors — `HttpValidationProblemDetails` nativo, chiavi sui nomi JSON

**Decisione.** `400` + `ProblemDetails` con `errors: { "campo": ["msg", …] }` — cioè `HttpValidationProblemDetails`, la forma che .NET emette nativamente. Le chiavi sono i **nomi dei campi come appaiono nel JSON di richiesta**, non i nomi C#.

**Sotto-decisione collegata, presa insieme a questa: validazione nativa .NET 10** (DataAnnotations + `IValidatableObject` + source generator), **condizionata all'esistenza di R48 dal primo giorno**.

**Motivo dominante (forma del corpo): è la sola forma che la piattaforma produce da sola.** Qualunque altra va costruita a mano in ogni ramo che valida — si paga sempre, per sempre — contro un beneficio (un `code` machine-readable per campo) che oggi non ha un consumatore: il frontend è **una sola** applicazione, scritta da noi, con l'i18n già gestito lato client.

**Motivo dominante (libreria): la decisione #5 ha appena stabilito che Roamly preferisce meno indirezione e meno dipendenze quando la piattaforma basta.** La validazione nativa è la stessa scelta nello stesso spirito, presa dieci giorni dopo. **La condizione non è negoziabile:** il fallimento silenzioso documentato da Microsoft — metadati mancanti, nessuna eccezione, nessun log, input invalido che arriva all'handler — è *esattamente* il modo in cui questo repository ha già prodotto regole inerti. Adottare quel meccanismo **senza** il suo verificatore sarebbe ripetere l'errore con un nome nuovo.

**Alternative scartate**

| | Perché perde |
|---|---|
| **B — array piatto `errors: [{ field, code, message }]`** | Va costruito a mano in ogni caso e si perde l'allineamento con l'output nativo. Il `code` per campo non ha oggi un consumatore |
| **C — dizionario + `code` per voce (ibrido)** | Non è la forma nativa: richiede un writer custom per un vantaggio ipotetico |
| **FluentValidation 12.1.1** (seconda scelta pienamente difendibile) | Scartata nel merito, **non per la licenza** (Apache-2.0, verificata). Ha un vantaggio oggettivo che va riportato: **un `AbstractValidator<T>` si testa a L0 senza alcun host**, mentre le regole native si verificano naturalmente a L2 — un argomento reale sotto **R31, ADR-0009**, ma non decisivo contro «zero dipendenze, zero licenze da sorvegliare, integrazione nativa con `ProblemDetails`» |
| **Entrambe** (nativa per la forma, FluentValidation per il dominio) | **Due meccanismi che possono fallire in silenzio invece di uno**, e due punti in cui un request model può restare scoperto. Raddoppia R48 |
| **Validazione a mano nell'handler** | Collide con la voce 2: gli errori di input si mescolerebbero agli esiti di dominio. E nessun verificatore è possibile |

⚠️ **Due interazioni da non perdere.** Un `400` di validazione **ha** `errors`; un `404` **non ce l'ha** (R46, ADR-0005) — le due cose sono consistenti solo se nessuno «arricchisce» il 404 per simmetria, ed è R46 a vietarlo. E `OwnerId` **non è mai bindabile** da un request model (**R3, ADR-0003**): non è mai un campo validabile e non può comparire in `errors`. Se comparisse, sarebbe la prova che un request model lo espone.

**Dove si implementa**

| Pezzo | Luogo |
|---|---|
| Esecuzione della validazione | **endpoint filter** generato da `AddValidation()` |
| Dichiarazione delle regole | **`Validator.cs` dentro la slice** (struttura già prevista da `ARCHITECTURE.md` §4) |
| Forma del corpo | la factory della voce 1 |
| Handler | **niente:** riceve input già valido |

**Regola**

| Regola | Enunciato | Verificatore | Livello |
|---|---|---|---|
| **R48, ADR-0005** | **Nessun request model senza validazione attiva.** Ogni tipo usato come parametro di binding del corpo sotto `Features/` è coperto da un validatore **effettivamente in esecuzione**; la risposta a un input invalido è `400` + `ProblemDetails` con `errors` **chiavizzato sui nomi JSON dei campi** | **L0 (bloccante):** riflessione sui tipi `*Command`/`*Request` sotto `Features/` → ognuno ha i metadati generati di `[ValidatableType]`. **È il gemello di R44** e nasce dallo stesso modo di fallimento: metadati mancanti = nessun errore, nessun log, input invalido all'handler. **+ L2:** un caso per slice verifica lo `400` e le **chiavi** di `errors` — che vanno verificate, non assunte | **L0 + L2** |

---

### 4. Concorrenza — ETag debole dal `rowversion`, `If-Match` **obbligatorio**

**Decisione.** ETag **debole** (`W/"<base64url del RowVersion>"`) sul `GET` di singola risorsa; `If-Match` **obbligatorio** su `PUT`/`PATCH`/`DELETE` di risorsa aggiornabile. Header assente → **`428 Precondition Required`**; token non combaciante → **`412 Precondition Failed`**. `RowVersion` nullo ⇒ **nessun header `ETag`**, mai `W/""`.

**Il perimetro, che è la parte che chiude l'anello fra `API-CONVENTIONS.md` §2 e `DATA.md` §2:**

> Un endpoint di update esiste **solo** per entità che hanno un token di concorrenza nel modello. Aggiungere un `PUT`/`PATCH`/`DELETE` su un'entità che non ce l'ha richiede **prima** una migration che glielo aggiunga.

Oggi è soddisfatto a costo zero (`PUT /api/v1/campers/{id}` è l'unico update di Phase 1 e `Camper.RowVersion` esiste). È un **trigger**, non un lavoro. `Equipment`, `MaintenanceLog` e `OdometerReading` **non** hanno token: il giorno in cui uno di essi diventa aggiornabile, R49 diventa rossa prima che l'endpoint esista.

**Motivo dominante: il token esiste già nello schema e la PWA è multi-dispositivo per progetto.** Telefono in cantina e tablet in cucina sullo stesso camper è lo scenario normale, non l'eccezione. Non usare una colonna già pagata significa pagarla senza il beneficio.

**Alternative scartate**

| | Perché perde |
|---|---|
| **A — last-write-wins** | Costo zero, ma perde in silenzio una modifica in uno scenario dichiarato del prodotto, con il token già in tabella |
| **C — `If-Match` facoltativo** | **È A con una documentazione più lunga.** La differenza con B non è di rigore, è di **verificabilità**: con C non può esistere un test che diventi rosso quando un client smette di mandare l'header, perché l'assenza dell'header è **legittima** per definizione. Una regola che nessun test può far fallire è di nuovo «verde ma non in vigore» — il difetto che questo progetto sta cercando di smettere di produrre. `428` esiste esattamente per rendere impossibile il caso peggiore: un client che non sa di dover mandare l'header e sovrascrive senza accorgersene |
| **D — ETag come hash del corpo della risposta** | Funzionerebbe anche senza token, ma richiede un hash su ogni risposta e, lato scrittura, di rileggere e riserializzare per confrontare. Più costoso e meno preciso del token che il database già mantiene |

#### ⚠️ Le due trappole di implementazione — vanno lette prima di scrivere il codice

Senza queste due righe il pattern raccomandato **sembrerà una violazione**, e verrà aggirato con qualcosa di peggio.

1. **Praticamente ogni esempio online di questo pattern usa `db.Set<T>().FindAsync(id)`. In Roamly `Find`/`FindAsync` sono banditi** (**R5, ADR-0003**) perché bypassano i query filter di ownership. Il lookup **deve** essere una query filtrata normale. Copiare l'esempio dalla documentazione è qui un bug di sicurezza, non uno stilistico.
2. **`Entry()` non è bandito. Lo è `Entry().State = …`.** Quindi `db.Entry(entity).Property(e => e.RowVersion).OriginalValue = tokenDecodificato;` è **lecito**, ed è il modo corretto di iniettare il token del client prima di `SaveChangesAsync`, che solleverà `DbUpdateConcurrencyException` se qualcuno ha scritto nel frattempo.

> 🔴 **Prerequisito dichiarato, non risolto qui.** `BannedSymbols.txt` alla radice contiene sei righe (`DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow`, `DateTimeOffset.Now`, `UseSqlite`, `UseInMemoryDatabase`) e **nessuna** delle API di ownership elencate in `ARCHITECTURE.md` §4. **R5, ADR-0003 non è oggi in vigore in nessuna forma**, e nemmeno il suo verificatore L0 di secondo livello esiste in `tests/`. Finché è così, la distinzione fra `Entry().Property(...)` (lecito) e `Entry().State` (vietato) è applicata solo dalla lettura di questo paragrafo. **È dominio di @sentinel/@argus, Blocco 3.** Lo dichiaro perché è un prerequisito di questa voce, non un contorno.

**Dove si implementa**

| Pezzo | Luogo |
|---|---|
| Lettura di `If-Match`, `428` se assente, `412` se non combacia | **endpoint filter** (`Common/Http/ConcurrencyFilter.cs`) applicato al gruppo degli endpoint di update |
| Emissione dell'header `ETag` sul `GET` | **endpoint** (o filter di risposta) dal `RowVersion` del read model |
| Token di concorrenza | **modello EF** — `IsRowVersion()`, già presente |
| Confronto reale e `DbUpdateConcurrencyException` | **handler**, via `Entry(...).Property(...).OriginalValue` |

Una voce sola che tocca **filter + modello + handler**: è l'esempio più chiaro del perché un behavior di mediator non basterebbe (dovrebbe leggere un header) e perché nemmeno un solo filter basta (deve arrivare fino a EF).

**Regola**

| Regola | Enunciato | Verificatore | Livello |
|---|---|---|---|
| **R49, ADR-0005** | **Nessun update senza token.** (a) Ogni entità raggiunta da un endpoint `PUT`/`PATCH`/`DELETE` ha un token di concorrenza configurato con `IsRowVersion()`. (b) Ogni endpoint di update rifiuta con `428` una richiesta priva di `If-Match` e con `412` un token non combaciante | **L0 (bloccante):** su `FullSchemaDbContext.Model` — l'insieme delle entità esposte da endpoint di update è ⊆ dell'insieme delle entità con `IsConcurrencyToken` + `ValueGenerated.OnAddOrUpdate`. Nessun database: basta il modello EF in memoria, quindi **L0 e non L1** per **R31, ADR-0009**. **+ L2:** `PUT` senza header → 428; ETag stantio → 412; ETag corrente → 200 | **L0 + L2** |

> La parte (a) a L0 è ciò che rende impossibile la regressione peggiore: aggiungere un `PUT` su `Equipment` e scoprire mesi dopo che non c'era concorrenza.

---

### 5. Idempotency — nessun header: idempotenza dall'identità client-side

**Decisione.** Il client genera l'`Id` COMB (già previsto da [`ADR-0008`](0008-primary-key-strategy.md)) e lo manda. Un `POST` che collide sulla PK con una riga **identica per contenuto** restituisce `200` + la risorsa esistente invece di `409`. **Nessuno store, nessuna tabella, nessuna scadenza.** In più, ogni `POST` **dichiara** la propria politica come metadata.

**Motivo dominante: l'identità client-side rende lo store ridondante per tutti i casi che Roamly ha oggi.** ADR-0008 ha già pagato il prezzo del Guid COMB generato dal client per un altro motivo; la deduplicazione ne è un sottoprodotto gratuito.

Il caso reale non è il pagamento: **nessun POST di Phase 1 chiama un servizio a pagamento** (i candidati stanno nella decisione #8 sullo storage e nell'integrazione mappe, entrambe aperte). I casi reali sono due, e sono di pulizia: un doppio invio di `POST /api/v1/me/deletion` non deve produrre due `ErasureReceipt` né due date diverse — e **R23, ADR-0007** (le date vengono dal server) rende la seconda data **osservabile dall'utente**; e un doppio tap su rete lenta non deve creare due camper identici, su un'app usata in movimento e su rete mobile.

**Alternative scartate**

| | Perché perde |
|---|---|
| **A — nessuna idempotenza, se ne riparla col primo POST a pagamento** | Lascia il doppio-tap in creazione e lascia `/me/deletion` senza contratto proprio dove ADR-0004 è più sensibile |
| **C — `Idempotency-Key` completo con tabella di persistenza delle risposte (stile Stripe)** | **Sproporzionato**: sarebbe l'unico pezzo di infrastruttura di Roamly senza un caso d'uso in produzione, e l'unica tabella che non modella il dominio. Richiede retention e scadenze, e **tocca il grafo di cancellazione di `DATA.md` §6**, quindi tocca ADR-0004. Se l'utente la scegliesse in futuro, la severità salirebbe a HIGH e coinvolgerebbe @oracle e @sentinel |
| **D — `Idempotency-Key` solo su `/me/deletion`, senza store** | Un meccanismo per un endpoint solo: costa quanto la scelta adottata e copre meno |

**Il limite accettato:** questa scelta non copre il caso «stesso intento, payload diverso», e richiede di definire il confronto di contenuto. È accettabile perché il caso reale è il doppio tap, non il retry semantico.

**Dove si implementa**

| Pezzo | Luogo |
|---|---|
| Dichiarazione della politica per endpoint | **metadata sull'endpoint** — `.WithMetadata(new IdempotencyPolicy(...))` |
| Rilevazione della collisione e risposta `200` | **handler** (la `DbUpdateException` su PK) + traduzione nell'endpoint |

**Regola**

| Regola | Enunciato | Verificatore | Livello |
|---|---|---|---|
| **R50, ADR-0005** | **Ogni `POST` dichiara la sua politica di idempotenza.** Ogni endpoint mappato con `MapPost` porta un metadata `IdempotencyPolicy` esplicito, con valore fra `IdentityBased`, `NotRequired` o `KeyRequired`. Nessun default implicito | **L0 (bloccante):** enumerazione di `EndpointDataSource` — ogni endpoint POST ha il metadata. Il test **non** giudica *quale* politica sia giusta: obbliga a sceglierne una consapevolmente. **+ L2:** per gli endpoint `IdentityBased`, doppio POST con stesso `Id` e stesso corpo → stessa risorsa, nessun duplicato | **L0 + L2** |

> **R50 conta più della scelta stessa.** Il giorno in cui arriverà un POST a pagamento (decisione #8), il verificatore obbligherà a dichiarare una politica, e la decisione verrà presa **in quel momento, con il caso reale davanti**, invece di essere anticipata oggi su un'ipotesi. Passare a uno store persistito resta additivo: si aggiunge un header e un filter, gli endpoint esistenti continuano a funzionare.

---

### 6. Versioning — dentro `v1` si può solo **aggiungere**, e lo dimostra uno snapshot

**Decisione.** `v1` è immutabile per sottrazione: dentro una versione si può solo **aggiungere** (un campo opzionale, un endpoint nuovo). Rimuovere o rinominare un campo o un endpoint, restringere un tipo, rendere obbligatorio un parametro finora opzionale ⇒ `/api/v2`. La policy è applicata da uno **snapshot OpenAPI committato con diff bloccante in CI**. La deprecazione è un metadata OpenAPI sull'endpoint: il diff la rende visibile, e l'endpoint si rimuove quando il client non lo chiama più — verificabile, perché **il client è nel repo**. **Nessuna finestra temporale, nessun header `Sunset`**: sarebbero cerimoniale per un consumatore che non esiste.

**Motivo dominante: con un solo client il rischio non è l'incompatibilità, è l'invisibilità.** Client e server aggiornati insieme **nascondono** i breaking change — fino a quando non arriva la prima PWA installata che non si è aggiornata, e una PWA installata può restare a una versione vecchia per giorni. La domanda difficile del versioning («come sopravvivono i client vecchi?») in Roamly oggi non si pone; quella che si pone è **«come faccio ad accorgermi di aver rotto il contratto?»**.

**Lo snapshot è la risposta a quella domanda, e non alla successiva.** Non serve a mantenere due versioni: serve a **sapere** quando ne serve una seconda. Il diff compare nella pull request: si *vede* cosa cambia nel contratto, e la build fallisce se il cambiamento non è stato approvato aggiornando lo snapshot. È la stessa forma di R44 e R46 — applicato dalla macchina, non promesso dall'uomo.

**Alternative scartate**

| | Perché perde |
|---|---|
| **A — nessuna policy, `v1` per sempre** | Una PWA installata e non aggiornata vede il contratto cambiare sotto i piedi, e la cosa è invisibile finché non succede a un utente |
| **C — versioning per header o media type** | `API-CONVENTIONS.md` §3 ha già scelto il path. Riaprirlo non porta nulla a un'app con un client solo |
| **D — `Asp.Versioning` con version set e deprecazione** | Un pacchetto per gestire una **pluralità di versioni che non esiste**. **Risponde alla domanda successiva, non a quella attuale:** sa servire `v1` e `v2` insieme, ma non dice a nessuno che `v1` è cambiata |

**Dove si implementa**

| Pezzo | Luogo |
|---|---|
| Prefisso di versione | **routing** — un `MapGroup("/api/v1")` unico, non ripetuto per endpoint |
| Snapshot e diff | **CI** + un file committato (`docs/api/openapi-v1.json` o `tests/.../__snapshots__/`) |
| Deprecazione | metadata OpenAPI sull'endpoint |

**Regola**

| Regola | Enunciato | Verificatore | Livello |
|---|---|---|---|
| **R51, ADR-0005** | **Il contratto di una versione non si restringe mai.** Il documento OpenAPI di `/api/v1` è committato; ogni modifica che rimuove o rinomina un campo o un endpoint, restringe un tipo o rende obbligatorio un parametro finora opzionale fa fallire la build. Aggiungere è sempre lecito | **Approval test.** **L0 se la generazione offline è praticabile** (documento prodotto dal target di build, nessun database, < 5 s) — **altrimenti L2** (confronto fra `/openapi/v1.json` servito e lo snapshot committato: funziona ugualmente, costa un container). In entrambi i casi la PR che cambia il contratto deve aggiornare lo snapshot, **e il diff è la decisione** | **L0 se praticabile, L2 altrimenti** — conferma a carico di **@vulcan** |

> ⚠️ La praticabilità della generazione offline (`Microsoft.Extensions.ApiDescription.Server`) **non è verificata**. `Microsoft.AspNetCore.OpenApi` 10.0.x è MIT, ma il funzionamento del target di build in questa configurazione non è stato provato. Il fallback L2 non cambia la decisione, solo il costo del verificatore.

---

### 7. Paginazione — cursore keyset **opaco**, servito dalla clustering key

**Decisione.** `?limit=50&after=<cursore>`, risposta sempre `{ "items": [...], "nextCursor": "..." }`, `nextCursor: null` = fine. Il cursore è **opaco** (`Base64Url` di una stringa, senza firma: contiene un `Id` che il client già possiede). `limit` ha default **50** e massimo **200**. Un cursore malformato o non decodificabile è un **`400`** con `type` dedicato — non un `404`, non un `500` — ed è qui che questa voce dipende dalla voce 1.

**Motivo dominante: la forma della risposta è irreversibile, la paginazione no.** Nessuna collection di Phase 1 ha bisogno di essere paginata oggi (equipaggiamento di un camper, letture contachilometri: decine di righe l'anno). Ma restituire `{ items, nextCursor }` fin dal primo endpoint **costa una riga** e mantiene aperta l'opzione; restituire un array nudo la chiude e richiede `/api/v2` per riaprirla (voce 6). È il caso da manuale di decisione asimmetrica: costo nullo adesso, una versione maggiore dopo.

Il keyset è gratuito perché **`ORDER BY OwnerId, Id` è l'ordine di creazione ed è l'ordine della clustering key** (ADR-0008): il cursore è servito dall'indice clustered, **nessun indice nuovo**, quindi **R42, ADR-0008** non viene nemmeno sfiorata.

**Alternative scartate**

| | Perché perde |
|---|---|
| **A — nessuna paginazione, collection intere** | Per l'equipaggiamento sarebbe pure corretto, ma un endpoint che restituisce una lista nuda **non può** diventare paginato senza `v2`. E `OdometerReading` cresce per sempre |
| **B — offset/limit** | Su SQL Server `OFFSET n ROWS` scansiona e scarta n righe, e con inserimenti concorrenti le righe slittano fra una pagina e l'altra. Il salto a pagina N **non serve a nessuna UI di Roamly**: liste e infinite scroll, non tabelle con paginatore |
| **D — cursore trasparente (l'`Id` in chiaro)** | Congela nel contratto pubblico la scelta della chiave di ordinamento: cambiarla diventa breaking per la voce 6. Un cursore opaco costa una base64 e compra libertà per sempre |

⚠️ **Il limite del COMB, da dichiarare.** L'ordinamento gratuito vale solo per l'ordine **di creazione**. Una collection ordinata per un altro campo — `MaintenanceItem` per `NextDueOnUtc` — ha bisogno di un cursore **composito** `(NextDueOnUtc, Id)` sul suo indice. L'indice esiste (`IX_MaintenanceItems_OwnerId_NextDueOnUtc`) ma è **filtrato `[IsActive] = 1`**: copre la query **solo se** l'endpoint filtra sugli attivi.

> 🟠 **Il cursore si progetta sulla tabella degli indici, e quella tabella oggi è sbagliata.** `DATA.md` §7 nomina `OdometerReading (OwnerId, CamperId, ReadAtUtc DESC)` — la proprietà si chiama **`TakenOnUtc`**; conta come indici separati l'unique e il non-unique su `OdometerReading`, mentre nel codice **è un solo indice** (`HasIndex(OwnerId, CamperId, TakenOnUtc).IsDescending(false, false, true).IsUnique()`); e nomina `MaintenanceItem (OwnerId, NextDueAtUtc)` — la proprietà si chiama **`NextDueOnUtc`**, e l'indice reale ha un `HasFilter("[IsActive] = 1")` che `DATA.md` non documenta. **Una tabella di indici con i nomi sbagliati è una tabella su cui non si può progettare un cursore.** Le tabelle di ordinamento della voce 8 sono perciò derivate **dal codice**, non da `DATA.md`. La correzione di `DATA.md` §7 è **di @oracle** e non viene fatta qui.

**Dove si implementa**

| Pezzo | Luogo |
|---|---|
| Query keyset (`WHERE (OwnerId, Id) > cursore ORDER BY … TAKE limit+1`) | **handler** |
| Forma della risposta `Page<T>` | **response model** in `Common/` |
| Codifica/decodifica del cursore | helper in `Common/`, usato dall'handler |
| Validazione di `limit` | validazione del request model (voce 3) |

È la seconda voce che `ARCHITECTURE.md` §1 conta fra i concern HTTP ma che vive nell'handler: **la paginazione *è* la query**, non un wrapper attorno.

**Regola**

| Regola | Enunciato | Verificatore | Livello |
|---|---|---|---|
| **R52, ADR-0005** | **Nessuna collection nuda.** Ogni endpoint che restituisce più di una risorsa restituisce `Page<T>` (`items` + `nextCursor`), mai un array al primo livello del corpo. `limit` ha default e massimo dichiarati | **L0 (bloccante):** riflessione sui tipi di ritorno degli endpoint `MapGet` di collection — nessuno è `IEnumerable<T>`/`T[]` al primo livello. **+ L2:** una collection con N+1 elementi impaginata in due chiamate restituisce N+1 elementi distinti e `nextCursor: null` alla fine | **L0 + L2** |

---

### 8. Filtering e sorting — nessuna grammatica generica

**Decisione** (**LOW, chiusa da @hermes**, come consentito dalla DECISION SEVERITY dell'orchestratore). Ogni collection espone **il solo ordinamento servito dal suo indice**, fissato nell'endpoint e non negoziabile dal client. I filtri ammessi sono una **whitelist esplicita e tipizzata** di parametri dichiarati nel request model della slice, ognuno servito da un indice esistente. Un parametro non previsto è **ignorato** — non un `400`: ignorare mantiene la compatibilità in avanti della voce 6.

**Motivo dominante: non ci sono «più strategie valide fra cui scegliere», ce n'è una compatibile con le regole già in vigore.** `DATA.md` §7 stabilisce che nessun indice esiste senza una query che lo giustifichi, e **R42, ADR-0008** lo rafforza sulla clustering key. Qualunque grammatica generica — OData, RSQL, o un `?sort=` ad hoc che accetta un nome di campo qualsiasi — significa **per costruzione accettare query che nessun indice serve**, cioè **accettare un indice futuro che nessuno ha giustificato, attraverso il contratto API invece che attraverso una migration**. Su una collection con `OwnerId` come primo campo di ogni indice, un ordinamento arbitrario produce un sort di tutte le righe del tenant: oggi non si nota (decine di righe), si noterà quando non si potrà più tornare indietro senza breaking change.

Ordinamenti di Phase 1, **dagli indici reali letti nel codice** (non da `DATA.md` §7, per il motivo detto nella voce 7):

| Collection | Ordinamento fisso | Indice che lo serve |
|---|---|---|
| `Camper` | creazione (`Id` COMB) | clustered `(OwnerId, Id)` |
| `Equipment` | creazione (`Id` COMB) | clustered `(OwnerId, Id)` |
| `OdometerReading` | `TakenOnUtc` desc | `IX_…_OwnerId_CamperId_TakenOnUtc` (già `IsDescending` sul terzo campo) |
| `MaintenanceItem` | `NextDueOnUtc` asc | `IX_…_OwnerId_NextDueOnUtc`, **filtrato `[IsActive] = 1`** → l'endpoint **deve** filtrare sugli attivi perché l'indice copra |
| `MaintenanceLog` | creazione (`Id` COMB) | clustered `(OwnerId, Id)` |

Se l'utente ritenesse questa lettura di `DATA.md` §7 troppo stretta, la voce torna MEDIUM — ma allora va riaperto `DATA.md` §7, non questa decisione.

**Dove si implementa.** **Handler** (l'ordinamento è nella query) + whitelist tipizzata nel **request model** della slice. Nessun filter, nessun middleware, nessun parser.

**Regola**

| Regola | Enunciato | Verificatore | Livello |
|---|---|---|---|
| **R53, ADR-0005** | **Nessun ordinamento dinamico.** Nessun endpoint accetta un parametro che nomini una colonna, un campo o una direzione di ordinamento (`sort`, `orderBy`, `filter`, `$filter`, `q` generico). L'ordinamento è fissato nell'handler; i filtri sono proprietà tipizzate del request model | **L0 (bloccante):** riflessione sui request model e sui parametri di query degli endpoint — nessun parametro `string` il cui nome sia nella lista vietata, e nessun parametro passato a un'espressione di ordinamento costruita dinamicamente | **L0** |

---

## Sotto-decisioni minori, chiuse da @hermes (LOW)

| Sotto-decisione | Decisione |
|---|---|
| Naming dei `type` di `ProblemDetails` | URI stabili sotto `https://roamly.app/problems/{slug}`, mai localizzati, **non necessariamente risolvibili** |
| Encoding del cursore | `Base64Url` di una stringa opaca, **senza firma** — non contiene segreti, contiene un `Id` che il client già possiede. ⚠️ Se un giorno il cursore contenesse qualcosa che il client **non** possiede, diventerebbe materia di @sentinel |
| `limit` di default e massimo | **50 / 200**, modificabili senza breaking change |
| Forma dell'ETag | debole `W/"…"`, da `RowVersion` in `Base64Url`; `RowVersion` nullo ⇒ nessun header. Conseguenza tecnica della voce 4, non una scelta indipendente |
| `MapGroup` unico per `/api/v1` | sì, un solo gruppo, con auth e filtri comuni applicati lì |

---

## Regole invarianti — R45-R53

> Numerazione in continuità con **R1-R7** (`SECURITY.md` §2.2), **R8-R10** (ADR-0004), **R11-R25** (ADR-0007), **R26-R29** e **R40-R43** (ADR-0008), **R30-R39** (ADR-0009), **R44** (`ARCHITECTURE.md` §4, decisione #5 — handler registrati). **Nessuna regola esistente viene toccata.** Le citazioni vanno sempre qualificate nella forma `R45, ADR-0005`.

| # | Enunciato | Verificatore | Livello |
|---|---|---|---|
| **R45** | **Un solo corpo d'errore.** Ogni risposta ≥ 400 ha `application/problem+json` e un corpo `ProblemDetails`; nessun 4xx/5xx senza corpo, nessun corpo d'errore di altra forma | `[Theory]` su `EndpointDataSource` che forza 400/401/404 e asserisce content-type e schema; + controllo su `Produces`/`ProducesProblem` sotto `Features/` | **L2 + L0** |
| **R46** | **Nessun corpo d'errore è funzione dello stato del database.** `detail`/`instance` mai popolati sui 4xx di lookup; chiavi di `Extensions` = lista chiusa dichiarata in un punto unico | unit test sulla factory (`Detail == null`, `Instance == null`, `Extensions.Keys` = costante); + confronto R7 sul corpo privato della lista chiusa | **L0 + L2** |
| **R47** | **Un esito di dominio non è un'eccezione e non è un `4xx`.** Ogni `HandleAsync` sotto `Features/` restituisce `Task<DomainResult<T>>`; esiti negativi su `200` con l'envelope dichiarato | riflessione sui tipi `*Handler` (riusa la scoperta di R44); + un caso di riferimento HTTP | **L0 + L2** |
| **R48** | **Nessun request model senza validazione attiva.** Ogni tipo di binding del corpo sotto `Features/` è coperto da un validatore in esecuzione; input invalido → `400` + `errors` **chiavizzato sui nomi JSON** | riflessione sui `*Command`/`*Request` → metadati `[ValidatableType]` presenti; + un caso per slice che verifica status e chiavi | **L0 + L2** |
| **R49** | **Nessun update senza token.** (a) ogni entità con endpoint di update ha `IsRowVersion()`; (b) update senza `If-Match` → `428`, token stantio → `412` | sul modello EF in memoria: entità con endpoint di update ⊆ entità con token; + i tre casi HTTP | **L0 + L2** |
| **R50** | **Ogni `POST` dichiara la sua politica di idempotenza** (`IdentityBased` \| `NotRequired` \| `KeyRequired`). Nessun default implicito | enumerazione di `EndpointDataSource`; + doppio POST `IdentityBased` senza duplicati | **L0 + L2** |
| **R51** | **Il contratto di una versione non si restringe mai.** Snapshot OpenAPI di `/api/v1` committato; rimozione, rinomina, restrizione di tipo o parametro reso obbligatorio fanno fallire la build. Aggiungere è sempre lecito | approval test sullo snapshot | **L0 se la generazione offline è praticabile, L2 altrimenti** — conferma **@vulcan** |
| **R52** | **Nessuna collection nuda.** Ogni endpoint multi-risorsa restituisce `Page<T>` (`items` + `nextCursor`), mai un array al primo livello. `limit` ha default e massimo dichiarati | riflessione sui tipi di ritorno dei `MapGet` di collection; + impaginazione reale in due chiamate | **L0 + L2** |
| **R53** | **Nessun ordinamento o filtro dinamico.** Nessun parametro che nomini colonna, campo o direzione; ordinamento fissato nell'handler, filtri tipizzati nel request model | riflessione sui parametri di query e sui request model | **L0** |

> **Il prossimo numero libero è R54.** Nessun ADR successivo deve dedurre il primo numero da un conteggio: va letto qui.

**Sette delle nove regole hanno una componente L0 bloccante**, che è la parte che impedisce la regressione prima della CI. Le due che non ce l'hanno (R45 per la parte principale, R51 nel fallback) non sono osservabili senza una risposta HTTP reale: promuoverle a un livello più economico significherebbe scrivere un test che non può diventare rosso.

---

## Conseguenze

### Cosa diventa più facile

- **R7 (ADR-0003) diventa verificabile.** Oggi dice «byte-identico» senza dire di cosa; R46 nomina i tre campi e chiude l'insieme delle extension. Il test di R7 ha finalmente un oggetto definito.
- **Il `rowversion` smette di essere un rimando circolare.** `API-CONVENTIONS.md` §2 rimandava a `DATA.md`, che rimandava agli ETag dell'API: il «dove» esisteva solo nel codice. Ora è scritto, e la propagazione in `API-CONVENTIONS.md` §1.4 chiude l'anello.
- **Ogni endpoint nuovo nasce con la forma giusta senza che nessuno se ne ricordi**: un `POST` senza politica di idempotenza, un `GET` di collection che restituisce un array, un `PUT` su un'entità senza token sono **rossi a L0** prima di arrivare in CI.

### Cosa diventa più difficile — vincoli che questo ADR impone al futuro

1. **Una migration prima di ogni nuovo `PUT`.** Un endpoint `PUT`/`PATCH`/`DELETE` su `Equipment`, `MaintenanceLog` o `OdometerReading` **non è più una slice**: richiede prima una migration che aggiunga il token di concorrenza. R49(a) lo rende rosso a L0 nel momento in cui l'endpoint viene mappato, non dopo. È un attrito deliberato, non un effetto collaterale.
2. **Lo snapshot OpenAPI va mantenuto.** Ogni PR che cambia il contratto deve aggiornarlo, e il diff va letto invece che rigenerato per far passare la build. Uno snapshot rigenerato senza guardarlo è uno snapshot inutile: è l'unico punto di questo ADR in cui la disciplina umana resta necessaria, e va detto.
3. **R48 è la condizione della validazione nativa, non un suo complemento.** Se R48 non esiste dal primo giorno, la scelta della validazione nativa **va riaperta**, perché il meccanismo adottato fallisce in silenzio per progetto. Non è una raccomandazione: è la clausola sotto cui la sotto-decisione è stata accettata.
4. **`If-Match` è obbligatorio, quindi rimuoverlo è breaking.** Per la policy della voce 6, rilassare il `428` a un update permissivo è una restrizione del contratto dal punto di vista di chi si aspettava il rifiuto — e comunque una modifica di comportamento che lo snapshot rende visibile.
5. **Il perimetro di R53 si può allargare, mai stringere.** Aggiungere un filtro tipizzato o un ordinamento alternativo è **additivo** e quindi non breaking; è precisamente il motivo per cui stringere ora è la scelta a rischio minimo.

### Tradeoff negativi accettati

- La limitazione .NET 10 sui **value type nullable** (`dotnet/aspnetcore#67033`): un attributo di validazione su un `int?` è ignorato quando si passa `null`. Risolta in .NET 11; fino ad allora le regole su value type nullable vanno espresse in `IValidatableObject` e coperte a L2.
- Le regole di validazione native si verificano naturalmente a **L2**, mentre un `AbstractValidator<T>` si sarebbe testato a **L0** senza host. È un costo reale rispetto all'alternativa scartata, ed è il prezzo di non avere una dipendenza in più.
- Il client deve **conservare l'ETag** fra `GET` e `PUT`, e gestire **due forme di `200`** (esito positivo e envelope `rejected`).
- Il cursore opaco **non permette il salto a pagina N**. Nessuna UI di Roamly lo chiede, ma la porta è chiusa finché la forma della risposta resta questa.
- ~120 righe una tantum in `Common/Http/` (factory, `IExceptionHandler`, `ConcurrencyFilter`) che nessuna slice "possiede".

---

## Cosa NON entra in questo ADR

| Materia | Perché no | Owner |
|---|---|---|
| **Forma di `DELETE /api/v1/me/deletion` per l'utente non più autenticabile** | vedi §*Il rinvio motivato*, sotto | **@sentinel** (+ @hermes per la superficie) |
| Auth, ownership, 404-non-403, CORS, antiforgery | già chiuse da **ADR-0003**. Usate come vincolo, non riaperte | — |
| **`BannedSymbols.txt` privo delle API di ownership** | è il verificatore di **R5, ADR-0003**, non una convenzione API. Ma è **prerequisito** della voce 4 | **@sentinel** / **@argus**, Blocco 3 |
| **Nomi di colonna errati e conteggio degli indici in `DATA.md` §7** | documento di dati. Citato nella voce 7 perché il cursore si progetta su quella tabella | **@oracle** |
| **Riformulazione letterale di R7** («byte-identico» vs extension per-richiesta) | è una regola di sicurezza già accettata: non la riscrive @hermes | **@sentinel** |
| **Conteggio «sei concern HTTP»** in `ARCHITECTURE.md` §1 | due delle sei voci (paginazione e status code di dominio) vivono nell'handler, non in middleware: il numero difendibile è **cinque**. La conclusione della decisione #5 **non cambia** — nessuna delle otto voci richiede un behavior di dominio | **@archimedes** |
| Generazione offline del documento OpenAPI per R51; `Microsoft.Extensions.Validation` nel framework condiviso | verifiche di build e pipeline | **@vulcan** |
| Query plan del cursore keyset; copertura dell'indice filtrato di `MaintenanceItem` | indici e piani | **@oracle** |
| OpenAPI / Swagger UI, client TypeScript generato | tooling del passo 6, non convenzione | @hermes, dopo questo ADR |
| Rate limiting, lockout, throttling | superficie di sicurezza | **@sentinel** |
| Upload, multipart, URL firmati | dipende dalla **decisione #8** (storage), aperta | **@vulcan** |
| Read model del `Ribbon` | dipende da `CONTEXT.md` §3.9, dominio | **@archimedes** |
| Formattazione di `Money`, totali multi-valuta; resa visiva della differenza fra esito `200` negativo ed errore `4xx` (R17/R25, ADR-0007) | presentazione | **@pixel** |
| **R38, ADR-0009** («vedere ogni verificatore fallire») applicata alle nove regole nuove | strategia di test | **@argus** |

### Il rinvio motivato di `DELETE /api/v1/me/deletion`

`API-CONVENTIONS.md` §1.1 mette la forma di quell'endpoint in carico a @hermes **e** @sentinel. Non entra qui, per un motivo preciso: **il gap non riguarda il contratto HTTP.** Verbo, rotta, status code e `ProblemDetails` sono già derivabili dalle voci 1 e 2 di questo ADR. Il gap riguarda **come un utente che non può più autenticarsi normalmente raggiunge quell'endpoint** — e le tre opzioni reali (un login limitato valido solo per la revoca; un link firmato a scadenza inviato al momento della richiesta di cancellazione; un canale umano fuori banda) sono **tutte e tre decisioni di autenticazione**, con conseguenze su token, superficie d'attacco e privacy. Metterle qui significherebbe che la convenzione più banale dell'ADR — un `DELETE` che risponde `204` — trascina con sé la decisione di sicurezza più delicata del progetto. Sono cose di peso diverso e vanno decise in posti diversi.

**Cosa questo ADR fissa comunque**, senza toccare il meccanismo di accesso: rotta e verbo `DELETE /api/v1/me/deletion`; **`204 No Content` sia se una cancellazione era programmata sia se non lo era** (idempotente, politica `NotRequired` per R50); **nessun `404` se non c'è nulla da revocare**, perché sarebbe un oracolo sullo stato dell'account e R46 lo vieta; l'endpoint **non** richiede `If-Match`, non essendo l'update di una risorsa con token.

**Cosa resta a @sentinel:** il meccanismo di accesso. **Blocca** il rilascio della schermata «Elimina account»; **non blocca** il passo 6 della ROADMAP.

---

## Riferimenti

Documenti aggiornati contestualmente a questo ADR:

- [`architecture/API-CONVENTIONS.md`](../architecture/API-CONVENTIONS.md) — le otto voci di §2 passano da `⬜` a ✅; §1.4 dichiara dove vive il `rowversion`; le due note esistenti rimandano a R46 e R47.
- [`README.md`](../README.md) — indice degli ADR.

Documenti usati come vincolo e **non** modificati: [`ADR-0003`](0003-auth-and-ownership.md) (R1-R7), [`ADR-0004`](0004-privacy-and-erasure.md) (R8-R10), [`ADR-0007`](0007-design-system.md) (R11-R25), [`ADR-0008`](0008-primary-key-strategy.md) (R26-R29, R40-R43), [`ADR-0009`](0009-test-strategy.md) (R30-R39, tassonomia L0-L2), [`architecture/ARCHITECTURE.md`](../architecture/ARCHITECTURE.md) §1 e §4 (R44), [`architecture/SECURITY.md`](../architecture/SECURITY.md) §2.2, [`architecture/DATA.md`](../architecture/DATA.md) §2 e §7, [`architecture/TESTING.md`](../architecture/TESTING.md).

Fonti esterne: Microsoft Learn `aspnetcore-10.0` (`ProblemDetails`, `IProblemDetailsService`, `IExceptionHandler`, `UseStatusCodePages`, `AddValidation`, `TypedResults`), RFC 9457, `dotnet/aspnetcore#67033`, NuGet e repository GitHub di FluentValidation 12.1.1 (licenza Apache-2.0).
