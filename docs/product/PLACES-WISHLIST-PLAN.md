# Da visitare: piano della prima versione

Stato: vista lista/mappa personale implementata e revisionata; integrazione non ancora attivata sull'host. Piano iniziale: 2026-11-03; aggiornamento del perimetro: 2026-11-06.

## Aggiornamento locale — 2026-11-11

La vista iniziale di «Da visitare» diventa **Mappa**; Lista resta selezionabile.
L'apertura carica direttamente la mappa secondo il percorso personale esistente,
anche quando la raccolta e' vuota. Nessuna modifica ai controlli del provider.

Nei form create/edit le coordinate estratte automaticamente dai link Google Maps
sono arrotondate a un massimo di **tre decimali**, senza zeri finali obbligatori:
per Bunker Soratte, **42.237, 12.512**. Cambio link, svuotamento e reset continuano
a seguire i valori automatici arrotondati; i campi modificati manualmente e le
posizioni gia' salvate non vengono arrotondati o sovrascritti. Parser esatto,
fallback storico in lettura, precisione SQL e API restano invariati. Link brevi
o senza coordinate esplicite richiedono ancora inserimento manuale, senza fetch.
Questo aggiornamento supera il default Lista e la precisione automatica del form
descritti sotto; non e' ancora distribuito.

## Aggiornamento richiesto e implementato localmente — 2026-11-10

Richiesta esplicita: «vorrei che quando si inserisce il link di ggogle maps poi tali coordinate vengano estratte automaticamente per visualizzare il pin sulla mappa. Inoltre vorrei vedere la mappa subito disponibile senza autorizzare la visualizzazione in più procedimenti».

Questo aggiornamento **supera**, per la vista personale, le precedenti scelte di link
opaco/no estrazione e consenso manuale Maps riportate nelle sezioni storiche sotto.
Lista resta il default; scegliere Mappa carica direttamente SDK/CSS e canvas, anche
senza luoghi o coordinate, con vista iniziale Italia e avviso non bloccante. Niente
GET/POST al registro all'apertura, niente grant automatici o azioni tecniche chiamate consenso.
Resta una breve informativa sul trasferimento IP/viewport all'endpoint Azure EU;
nomi e note sono overlay locali, non query Search. Attribuzione, accessibilità senza
geocodifica implicita, telemetria disabilitata e teardown/abort restano invariati.

L'estrattore locale accetta un solo luogo esplicito `!3d…!4d…`, oppure una coppia
numerica `q` o `api=1&query`, da host allowlisted `google.{dominio}`,
`www.google.{dominio}` e `maps.google.{dominio}` (com, it, fr, de, es, co.uk, nl, pt,
ch, at, be, ca, com.au, co.jp). Un solo decode standard per componente, URL HTTPS
senza userinfo, coordinate entro range; niente supposizioni da `@`, fetch, redirect,
espansione short-link o import di nomi. Formati ambigui/non supportati restano
salvabili come link e richiedono coordinate manuali.

Create/edit mostrano coordinate correggibili: un cambio link aggiorna o svuota
soltanto i campi automatici; un campo toccato manualmente resta protetto fino alla
chiusura/reset del form. Le posizioni già salvate sono trattate come manuali,
senza indovinarne la provenienza. API create/update derivano solo con posizione nulla.
GET/list derivano la posizione effettiva dei vecchi luoghi senza modificarli né
alterare RowVersion/ETag. Per Bunker Soratte si usano **42.2370252, 12.5117658**,
non la viewport **42.2370291, 12.5091909**. Nessun backfill o migration.
La precisione SQL esistente resta a sei decimali per le coordinate persistite:
l'estrattore e i vecchi luoghi con fallback in lettura mantengono le cifre del link;
il replay di una nuova creazione confronta alla precisione SQL, evitando conflitti
spuri dopo l'arrotondamento del database.

L'emissione token personale conserva autenticazione, CSRF, rate limit, `no-store`,
managed identity e controllo account sotto lock prima/dopo Entra (nessuna rete sotto
lock), incluse cancellazione e password obbligatoria. La sospensione economica
persistita continua a negare l'accesso; 1 EUR non è un hard cap. Registro storico,
export/erasure e percorso rigoroso MapsAdmissionService restano invariati.

**Solo checkout locale:** nessun commit/push/deploy, mutation cloud, migration applicata
al database applicativo o traffico Maps reale. I rilasci e le approvazioni precedenti
qui sotto non attestano questa modifica.

L'utente ha scelto mappa ed elenco sincronizzati, poi Azure Maps, e ha delegato le scelte restanti privilegiando il costo in denaro. Le decisioni seguenti esercitano tale delega sul piano; non sono una validazione d'usabilità né un'autorizzazione a provisioning, spese o trasferimenti di dati. La decisione geo #3 resta aperta fino a spike e ADR.

## Aggiornamento: percorso personale semplice — 2026-11-06

