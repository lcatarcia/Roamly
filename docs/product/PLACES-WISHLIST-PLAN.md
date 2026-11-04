# Da visitare: piano della prima versione

Stato: pianificazione, non implementato né attivato. Data: 2026-11-03.

L'utente ha scelto mappa ed elenco sincronizzati, poi Azure Maps, e ha delegato le scelte restanti privilegiando il costo in denaro. Le decisioni seguenti esercitano tale delega sul piano; non sono una validazione d'usabilità né un'autorizzazione a provisioning, spese o trasferimenti di dati. La decisione geo #3 resta aperta fino a spike e ADR.

## Esperienza scelta

**Sezione «Da visitare», variante A:** elenco laterale, mappa e dettaglio del luogo selezionato. Su mobile, elenco espandibile. Non servono raccolte tematiche per iniziare.

Il caricamento iniziale mostra la wishlist senza contattare Azure Maps. L'azione **«Apri mappa»** carica la sola mappa base, dopo i controlli privacy richiesti. Selezionare una voce e un segnaposto aggiorna la stessa selezione; il dettaglio resta accessibile senza usare la mappa.

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

L'obiettivo è restare nelle quote gratuite con uso personale, **non promettere un costo nullo**. Nessun plafond di spesa aggiuntivo è stato concordato.

- Nessuna API Search, autocomplete, reverse geocoding, routing, meteo, traffico o AI nella prima versione.
- Disabilitare esplicitamente il fallback di geocodifica per l'accessibilità del Web SDK (`enableAccessibilityLocationFallback: false`), mantenendo accessibilità ed elenco utilizzabili. Non presumere che l'assenza di un box Search impedisca tutte le richieste geocoding.
- Caricamento della mappa solo su richiesta, singola istanza per consultazione, nessun refresh periodico o aggiornamento in background.
- Nessun polling del provider, prefetch esteso o download offline; rispettare attribuzione e policy cache applicabili senza introdurre cache applicativa persistente.
- Prima dell'attivazione, definire e verificare come osservare consumo per meter, disabilitare l'integrazione e limitare abuso. Avvisi Azure, limiti UI e token non costituiscono un hard cap di fatturazione.
- In caso di controllo consumi non affidabile, non autorizzare nuovo accesso alla mappa; la wishlist rimane utilizzabile senza provider. Non far passare questa precauzione per una garanzia di costo massimo.

Non aggiungere un proxy geografico o contatori distribuiti soltanto per supporre di ottenere gratuità: la soluzione tecnica minima va scelta e confermata nel gate sicurezza/consumi sulla base del flusso Azure effettivamente supportato.

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

Il runtime non ha ancora un meccanismo di consensi trovato e riutilizzabile. Dopo login interattivo autorizzato, subscription e tenant di `azure-release` sono stati verificati: l'inventario read-only dell'intera subscription restituisce zero account Maps. La managed identity `id-roamly-runtime-prod` esiste ed è già collegata alla Container App; il riuso con ruolo render-only richiede comunque conferma e una nuova assegnazione autorizzata. Il blocco di accesso è risolto, non i gate: nessuna risorsa creata, nessun token Maps emesso o traffico live attivato. Quote e prezzo effettivi dell'offerta restano da verificare.

### Consenso trasversale: modello scelto e dettagli aperti

Archimedes propone ownership **Identity**, senza consenso parallelo Places o servizio SaaS. Una sola finalità iniziale versionata, `azure-maps-render`, descrive rendering, viewport, IP e metadati di rete; esclude Search, geocoding e routing. I marker restano locali, ma centrare la mappa su un luogo può rivelarne indirettamente l'area tramite i tasselli. Testo, versione e flusso di raccolta devono essere ricostruibili: `PrivacyPolicyVersionSeen` non è una prova del consenso (`ApplicationUser.cs:32-33`; ADR-0004, trigger 1).

Prima di qualunque acquisizione del bearer o montaggio SDK, il server verifica un grant esplicito alla finalità corrente. «Apri mappa» manifesta solo l'intenzione; se manca il grant, presentare accettazione dedicata e «Continua senza mappa». Rifiuto e revoca mantengono funzionante il CRUD manuale. Un aggiornamento sostanziale della finalità richiede un nuovo grant, non migra quello vecchio.

**Modello scelto dall'utente per il piano il 2026-11-03:** «Registro unico grant/revoke (Raccomandata)», rispetto a stato corrente più registro, riga per ciclo o consenso di sessione. Una sola tabella contiene la prova delle transizioni e permette di derivare lo stato corrente; nessuna proiezione persistente duplicata. La scelta non autorizza migration o implementazione.

Lo schema dettagliato resta candidato: owner-scoped con PK `(OwnerId, Id)`, scopo/versione, revisione crescente tra le versioni, istante server, riferimento al testo e identificativo operazione per retry. Nessun IP, coordinata o bearer nella prova. Derivare lo stato dall'ultima transizione; una revoca non deve far riemergere un vecchio grant.

Grant, revoca e verifica finale di emissione devono condividere l'ordinamento con lo stato account. Il candidato riusa la serializzazione sulla riga account già presente nell'erasure (`AccountErasureJob.cs:140-182`), senza tenere lock SQL durante acquisizioni remote. Verificare consenso prima dell'acquisizione, poi ricontrollare account, finalità e revisione prima di autorizzare il rilascio; scartare candidati obsoleti. Una revoca che precede tale autorizzazione blocca il rilascio, ma non annulla un bearer già autorizzato o consegnato, anche se la risposta arriva dopo la revoca. Il client deve ignorare risposte tardive, smontare la mappa e fermare i rinnovi; non è una revoca della credenziale presso Azure.

La prova deve entrare nell'export e nello sweep erasure tramite il meccanismo owned esistente (`OwnedResourceWalker.cs:24-30`; `AccountExportHandler.cs:115-133`). **Retention, decorrenza e cancellazione delle prove richiedono decisione esplicita e validazione competente**: non dedurle dai log o dai 30 giorni di grazia dell'account. Non introdurre residui post-erasure né considerarli anonimi tramite hash. Schema dettagliato, testo e base giuridica non sono approvati dalla scelta del registro.

Prove specifiche: zero richieste provider/acquisizioni token senza grant; grant versionato e retry senza duplicati; revoca concorrente che blocca un grant obsoleto e scarta token in acquisizione; vecchia versione non utilizzabile; restrizioni account e CSRF; token solo in memoria e risposta non memorizzabile; export/erasure isolati per owner; funzioni manuali disponibili senza consenso. I test delle gare devono attraversare SQL Server, non una simulazione del relativo lock.

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