L'utente ha chiesto esplicitamente «meno burocrazia e piu' azioni», precisando che usera' l'app soltanto lui, e ha confermato di procedere. Per questa implementazione si privilegia quindi il percorso minimo funzionante: lista predefinita, mappa Azure caricata volontariamente, pin distinti per categoria e label con `SavedPlace.Name`. Non si richiedono ulteriori approvazioni per le normali scelte implementative e non si aggiungono servizi o controlli preventivi.

Il controllo economico rigoroso gia' implementato resta disponibile e non viene indebolito internamente, ma **non diventa un prerequisito della vista personale**: le risposte Cost Management vuote o ritardate non possono provare il suo contratto di copertura completa. La soglia di **1 EUR/mese resta un riferimento operativo**, non una promessa di sospensione automatica o un tetto alla fattura. Nessuna osservazione mancante viene trasformata in costo zero. Il nuovo percorso e il relativo limite devono risultare espliciti nella configurazione e nella documentazione tecnica; non presentare il gate rigoroso come attivo.

Restano autenticazione e ownership esistenti, niente chiavi Maps nel browser, informazione e consenso volontario al primo uso tramite il registro Identity gia' presente, revoca e lista utilizzabile senza provider. Non si aggiungono un secondo registro, un proxy geografico, un job o un'altra migration. La semplificazione non autorizza deploy, modifiche cloud ulteriori, applicazione di migration o prove Maps a consumo in questa sessione.

Le sezioni successive conservano le decisioni e le verifiche precedenti: dove richiedono il gate economico fail-closed prima di ogni apertura, descrivono il percorso rigoroso, non un blocco obbligatorio per l'uso personale ora richiesto. Spike/ADR geografici generali, Search e routing camper restano fuori da questa integrazione manuale.

### Integrazione realizzata

Lista predefinita e selettore Lista/Mappa mantengono il layout esistente. La vista mappa usa il Web SDK Azure caricato localmente e solo su richiesta dopo il consenso, quattordici simboli di categoria, label con il nome salvato, legenda, selezione accessibile e dettagli. La paginazione resta condivisa: conteggi espliciti dei luoghi caricati e posizionati, senza inventare coordinate per quelli incompleti.

`GET /api/v1/maps/config` e `POST /api/v1/maps/token` riusano sessione, CSRF, limiti API e registro Identity. Il provider credenziali usa managed identity; account e consenso sono verificati prima dell'acquisizione remota e ricontrollati sotto lock prima del rilascio, senza rete sotto lock SQL. L'emissione personale rispetta eventuali sospensioni economiche persistite, senza presentare il gate rigoroso o un osservatore costi come attivi.

Con configurazione assente la feature resta disabilitata e la finalita' resta DRAFT-1. Con configurazione valida viene usata la definizione v1 del registro esistente. Parametri `Maps__Enabled`, `Maps__AccountClientId` e `Maps__ManagedIdentityClientId`, istruzioni e limiti sono nel [README](../../README.md). Nessuna nuova migration richiesta da questa integrazione; quelle del registro e del gate gia' generate restano da applicare nel normale rilascio.

Janus ha approvato il codice dell'integrazione personale senza rilievi bloccanti. Questo non certifica MI/RBAC effettivi o rendering live sull'host: non sono stati emessi token reali, effettuati test Maps a consumo o eseguiti deploy.

## Esperienza scelta

**Sezione «Da visitare», variante A:** elenco laterale, mappa e dettaglio del luogo selezionato. Su mobile, elenco espandibile. Non servono raccolte tematiche per iniziare.

Il caricamento iniziale mostra la wishlist senza contattare Azure Maps. L'azione **«Apri mappa»** carica la sola mappa base, dopo i controlli privacy richiesti. Selezionare una voce e un segnaposto aggiorna la stessa selezione; il dettaglio resta accessibile senza usare la mappa.

**Precisazione dell'utente del 2026-11-06:** mantenere l'impostazione visiva attuale della sezione e rendere disponibili sia la vista lista sia la vista mappa. I pin devono distinguere la categoria del luogo e avere una label con il nome con cui l'utente lo ha salvato (`SavedPlace.Name`), non un titolo recuperato dal provider. Le categorie devono essere riconoscibili anche senza affidarsi al solo colore. L'utente ha scelto di procedere verso la mappa reale partendo dai gate mancanti: non una mappa illustrativa o un semplice placeholder. Questa scelta non autorizza provisioning, traffico Maps o spese e non chiude i gate sottostanti.

| Capacità | Prima versione | Motivo economico e limite accettato |
|---|---|---|
| Salvare un luogo | Nome e categoria; indirizzo, note, posizione e link Google Maps facoltativi. | Dati manuali, nessuna richiesta Search. |
| Localizzare un luogo | Coordinate inserite manualmente o punto scelto sulla mappa. | Niente geocodifica inversa: il click non compila indirizzo o nome. |
| Cercare | Nome e categoria nella propria raccolta, tramite Roamly. | Nessun consumo Azure Search; non trova luoghi nuovi fuori dalla raccolta. |
| Link Google Maps | Riferimento HTTPS conservato e apribile solo con azione esplicita. | Nessun fetch, espansione short-link, anteprima remota o estrazione di dati Google. |
| Mappa | Azure Maps base, su richiesta, senza satellite o layer accessori. | Riduce caricamenti involontari, non garantisce gratuità. |
| Riutilizzare un luogo | «Aggiungi a un viaggio», scegliendo un Trip modificabile. | Riusa TripStop e i vincoli esistenti, senza routing. |

Categorie: città, paese, museo, locale, attrattiva; mantenere i tipi esistenti per sosta e servizi senza rinumerare valori persistiti. Un luogo senza posizione è un dato valido: compare nell'elenco con «Posizione da completare», mai con coordinate inventate.

Il link non è obbligatorio e non sostituisce nome o posizione. Il form spiega prima del salvataggio che non viene importato: la rinuncia alla precompilazione è il compromesso deliberato per evitare dipendenze, costi e incertezze sui diritti di riuso. Anche i link abbreviati possono essere conservati come riferimenti, senza essere risolti dal backend.

## Confini e dati

- **Places possiede SavedPlace**, figlio diretto di User e indipendente da Camper/Trip. Non introdurre WishlistPlace o un catalogo pubblico parallelo.
- Nome, indirizzo, note e categoria sono inseriti dall'utente. Il link è un riferimento esterno, non un identificativo del provider cartografico. Le colonne ExternalProvider/ExternalPlaceId/ExternalFetchedAtUtc non devono essere compilate artificialmente dal solo link.
- Conservare la provenienza corretta della posizione scelta sulla mappa secondo i termini verificati; non qualificare un punto scelto dall'utente come POI verificato. Nessun record Search viene acquisito nella prima versione.
- Schema e API seguiranno PK `(OwnerId, Id)`, FK composite, Coordinates nullable e convenzioni di concorrenza esistenti. L'introduzione del campo link e del token di concorrenza richiede la normale modifica del modello/migration, non avviene con questo piano.
- **Trips possiede TripStop:** aggiungere un luogo a un viaggio passa dall'aggregato Trip con il suo ETag. La tappa conserva nome e posizione come snapshot e SavedPlaceId come riferimento opzionale. Modificare il luogo non riscrive le tappe.
- Se il nome del luogo supera il limite della tappa, chiedere un nome compatibile; niente troncamenti silenziosi. Inserire una tappa non marca il luogo come visitato.
- Eliminare un luogo ancora referenziato è rifiutato con conflitto esplicito, senza modifiche parziali. L'utente può prima scollegarlo dalle tappe tramite modifica del Trip, mantenendo gli snapshot. Niente cascade o sganci nascosti.

Riutilizzare API, autenticazione, ownership, export ed erasure già presenti. Nessun nuovo servizio sempre attivo, database, cache distribuita o libreria spatial. La mappa è una dipendenza di presentazione: il CRUD manuale non dipende dal provider.

## Elenco, filtri e marker

La ricerca interroga l'intera raccolta dell'owner, non soltanto le righe già visualizzate. L'elenco usa paginazione keyset e ordinamento stabile secondo le convenzioni esistenti.

Per limitare complessità e trasferimenti iniziali, la mappa rappresenta **i risultati caricati nell'elenco**, con indicazione visibile del numero caricato, degli elementi senza posizione e della disponibilità di altre pagine. «Carica altri» aggiunge righe e marker; cambiare filtri azzera cursore e risultati caricati. Non dichiarare una panoramica completa finché ci sono pagine non caricate. Pan e zoom non scatenano ricerche esterne.

Risposte obsolete non sostituiscono filtri più recenti. Se un luogo selezionato esce dai risultati, aggiornare esplicitamente selezione e dettaglio. La selezione della stessa voce non deve ricreare la mappa e scaricare inutilmente risorse.

Con mappa disattivata, consenso assente o errore Azure, dichiarare lo stato e mantenere elenco, dettaglio ed editor manuale funzionanti finché l'API Roamly è disponibile. Senza rete mostrare un errore esplicito: nessuna promessa offline, PWA o cache persistente dei dati utente.

## Contenimento dei costi

L'obiettivo è restare nelle quote gratuite con uso personale, **non promettere un costo nullo**. Il 2026-11-06 l'utente ha scelto **1 EUR/mese aggiuntivo come soglia operativa per allerta e sospensione dei nuovi accessi alla mappa**. Non e' un hard cap della fattura: ritardi dei dati Azure e credenziali gia' rilasciate possono consentire ulteriore consumo. La scelta non attiva la mappa, non crea un budget Azure e non autorizza da sola provisioning, emissione di token o prove a consumo. Successivamente l'utente ha approvato **«Controllo all'apertura con blocco se i dati non sono affidabili»**: evidenze economiche valide prima dei nuovi accessi, aggiornamento on demand quando necessario e fail-closed; stato SQL globale, non personale. Scartati controllo manuale, job e automazioni periodiche. Il blocco locale seguente implementa questa strategia, non un adapter Azure attivo.

- Nessuna API Search, autocomplete, reverse geocoding, routing, meteo, traffico o AI nella prima versione.
- Disabilitare esplicitamente il fallback di geocodifica per l'accessibilità del Web SDK (`enableAccessibilityLocationFallback: false`), mantenendo accessibilità ed elenco utilizzabili. Non presumere che l'assenza di un box Search impedisca tutte le richieste geocoding.
- Caricamento della mappa solo su richiesta, singola istanza per consultazione, nessun refresh periodico o aggiornamento in background.
- Nessun polling del provider, prefetch esteso o download offline; rispettare attribuzione e policy cache applicabili senza introdurre cache applicativa persistente.
- Prima dell'attivazione, definire e verificare come osservare consumo per meter, disabilitare l'integrazione e limitare abuso. Avvisi Azure, limiti UI e token non costituiscono un hard cap di fatturazione.
- In caso di controllo consumi non affidabile, non autorizzare nuovo accesso alla mappa; la wishlist rimane utilizzabile senza provider. Non far passare questa precauzione per una garanzia di costo massimo.

Non aggiungere un proxy geografico o contatori distribuiti soltanto per supporre di ottenere gratuità: la soluzione tecnica minima va scelta e confermata nel gate sicurezza/consumi sulla base del flusso Azure effettivamente supportato.

### Gate economico locale implementato — 2026-11-06

- **Seam senza endpoint:** `MapsAdmissionService.CheckAsync` nell'API verifica account e stato corrente del registro Identity sotto lo stesso lock della cancellazione; nega account assente/in cancellazione/con cambio password obbligatorio e grant assente, revocato, inattivo o non corrispondente al testo/versione corrente **prima dell'osservatore**. Ricontrolla Identity dopo un esito economico favorevole. Non duplica il consenso e non modifica elenco/CRUD manuale.
- `MapsEconomicGate.CheckAsync` usa evidenze condivise ancora valide oppure invoca `IMapsCostObserver` fuori da ogni transazione/lock SQL. Una lease con scadenza e revisione, protetta da `sp_getapplock` per risorsa, rende il refresh single-flight tra istanze. Le altre aperture negano `RefreshInProgress`, senza attese di rete sotto lock. Risultati tardivi, lease scadute e revisioni superate non autorizzano né sovrascrivono risultati successivi; un refresh interrotto è recuperabile alla scadenza della lease.
- **Unica tabella `MapsEconomicStates`, una riga per risorsa:** PK `ResourceId`, periodo, revisione, totale economico EUR osservato e copertura temporale, lease, motivo/data di sospensione e `AlertPending`. Solo identificativo tecnico della risorsa e costi aggregati del servizio: niente `OwnerId` fittizio, account utente, luoghi, coordinate, PII o bearer. Classificazione infrastrutturale non-owned R10 esplicita, fuori da export/erasure personali; vincoli SQL su periodo/revisione, coppie nullable, costo non negativo e coerenza soglia/sospensione. Nessuna tabella budget/outbox generica né indice aggiuntivo alla PK.
- **Validità:** finestra locale mensile UTC `[primo giorno 00:00 UTC, primo giorno del mese successivo 00:00 UTC)`. La convenzione deve essere verificata contro quella economica reale prima di abilitare la policy. La prova deve riferirsi all'esatta risorsa, EUR e intero periodo richiesto, con offerta verificata, copertura completa dall'inizio periodo al relativo `CoveredThroughUtc` e campioni presenti. Timestamp futuri, copertura precedente al periodo o successiva all'osservazione, totali negativi/non finiti, dati vecchi e coperture arretrate sono rifiutati. Un HTTP 200 o un'osservazione appena raccolta non prova queste condizioni.
- `EvidenceMaxAge`, `CoverageMaxLag`, `RefreshLease`, `ObserverTimeout` e `SqlLockTimeout` **non hanno default operativi**: vanno concordati; il timeout dell'osservatore deve essere minore della lease. I test usano solo intervalli sintetici e `TimeProvider`. La DI registra una policy **inattiva/non configurata**, senza binding di attivazione, e `UnavailableMapsCostObserver`, che segnala indisponibilità invece di restituire un apparente successo/zero. I fake vivono esclusivamente nei test.
- **Soglia fissa `>= 1 EUR`:** sospensione persistente `ThresholdReached` e segnale non personale `AlertPending = true` nella stessa transazione. `0.99` non sospende. Il segnale è **pendente, non un'allerta consegnata**: il canale reale richiede ancora approvazione. Restart, rettifiche al ribasso, prove invalide e cambio mese non azzerano il latch. Nessun reset HTTP o riapertura automatica; procedura di riapertura operativa ancora da decidere. Solo per uno stato mai sospeso il nuovo mese richiede nuove evidenze.
- **Esiti espliciti:** `AccountRestricted`, `ConsentRequired`, `NotConfigured`, `Suspended`, `EvidenceMissing`, `EvidenceInvalid`, `EvidenceStale`, `ObserverUnavailable`, `StorageUnavailable`, `RefreshInProgress`, `Superseded`; ogni diniego viene loggato per motivo, senza dettagli del provider o dati personali. Cancellazione della richiesta propagata senza ammissione; errore/timeout dell'osservatore o errore DB negano. `Admitted` indica esclusivamente il superamento locale della verifica, **non autorizza il rilascio di credenziali**.
- **Limite economico:** si confronta un solo totale del servizio con la soglia, senza sommare stime e reported cost, convertire valute o inferire costi da Usage/token. Cost Management reported è ritardato e rettificabile, non invoice-final; assenza di campioni Usage non significa zero. Tariffa/offerta EUR, copertura e significato del totale restano da verificare nel futuro adapter, non sono validati da un flag sintetico dei test.

Migration `20261106105835_AddMapsEconomicGate` e snapshot generati; helper bootstrap allineato. **Non applicata al database applicativo**: prove SQL solo nei database isolati Testcontainers, incluse migration e concorrenza. Copertura mirata: soglia esatta, dati inaffidabili, indisponibilità, single-flight, recupero lease, risultati superati, latch dopo restart/cambio mese e revoca durante osservazione.

**Gate residui:** mappa ancora chiusa, finalità Identity DRAFT-1/inattiva, nessun adapter Azure/SDK/token provider, scheduler o traffico live introdotto. RBAC render-only già ristretto come riportato sotto, ma nessun permesso runtime Monitor/Cost Management autorizzato. Servono ancora validazione economica reale, intervalli operativi, canale allerta, riapertura e relative autorizzazioni cloud/privacy. Un futuro issuer dovrà ricontrollare atomicamente account, grant/revisione e stato economico, senza rete sotto lock; la gara finale di emissione non è implementata perché non esiste emissione. Bearer preesistenti non vengono revocati immediatamente dal gate; nessuna garanzia di tetto fattura o dichiarazione production-ready senza Janus.

Janus ha approvato il solo blocco economico locale mantenuto inattivo, senza rilievi bloccanti. L'approvazione non comprende mappa, attivazione, permessi cloud aggiuntivi o deploy.

**Aggiornamento successivo dei permessi runtime — 2026-11-06:** supera il precedente stato «nessun permesso runtime Monitor/Cost Management autorizzato». Dopo approvazione esplicita dell'utente, Vulcan ha creato due ruoli custom separati e i rispettivi grant alla managed identity `id-roamly-runtime-prod`: `Roamly Maps Metrics Reader`, sola Action `Microsoft.Insights/Metrics/Read` sul solo account `maps-roamly-dev`; `Roamly Maps Cost Query Reader`, sola Action `Microsoft.CostManagement/query/read` sul solo resource group `rg-roamly-dev`. AssignableScopes limitati ai rispettivi scope, tutte le esclusioni e DataActions vuote. Il secondo permette tecnicamente la lettura dei costi di tutte le risorse del gruppo: limite comunicato e accettato, non mitigato dal filtro applicativo. Rilettura ARM coerente; render-only e tutte le 30 assegnazioni preesistenti preservati. La verifica control-plane non prova accesso effettivo runtime o propagazione. Nessuna attivazione, emissione token, modifica CORS, deploy o applicazione di migration.

La query economica eseguita dall'operatore, allo scope resource group e filtrata sull'esatta risorsa Maps, ha restituito prima `429`, poi nessuna riga a un solo successivo tentativo. Non prova costo zero, campioni presenti o copertura completa: il gate economico resta inattivo e le evidenze reali restano da validare prima dell'abilitazione.

## Gate prima della mappa reale

| Gate | Prova richiesta prima di attivare |
|---|---|
| Licenza | Termini applicabili all'account, attribuzione e conservazione delle coordinate ottenute dal posizionamento; niente import di metadati POI. La futura Search richiede verifica separata. |
| Privacy | Dati trasmessi dal controllo mappa, viewport/IP, endpoint UE e informativa. Applicare il meccanismo trasversale di consenso previsto da ADR-0004 dove richiesto; non inventare un consenso parallelo Places. «Apri mappa» da solo non sostituisce il gate. |
| Autenticazione e abuso | Accesso Azure minimo necessario, nessuna chiave permanente nel bundle, flusso di credenziali/token verificato, gestione revoca/indisponibilità. |
| Consumi e risorse | Conferma separata di risorse e prove live, verifica quota/listino account, spesa accettabile e procedura di arresto. Nessun provisioning o test a consumo implicito. |
| Spike e ADR | Spike sintetico autorizzato, massimo tre giorni secondo il gate geo esistente, e ADR con perimetro esplicito. La scelta Azure per la wishlist non decide il routing camper. |

Ridurre lo scope a dati manuali evita l'importazione di record POI, ma **non aggira** licenza, privacy, autenticazione o il gate geo #3.

**Avanzamento del 2026-11-03:** approfondimento documentale completato in [ricerca Azure Maps §11](AZURE-MAPS-RESEARCH.md#11-approfondimento-per-la-wishlist-manuale--2026-11-03). L'utente ha confermato **il flusso Entra tramite API esistente per il piano**, non implementazione o modifiche cloud. Il disegno prevede managed identity dell'API, endpoint same-site autenticato e ruolo custom render-only; non è una configurazione applicata. Il token è una credenziale temporanea nel browser e non offre revoca immediata o un tetto economico.

Il registro locale Identity descritto sotto è implementato, ma la finalità Maps resta DRAFT/inattiva: non esiste ancora un consenso effettivo utilizzabile per la mappa. Dopo login interattivo autorizzato, subscription e tenant di `azure-release` sono stati verificati: l'inventario read-only dell'intera subscription restituisce zero account Maps. La managed identity `id-roamly-runtime-prod` esiste ed è già collegata alla Container App; il riuso con ruolo render-only richiede comunque conferma e una nuova assegnazione autorizzata. Il blocco di accesso è risolto, non i gate: nessuna risorsa creata, nessun token Maps emesso o traffico live attivato. Quote e prezzo effettivi dell'offerta restano da verificare.

**Aggiornamento read-only del 2026-11-06:** l'inventario odierno supera il dato storico del 3 novembre e restituisce l'account esistente `maps-roamly-dev` (`G2`, `disableLocalAuth: true`), non creato da questa sessione. L'assegnazione runtime osservata e' **Azure Maps Data Reader** (`Microsoft.Maps/accounts/*/read`), piu' ampia del render-only previsto: non distribuire token al browser prima del restringimento autorizzato. La metrica `Usage` e' disponibile, ma nelle ultime 24 ore non sono restituiti campioni non nulli; non equivale a consumo nullo verificato. Prezzi effettivi, soglie economiche e verifica operativa dei controlli restano aperti. Evidenze e limiti nella [ricerca aggiornata, sezione 12](AZURE-MAPS-RESEARCH.md#12-verifica-aggiornata-dei-gate--2026-11-06). Nessuna risorsa, assegnazione o configurazione e' stata modificata.

**Successiva modifica autorizzata del 2026-11-06:** l'utente ha approvato il restringimento RBAC sul solo account Maps. Creata e verificata l'assegnazione `Roamly Maps Render Reader` alla sola managed identity runtime, con unica `DataAction` `Microsoft.Maps/accounts/services/render/read` e assegnabilita' limitata al singolo account; rimossa poi la sola assegnazione diretta originale `Azure Maps Data Reader`. Rilettura ARM finale coerente, nessuna altra assegnazione modificata. Il restringimento e' completato, non l'integrazione: nessun token Maps, traffico data-plane o attivazione autorizzata. Propagazione ed efficacia funzionale restano da verificare nello spike; non e' una garanzia di revoca immediata o di costo massimo.

### Consenso trasversale: modello scelto e dettagli aperti

Archimedes propone ownership **Identity**, senza consenso parallelo Places o servizio SaaS. Una sola finalità iniziale versionata, `azure-maps-render`, descrive rendering, viewport, IP e metadati di rete; esclude Search, geocoding e routing. I marker restano locali, ma centrare la mappa su un luogo può rivelarne indirettamente l'area tramite i tasselli. Testo, versione e flusso di raccolta devono essere ricostruibili: `PrivacyPolicyVersionSeen` non è una prova del consenso (`ApplicationUser.cs:32-33`; ADR-0004, trigger 1).

Prima di qualunque acquisizione del bearer o montaggio SDK, il server verifica un grant esplicito alla finalità corrente. «Apri mappa» manifesta solo l'intenzione; se manca il grant, presentare accettazione dedicata e «Continua senza mappa». Rifiuto e revoca mantengono funzionante il CRUD manuale. Un aggiornamento sostanziale della finalità richiede un nuovo grant, non migra quello vecchio.

**Modello scelto dall'utente per il piano il 2026-11-03:** «Registro unico grant/revoke (Raccomandata)», rispetto a stato corrente più registro, riga per ciclo o consenso di sessione. Una sola tabella contiene la prova delle transizioni e permette di derivare lo stato corrente; nessuna proiezione persistente duplicata. La scelta non autorizza migration o implementazione.

Lo schema del solo registro locale è stato approvato e implementato il 2026-11-06: owner-scoped con PK `(OwnerId, Id)`, scopo/versione, revisione crescente tra le versioni, istante server, riferimento al testo e identificativo operazione per retry. Nessun IP, coordinata o bearer nella prova. Derivare lo stato dall'ultima transizione; una revoca non deve far riemergere un vecchio grant.

Grant, revoca e verifica finale di emissione devono condividere l'ordinamento con lo stato account. Il candidato riusa la serializzazione sulla riga account già presente nell'erasure (`AccountErasureJob.cs:140-182`), senza tenere lock SQL durante acquisizioni remote. Verificare consenso prima dell'acquisizione, poi ricontrollare account, finalità e revisione prima di autorizzare il rilascio; scartare candidati obsoleti. Una revoca che precede tale autorizzazione blocca il rilascio, ma non annulla un bearer già autorizzato o consegnato, anche se la risposta arriva dopo la revoca. Il client deve ignorare risposte tardive, smontare la mappa e fermare i rinnovi; non è una revoca della credenziale presso Azure.

La prova deve entrare nell'export e nello sweep erasure tramite il meccanismo owned esistente (`OwnedResourceWalker.cs:24-30`; `AccountExportHandler.cs:115-133`). **Politica tecnica scelta dall'utente per l'alpha il 2026-11-06:** conservare lo storico completo grant/revoke per la vita dell'account ed eliminarlo con l'erasure, senza purge periodico dei cicli conclusi e senza residui personali post-erasure. La richiesta di cancellazione non anticipa da sola l'erasure durante la grazia prevista per l'account. La durata non e' dedotta dai log o dai 30 giorni di grazia, ne' presentata come termine prescritto dalla legge: resta necessaria la validazione privacy competente prima dell'attivazione e il riesame prima dell'estensione oltre l'alpha. Non considerare anonima una prova tramite il solo hash. La scelta non approva schema dettagliato, testo, base giuridica, migration o attivazione cloud.

Prove specifiche: zero richieste provider/acquisizioni token senza grant; grant versionato e retry senza duplicati; revoca concorrente che blocca un grant obsoleto e scarta token in acquisizione; vecchia versione non utilizzabile; restrizioni account e CSRF; token solo in memoria e risposta non memorizzabile; export/erasure isolati per owner; funzioni manuali disponibili senza consenso. I test delle gare devono attraversare SQL Server, non una simulazione del relativo lock.

### Registro locale implementato — 2026-11-06

Approvazione esplicita: **«Approva il registro locale e la migration non applicata»**. Supera il precedente limite di sola pianificazione esclusivamente per registro, API, export/erasure, migration generata e prove locali. **Registro locale implementato, gate esterni ancora aperti.** Nessuna mappa reale è attivata, nessun consenso è dichiarato validato legalmente.

- Unica entità Identity `ConsentTransition`, nessuna proiezione persistente o consenso parallelo Places. Ultima revisione per finalità, non ultimo grant: una revoca non fa riemergere concessioni precedenti. Un grant è corrente solo se coincidono versione e riferimento testo della definizione server attiva.
- Catalogo minimo immutabile in DI: la registrazione applicativa contiene soltanto `azure-maps-render`, versione `DRAFT-1`, riferimento `DRAFT:azure-maps-render:DRAFT-1`, **inattiva**. Non contiene un'informativa definitiva e non è attivabile via body, ambiente o endpoint. Nei soli test il test host sostituisce la definizione con finalità/testi sintetici; ciò non abilita servizi esterni.
- Scritture in transazione con lo stesso `UPDLOCK, HOLDLOCK` sulla riga account usato dall'erasure; account riletto sotto lock, rifiuto esplicito se assente, in cancellazione o soggetto a cambio password. Nessuna chiamata di rete nel registro. Export ed erasure includono automaticamente tutte le transizioni tramite il walker owned e il piano di sweep esistenti.
- Retention alpha: storico completo fino all'erasure, nessun purge periodico, nessun IP/coordinata/token nella prova. Non cambia la grazia account; validazione privacy prima dell'attivazione e riesame oltre l'alpha restano necessari.

Contratto API v1 additivo, autenticato e soggetto alle barriere account/CSRF esistenti:

| Endpoint | Contratto |
|---|---|
| `GET /api/v1/me/consents/{purpose}` | `200` con `purpose`, `version`, `textReference`, `isActive`, `isGranted`, `revision`; `404` uniforme per finalità sconosciuta. Stato derivato, non credenziale o autorizzazione al rilascio di token. |
| `POST /api/v1/me/consents/{purpose}/grants` | Body `{ id, version, textReference }`, `Id` COMB client-side. `201` nuova ricevuta storica, `200` replay identico, `409` collisione payload, definizione obsoleta o finalità inattiva. Per il DRAFT Maps nessun grant nuovo viene registrato. |
| `POST /api/v1/me/consents/{purpose}/revocations` | Stesso body e idempotenza; registra una revoca della definizione corrente anche senza un grant precedente. Non riattiva altre versioni. |

Ricevuta: `id`, `purpose`, `version`, `textReference`, `action` (`grant`/`revoke`), `revision`, `recordedAtUtc` server (millisecondi UTC). Il replay conserva la ricevuta originale **senza** mutare lo stato: un vecchio grant ripetuto dopo una revoca non torna efficace. Le revisioni crescono anche cambiando versione; lo stesso ID non può rappresentare due azioni/finalità/payload per lo stesso owner. Identità e stato sono isolati tra owner.

Le risposte elaborate dalla slice del registro usano `Cache-Control: no-store`; gli errori usano `ProblemFactory`. Validazione nativa .NET 10 (`400`), restrizioni/sessione account (`401`/`403`) e rate limit (`429`) restano in vigore. Non esiste endpoint di aggiornamento di una transizione, quindi non si aggiunge `If-Match` artificiale: il registro appende eventi serializzati, non modifica una risorsa con `RowVersion`. Le regole ETag dei futuri update Places restano invariate.

Migration EF `20261106091448_AddIdentityConsentRegistry` **generata ma non applicata al database applicativo**; applicazione solo nei database SQL Server isolati Testcontainers per le prove. Restano aperti privacy/testo e licenza, flusso token Entra e revoca delle credenziali, consumo/abuso, configurazione cloud, SDK e validazione della UX consenso. Nessun SDK, endpoint token Maps, provisioning o nuova dipendenza esterna è introdotto da questo blocco.

**Allineamento del guard migration:** l'elenco atteso in `infra/tools/runtime-bootstrap/bridge/BootstrapMigrationHistory.cs` include `20261104084728_AddSavedPlaceGoogleMapsUrl` (già presente nel checkout) e `20261106091448_AddIdentityConsentRegistry`, coerentemente con le migration dell'applicazione. La verifica resta esatta: non sono rimossi controlli sulle migration mancanti o inattese. L'allineamento non applica migration, non esegue il bootstrap e non autorizza un deploy.

Validazione locale del blocco: cicli TDD al confine HTTP, 24 casi del registro (incluse gare SQL Server con lock reale, retry, versioni, isolamento owner, CSRF, restrizioni, export e rimozione dello storico), L0 **74/74**, snapshot OpenAPI generato dal test R51 e controllo EF offline senza differenze modello/migration. Dopo l'allineamento del guard, suite completa `dotnet test --solution Roamly.slnx`: **385 totali, 384 superati, 1 ignorato**, nessun fallimento; l'acceptance Azure Blob resta esplicitamente disabilitata. Janus ha approvato il solo blocco locale con finalità inattiva, senza rilievi bloccanti; l'approvazione non comprende mappa, attivazione o deploy. Nessun frontend modificato da questo blocco.

## Sequenza implementativa e accettazione

1. **Salvataggio ed elenco manuali:** persistenza reale, categorie, coordinate facoltative, link opaco, ricerca owner-scoped e paginazione. Nessuna chiamata a provider. Nessuna feature Phase 3 viene dichiarata sbloccata dal solo completamento del piano.
2. **Modifica e cancellazione:** ETag/If-Match, conflitti espliciti, vincoli sulle tappe referenzianti, export ed erasure aggiornati. L'app non si limita al salvataggio dimostrativo in memoria.
3. **Mappa Azure sincronizzata:** dopo i gate, attivazione volontaria, selezione condivisa, risultati caricati dichiarati, posizionamento e fallback verificati, attribuzione leggibile anche su mobile.
4. **Riuso nel Trip:** scelta del viaggio, snapshot e riferimento owner-scoped, aggiornamento atomico protetto dall'ETag del Trip; cancellazione/scollegamento non distruggono lo storico.

Prove richieste nella vera implementazione: due owner con accessi incrociati e 404 uniforme; validazione URL e coordinate; assenza di richieste remote dal salvataggio di un link; pagine e filtri senza duplicati/omissioni; selezione coerente; 428/412 per concorrenza; conflitto sulla cancellazione referenziata; snapshot e stato del Trip; export/erasure completi; assenza di chiamate Azure prima dell'apertura autorizzata; indisponibilità e disattivazione; accessibilità tastiera/mobile. Usare L0 durante il lavoro e suite completa prima della consegna, più controlli frontend pertinenti. Janus valida l'implementazione prima di qualunque dichiarazione di production readiness.

Esclusi: ricerca esterna, import automatico Google, routing, geolocalizzazione automatica, AI, foto/recensioni, raccolte e stato «visitato». Si rivalutano solo se il lavoro manuale diventa un limite reale.

## Riferimenti

- [Mappa decisionale](DECISION-MAP-PLACES-WISHLIST.md)
- [Confronto provider e fonti ufficiali](PLACES-MAP-PROVIDER-COST-RESEARCH.md)
- [Ricerca Azure Maps](AZURE-MAPS-RESEARCH.md)
- [Domain model](../architecture/CONTEXT.md)
- [Gate geografico #3](../OPEN-DECISIONS.md)
