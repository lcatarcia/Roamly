# Ricerca architetturale — provider geografici per Roamly

- **Verifica documentale:** 2026-10-13
- **Owner:** @archimedes
- **Stato:** ricerca preliminare; nessun provider selezionato
- **Perimetro temporale:** preparazione delle future Phase 3 (Maps & Places) e Phase 4 (Intelligence / vehicle-aware routing)

> **Questa ricerca non autorizza chiamate live ai servizi usando dati Roamly, l'invio di coordinate reali o personali, la creazione di account, il provisioning di risorse, né l'integrazione nel codice.** Sono state consultate esclusivamente fonti documentali pubbliche. L'eventuale spike successivo deve usare dati sintetici, senza credenziali o dati personali, e richiede autorizzazioni separate per ogni attività che esca da tale perimetro.

## 1. Sintesi esecutiva

Non emerge un motivo per scegliere un solo provider per ogni esigenza. Occorre valutare separatamente:

1. **Mappa e tiles** — rendering di sfondo e attribuzione; tile raster/vector, stile, caching, uso offline.
2. **Geocoding, places e search** — ricerca testuale/POI, autocomplete, reverse geocoding e diritti sui risultati.
3. **Routing** — percorso, riepilogo, restrizioni veicolo e geometria utilizzabile nel Ribbon.

La documentazione pubblica consultata suggerisce questa **shortlist per il prossimo spike, non per l'adozione**:

- **HERE** per la prova di routing con profilo camper: la documentazione aggiornata descrive routing truck e proprietà fisiche (tra cui altezza, larghezza, lunghezza e pesi), restituisce una polyline e può segnalare violazioni critiche. Mancano ancora verifica pratica sui casi camper europei, chiarimenti contrattuali su retention/riuso della geometria e una quotazione affidabile.
- **PTV Developer** come confronto commerciale mirato per routing pesante: la piattaforma è orientata a logistica, mappe, geocoding e routing e pubblica un Routing API; documentazione pubblica accessibile in questa verifica non basta però a confermare il fit puntuale per camper, i campi supportati, la licenza della geometria o la retention. Richiedere un'offerta e risposte scritte solo se HERE non supera i test o se PTV dimostra un vantaggio concreto.
- **GraphHopper con dati OSM**, hosted e self-hosted valutati come due alternative distinte: consente di studiare una strada OSM-based con maggiore controllo possibile sul lato self-hosted, ma qualità e completezza delle restrizioni per camper non sono dimostrate dalla sola documentazione. Self-hosting riduce l'invio delle query a un API SaaS ma aggiunge un carico operativo e licenze/dati da gestire.
- **TomTom Orbis Routing v3** non è oggi il candidato principale per vehicle-aware routing camper: la documentazione corrente consultata dichiara `car` come modalità e accetta il peso ma non altezza/larghezza/lunghezza, massa per asse o restrizioni ADR. Rimane plausibile per mappe/search o routing auto, ma non colma da solo il requisito camper. Le vecchie API TomTom Maps v1 sono una generazione distinta: non considerarle equivalenti senza verifica di disponibilità, roadmap e condizioni contrattuali.
- **OSM Foundation public tiles/Nominatim** non sono servizi production-ready da trattare come API gratuita senza limiti: sono servizi di comunità best-effort, con politiche restrittive. OSM come **dataset** e l'hosting autonomo sono opzioni diverse dai server pubblici OSMF.

**Raccomandazione per lo spike successivo:** un confronto documentale e con dati sintetici, time-boxed entro il massimo già previsto di tre giorni, composto da due tracce: (A) routing camper HERE e un confronto PTV solo se si ottengono specifiche/offerta sufficienti; (B) valutazione indipendente dei componenti map/search, confrontando HERE, TomTom e OSM-derived hosted/self-hosted senza presumere che vinca lo stesso fornitore del routing. Prima dell'ADR `geo-data-strategy`, ottenere risposte scritte su storage/retention, quote e termini, poi registrare cosa si è testato e non testato. Questa è una raccomandazione di ricerca, non una decisione vendor.

## 2. Contesto e vincoli Roamly

- Roamly è un personal travel OS per camperisti, con React, ASP.NET Core 10 e SQL Server.
- **ADR-0001** mantiene SQL Server e rimanda i tipi spaziali: coordinate WGS84 persistite come `decimal`, senza `geography`/NetTopologySuite in questa fase. Il routing vehicle-aware dipende da provider esterno; non si propone di cambiare tale ADR.
- Phase 3 include mappa, geolocalizzazione, ricerca, luoghi e luoghi salvati. Phase 4 include i vincoli vehicle-aware.
- La feature strategica deve tenere conto di altezza, lunghezza, larghezza, peso, restrizioni, ponti, accessi e caratteristiche del percorso. La geometria dev'essere visualizzabile e stilizzabile nel Ribbon.
- `FEATURES.md` §4 specifica che le fonti e la licenza dei dati delle aree di sosta restano aperte: i dati community di servizi come Park4Night non sono utilizzabili senza permesso esplicito. **Questa ricerca non identifica né raccomanda una fonte di aree di sosta.**
- `OPEN-DECISIONS.md` avverte che trasmettere posizioni dell'utente a terzi attiva il trigger del consenso. Query di ricerca generiche e coordinate di partenza/destinazione non sono intercambiabili dal punto di vista privacy.

Quindi “provider geo” non è un singolo componente architetturale. La selezione per ogni capacità può essere diversa e le licenze possono vietare combinazioni apparentemente naturali, come visualizzare il risultato routing sopra tiles di altro provider o conservare localmente una geometria.

## 3. Metodo, fonti e limiti della verifica

Sono state consultate le documentazioni tecniche, i termini, le policy di uso, le pagine di pricing e le informative pubblicate dai provider o dagli enti titolari dei servizi. **Data della verifica: 2026-10-13.** Dove la pagina pubblica è dinamica, incompleta o rimanda a un'offerta commerciale, non sono riportati importi o diritti dedotti. Le pagine possono cambiare dopo la verifica.

Le fonti ufficiali principali sono collegate nella sezione [Fonti ufficiali](#10-fonti-ufficiali-consultate). I documenti API descrivono capacità tecniche, ma non sostituiscono termini applicabili, DPA, offerta commerciale o interpretazione legale. “Non confermato” indica che la documentazione pubblica consultata non ha consentito di attestarlo; non significa che la funzione o il diritto non esistano.

## 4. Confronto: capacità e fit tecnico

| Candidato | Mappa / tiles | Search, places, geocoding | Routing e camper | Geometria per Ribbon | Stato dell'evidenza |
|---|---|---|---|---|---|
| **HERE Platform** | Capacità cartografiche presenti nella piattaforma; va selezionato e licenziato lo specifico prodotto map-rendering/tiles. Non si assume che ogni API di routing includa i diritti sui tiles. | HERE Geocoding & Search è una famiglia separata di servizi. Da verificare endpoint, categorie POI, autocomplete, geocoding inverso e limiti di risposta per il caso Roamly. | La documentazione Routing v8 descrive `transportMode=truck` e parametri fisici; proprietà non specificate assumono default `0`/`none`, quindi non configurarle è insufficiente per il profilo camper. Include restrizioni legali, fisiche, merci pericolose e temporali secondo le pagine consultate. | Routing v8 può restituire `polyline`; documenta anche notices di restrizione e notices critiche. La geometria resta tecnicamente promettente per una linea del Ribbon, ma persistibilità/riuso e combinazione con mappe di terzi richiedono conferma dei termini applicabili. | **Documentato**: parametri truck e route geometry. **Da testare**: scenario camper/Europa, restrizioni effettivamente coperte, limiti di parametri/waypoint, stile del Ribbon. **Da negoziare/confermare**: tariffe e diritti di cache/storage. |
| **TomTom** | Map Display API separata, con tiles raster/vector; l'uso display e l'attribuzione vanno verificati nei termini del prodotto specifico. | Search API separata: fuzzy search indirizzi/POI, autocomplete, ricerche entro geometria, categorie e Place by ID. Dati di aree camper/stop non sono per questo garantiti o autorizzati. | La documentazione corrente **Orbis v3** consultata indica modalità `car`; la guida vehicle aggiornata al 2026-09-01 mostra peso accettato ma altezza, larghezza, lunghezza, peso per asse e ADR non accettati. TomTom Maps v1 ha un contratto/API generazionale distinto e documenta più parametri; la compatibilità futura non è assunta. | Orbis v3 rappresenta la path in GeoJSON e/o `compressedPath` nei documenti di migrazione; routing v1 documenta una rappresentazione della geometria. Va provata la conversione e verificato se i termini consentano conservazione/riuso. | **Documentato**: Search API; Orbis car/limite dimensioni; path/GeoJSON. **Da testare**: display e composizione API. **Gap di prodotto** per requisito camper nella v3 corrente, salvo evoluzioni/documentazione successive. |
| **PTV Developer** | Piattaforma PTV Developer pubblicizza servizi geografici e Maps API. Verificare se mappe/raster/vector sono incluse o richiedono prodotti/licenze separati. | Il catalogo include servizi di geocoding e API geografiche; non è stato accertato il set di POI e autocomplete più adatto a luoghi camper. | PTV si posiziona sulle API logistiche e il Routing API è documentato; fonti commerciali ufficiali lo presentano per routing con attributi di veicolo/restrizioni. La pagina pubblica di routing non ha consentito qui di verificare tutti i parametri camper e il comportamento sulle specifiche restrizioni. | La geometria di percorso deve essere verificata sul modello di risposta e nei diritti previsti dal contratto. La sola disponibilità di una Maps API non dimostra che la geometria routing sia memorizzabile o riutilizzabile in Ribbon. | **Documentato a livello di famiglia/prodotto**: Routing API e APIs logistiche/geografiche. **Non confermato**: supporto preciso alle dimensioni camper richieste, formati geometrici, storage, attribution e termini applicabili; chiedere un'offerta/spec scritta prima di inserire un test API. |
| **GraphHopper Directions API (hosted)** | Non assumere che API di routing implichi tiles adatti o una licenza display indipendente; verificare prodotto e provider mappa associato. | La piattaforma offre Geocoding API; l'informativa dichiara che, a seconda del provider scelto, alcune richieste di geocoding possono essere inoltrate a soggetti terzi. Verificare e selezionare esplicitamente il sottoprovider. | Docs API descrivono routing, custom models/profiles e mappa OSM-based; non si considera dimostrato che un modello personalizzato equivale a una base dati camper affidabile o a un servizio certificato per altezza/peso su tutti i territori. Campi, limiti e semantica vanno verificati su documentazione tecnica e test. | Le API di routing espongono route/points e la documentazione descrive risposte di percorso. Restano da verificare densità/precisione, semplificazione, licenza di persistenza, attribution e interoperabilità con tiles. | **Documentato**: API e policy privacy. **Da provare**: profili, mappatura restrizioni OSM, geometria e capacità. La policy privacy riporta retention massima delle request API di 5 settimane sui server in Germania (dettagli in §5). |
| **GraphHopper Core self-hosted + dati OSM** | Hosting di tiles è un sottosistema separato; servono pipeline, tile renderer/serving e relative licenze/operazioni. | Geocoding self-hosted o provider separato. Non confondere Nominatim pubblico con Nominatim gestito internamente o con geocoding hosted. | L'esecuzione propria può ridurre il trasferimento di query a un routing SaaS, ma non rende automaticamente i dati OSM completi, corretti o aggiornati per le restrizioni camper. Profilo, dataset, aggiornamenti ed evidenza delle restrizioni ricadono maggiormente su Roamly. | Output controllato dal deployment e dalla versione dell'engine; verificare formato e gestione della geometria nel preciso percorso GraphHopper selezionato. L'hosting proprio non risolve gli obblighi ODbL sui dati di base o derivati. | **Opzione architetturale, non valutazione di qualità geografica**. **Da prototipare e operare**: ingest aggiornamenti, costi infrastrutturali, restrizioni effettive e test di regressione del routing. |
| **OSMF public tiles / public Nominatim** | Tile server pubblico OSMF: best-effort, senza SLA, limitato; non va assunto come infrastruttura gratuita production. | Nominatim pubblico ammette ricerca end-user a uso moderato, ma vieta autocomplete client-side, impone massimo 1 richiesta/s per applicazione, e mette limiti su bulk e privacy. | OSMF non fornisce con questi servizi un routing engine per camper. I dati OSM possono essere base per un engine separato, non garanzia di routing camper-specifico. | Nessuna route geometry è fornita dai tiles o da Nominatim. Per visualizzare route serve un routing provider indipendente. | Le policy pubbliche sono vincoli d'uso espliciti, non raccomandazione di integrazione production. ODbL/attribuzione riguarda il dataset e non equivale a licenza/availability delle API pubbliche. |

### Lettura del requisito “camper”

La presenza di parametri `height`, `weight`, `length` o `width` nella request **non prova** che:

- i limiti stradali siano mappati in modo capillare, aggiornato e utilizzabile nel territorio di Roamly;
- il significato/valore delle dimensioni corrisponda al camper carico, agli accessori e agli eventuali rimorchi;
- tutte le categorie di restrizioni (altezza/portata ponte, massa totale/per asse, larghezza/lunghezza, ZTL, strade strette, pendenza, accesso locale o temporaneo) siano coperte;
- il provider possa sempre trovare una via legale, né che l'assenza di una notice significhi compatibilità verificata.

HERE documenta sia notices di restrizione sia casi in cui restituisce un itinerario con violazioni critiche o non riesce a calcolare. Questo è un comportamento importante da rappresentare nel prodotto: un percorso visualizzato non va etichettato automaticamente come “sicuro” o “compatibile”. PTV e GraphHopper richiedono verifica ulteriore sui campi concreti, sull'origine dei dati e su failure/warning semantics. TomTom Orbis v3, nella guida corrente verificata, non soddisfa il profilo camper necessario per dimensioni e ADR.

**Precisazione HERE per la prova camper:** le guide distinguono `transportMode=truck` (restrizioni legali truck e parametri fisici; i parametri omessi assumono default `0`/`none`) dal routing generico, dove le proprietà fisiche possono comunque essere impostate — la guida mostra, per esempio, una `car` con altezza. Questi percorsi non sono equivalenti: una prova offline deve documentare separatamente parametri e restrizioni ottenibili nei due modi. L'esistenza di `vehicle[height]` su `car` non dimostra che si applichino tutte le restrizioni truck, né che un camper sia trattato come veicolo omologato per il routing truck.

## 5. Costi, dati e licenze

| Candidato | Costo indicativo al livello qualitativo | Trasferimento query/coordinate | Retention documentata | Storage, attribution e diritti |
|---|---|---|---|---|
| **HERE** | La pagina pricing pubblica verificata indirizza a contatto/soluzione per esigenze specifiche. Esistono condizioni e limiti per prodotto/transaction da verificare nel piano scelto; in questa ricerca non è stato stabilito un importo o un free tier applicabile a Roamly. Budget qualitativo: **usage/contratto da quotare**, più eventuali map rendering/search separati. | Routing, ricerca o geocoding hosted trasferiscono coordinate/query al provider. Trattare origine/destinazione e coordinate ottenute da geolocalizzazione come sensibili; una proxy Roamly potrebbe centralizzare il traffico ma non elimina il trasferimento a HERE. | Periodo e log delle request pertinenti non verificati nelle pagine pubbliche consultate per il prodotto/contratto specifico; DPA e configurazione applicabile da chiedere. | Obblighi di attribution e limiti di caching/storage dipendono da API e termini. La documentazione tecnica di caching o dei tiles non è autorizzazione generale a conservare dati di routing. Contratto e product-specific terms da confermare prima di persistere geometry o search results. |
| **TomTom** | Documentazione corrente offre evaluation/Freemium; la pagina pubblica consultata non ha consentito di attestare un prezzo e un plafond applicabili a ciascuna delle API Orbis, Search e Map Display. Trattare come **tier/consumo con cap e overage da verificare**, senza assumere un importo o soglia specifica. | Le API hosted ricevono le posizioni richieste; chiamata browser↔vendor trasferisce in modo diretto, API proxy↔vendor passa dal backend Roamly. Anche con proxy il provider riceve i dati geospaziali. | Non verificata qui una retention precisa per le request/coordinate delle API Orbis/Search. Privacy notice, DPA e termini del prodotto sono prerequisiti. | Percorso tecnico/GeoJSON non stabilisce diritti di archiviazione permanente, riuso, offline o visualizzazione su tiles di terzi. Verificare licenza specifica; attribution dei tiles e branding da includere nell'analisi UI. |
| **PTV Developer** | È indicato un trial/free key per valutazione, ma i prezzi commerciali e le soglie non sono pubblicati in modo verificabile nella documentazione consultata: probabile **quote/contratto** e limiti di trial da confermare. | Le richieste hosted trasferiscono indirizzi/coordinate e parametri veicolo a PTV; configurare campi minimali, evitare di trasmettere identità Roamly o ID direttamente riconducibili all'utente. | Non è stato possibile verificare un periodo API-specifico né una localizzazione/retention dei payload in fonti pubbliche consultate. Richiedere clausole DPA e risposta per l'API scelta. | Scope d'uso e condizioni possono essere definiti nell'offerta. Ottenere chiarimento scritto su route geometry, memorizzazione nei viaggi, refresh/cache, attribution, map-overlay/tiles e cessazione del contratto. |
| **GraphHopper hosted** | Modello commerciale API con free/paid usage tiers descritti sul sito pricing, ma la pagina e il relativo contenuto possono cambiare; importi/soglie non sono fissati in questo report perché non verificati in modo affidabile per l'offerta corrente al 2026-10-13. Qualitativamente: **credit/request-based**, includere quote geocoding, routing e possibili sottoprovider. | Privacy policy dice che ogni request API è memorizzata e che le richieste possono includere body, headers, IP e timestamp. Le request vengono processate su server Hetzner in Germania. Le chiamate di geocoding possono essere inoltrate ad altri provider scelti/configurati: possibile trasferimento ulteriore. | Policy pubblicata: informazioni delle request conservate per un massimo di **5 settimane**. Confermare che la versione corrente e l'eventuale DPA valgano per il piano e gli endpoint selezionati. | Non assumere che l'output di un'API hosted possa essere conservato indefinitamente. Controllare i termini API, l'uso commerciale e licenze OSM; specificare attribution quando si visualizzano dati OSM. |
| **GraphHopper self-hosted + OSM** | Nessuna tariffa per chiamata al provider di routing che si ospita, ma il costo non è zero: compute, memoria/dischi, aggiornamento/ingest dei dati, monitoraggio, backup, incidenti e tempo di gestione. Licenza e dipendenze dell'engine vanno verificate sulla versione scelta. | Può evitare che le richieste di routing Roamly raggiungano GraphHopper SaaS o altro provider, se l'installazione e le chiamate sono realmente sotto controllo di Roamly. Restano traffico verso hosting cloud e fonti di aggiornamento; la query può comunque essere osservabile nei log applicativi propri. | Retention controllabile operativamente da Roamly per le request applicative, ma va definita e testata. Non significa che la licenza dati o la retention dei backup siano automaticamente risolte. | Dati OSM sotto ODbL con attribuzione e obblighi di condivisione per database derivati in base ai termini applicabili. Non assimilare il dataset OSM al tileserver pubblico; serve analisi licenze/obblighi prima di distribuire dati o servizi. |
| **OSM public tiles/Nominatim** | Nessun prezzo a chiamata, ma **non è un tier gratuito garantito**: servizio finanziato da donazioni/sponsorship, capacità limitata, best-effort e senza SLA. Il costo di una soluzione più affidabile ricade su hosting di terzi o self-hosting. | Le richieste inviano tile coordinates, query testuali o coordinate agli endpoint pubblici OSMF. Le policy chiedono di non inviare dati personali o riservati. Usare un proxy/cache dove pertinente non autorizza un uso altrimenti vietato. | Non si promette una retention idonea per un'app; consultare la privacy policy OSMF, ma non trattare i servizi pubblici come un servizio con impegni di retention contrattuali. | Attribution OSM chiara e licenza ODbL. Tiles: niente bulk download/scraping o prefetch; cache secondo header o almeno 7 giorni se il client non li interpreta. Nominatim: max 1 request/s per app, no autocomplete, switching richiesto senza release, cache raccomandata; non usare per bulk o ricerca sistematica. |

### Privacy e minimizzazione delle query

Per ogni API hosted, il payload minimo dovrebbe essere specificato prima dell'implementazione. Una richiesta route può contenere origine/destinazione, tappe, profilo e configurazione veicolo; una ricerca può contenere testo digitato o area geografica; geolocalizzazione può essere ancora più sensibile della ricerca di un POI generico. Una geocodifica eseguita dal browser non è equivalente a una query generica senza dati dell'utente.

La strategia backend-proxy può proteggere le chiavi API, centralizzare logging e normalizzazione, e rendere sostituibile il provider; **non** impedisce al provider di ricevere le coordinate. Prima di inviare coordinate reali servono le decisioni privacy e consenso pertinenti, termini/DPA del servizio scelto, dichiarazione dei campi trasferiti e retention/log policy Roamly.

## 6. Geometria, Ribbon e persistenza

Il Ribbon è parte dell'identità UI Roamly: un provider candidato deve restituire un percorso visualizzabile come linea e consentire la stilizzazione a livello applicativo (colore, spessore, stati, warning). Le fonti tecniche suggeriscono:

- **HERE Routing v8:** risposta con `polyline`, segmenti/spans e notices; ciò dimostra che è tecnicamente possibile disegnare e distinguere tratti/avvisi. Non dimostra il diritto a conservare la geometria nel DB o a riutilizzarla dopo la scadenza di una route.
- **TomTom Orbis v3:** la migration guide descrive geometria route in GeoJSON e `compressedPath`; la gestione del modello v3 differisce dalla precedente API Maps v1. Va implementato/validato contro lo schema corrente solo dopo la selezione del prodotto e le verifiche di licenza.
- **PTV:** ottenere schema di route completo e clausole applicabili sul riuso; non basta vedere un path nella demo o una Maps API.
- **GraphHopper:** le API route espongono una geometria/points secondo schema dell'endpoint; validare encoding, densità e costo di conversione con il percorso hosted o self-hosted specifico.

Separare nell'ADR quattro decisioni che non sono sinonimi:

1. **Render in memoria** sul client della risposta ricevuta.
2. **Cache breve** di richiesta/risposta per contenere costo e latenza.
3. **Persistenza della route** associata al viaggio, eventualmente con geometria o solo con identificatore/riepilogo.
4. **Ricalcolo/retrieval** futuro della route, anche dopo modifica delle tappe o cambio provider.

Un diritto di visualizzazione non implica automaticamente quello di cache, archiviazione permanente, export, offline use o combinazione con mappe di un altro vendor. Finché i termini non sono verificati, progettare l'architettura come se questi diritti **non fossero acquisiti**: conservare i dati Roamly immessi dagli utenti e il riferimento alle coordinate secondo ADR-0001, ma non persistire una geometria provider come dato durevole senza approvazione contrattuale.

## 7. Dati camper, aree di sosta e responsabilità del risultato

- **Camper non è truck commerciale.** Il profilo veicolo dovrebbe esprimere dimensioni e pesi effettivi configurati dall'utente e distinguere, se richiesto, massa a pieno carico, massa corrente, carico per asse, rimorchio e accessori. Non assumere che “truck mode” produca una garanzia per un camper.
- La qualità delle restrizioni è un dato geografico, non una conseguenza automatica dell'API. Per ogni esito servono test rappresentativi su itinerari sintetici in aree con restrizioni note, confronto degli avvisi, gestione “nessun percorso/nessun dato/violazione” e disclaimer UX. Nessun risultato va presentato come omologazione o certificazione di sicurezza.
- **Aree di sosta** restano fuori dal perimetro comparativo dei POI generici. Non importare dati, categorie, immagini, descrizioni o recensioni da Park4Night o servizi simili senza permesso esplicito e licenza compatibile. Per ogni dataset candidato occorre provare provenienza, diritti di redistribuzione, aggiornamento e politica per la segnalazione di errori.
- Le mappe devono mostrare attribution leggibile e stabile, non coperta da Ribbon, pannelli o controlli. R19 vieta l'uso di CDN di terzi per font; non estende automaticamente il divieto a provider cartografici, ma la scelta tiles va comunque approvata nel contesto privacy/contratti e deve essere esplicita.

## 8. Rischio lock-in e costo d'integrazione

| Direzione | Vantaggio | Rischio / complessità |
|---|---|---|
| **Un vendor end-to-end** | Setup e fatturazione potenzialmente più semplici; una sola attribuzione e integrazione. | Può forzare una scelta subottimale per camper o places; le licenze per routing, search, tiles e geometria possono essere diverse. Cambio vendor può richiedere mappatura di tutti i contratti e dei dati memorizzati. |
| **Vendor distinti per routing e map/search** | Scegliere la migliore capacità per requisito; si può sostituire un servizio senza sostituire l'intera mappa. | Più accordi, chiavi, attribution, policy privacy, latenza, billing, formati e incompatibilità di rendering/geometria. L'overlay di una route su tiles diversi va verificato contrattualmente e tecnicamente. |
| **Provider hosted dietro un adapter backend** | Il frontend usa DTO e concetti Roamly, non payload vendor; chiavi non esposte nel browser, logging/consenso e sostituzione centralizzati. | L'adapter non rimuove i termini o l'invio di coordinate al provider. Può diventare un'interfaccia prematura se esiste un solo adapter; mantenerlo piccolo e specifico alle capability realmente necessarie. |
| **OSM/GraphHopper self-hosted** | Maggiore controllo operativo e possibile contenimento del trasferimento verso provider API terzi; il percorso può essere rigenerato sul proprio deployment. | Provisioning, ingest, aggiornamento dati, SLA, scaling, attribuzione/ODbL e validazione del dataset diventano responsabilità Roamly. Maggiore costo fisso e supporto ingegneristico; l'hosting non garantisce copertura camper. |

Non proporre ora una generalizzazione multi-provider completa. La futura seam dovrebbe esprimere capability di prodotto (ricerca, geocoding, route plan, route geometry) e normalizzare solo le differenze realmente necessarie per una slice. Prima di introdurre un port pubblico/esteso, servono un provider reale e almeno un fake di test o una seconda implementazione giustificata: evitare un adapter layer che sia solo un pass-through e non nasconda policy utile.

## 9. Raccomandazione e prossimo spike

### Shortlist

1. **HERE Routing v8** — candidato primario da provare per i vincoli camper documentati e la geometria.
2. **PTV Developer Routing API** — confronto condizionato alla disponibilità di una specifica tecnica precisa e di un'offerta scritta adeguata a un prodotto personale; non sprecare lo spike su una demo generica se prezzi e condizioni restano inaccessibili.
3. **GraphHopper hosted e/o self-hosted su OSM** — opzione di confronto per dati/controllo e costi, con test espliciti di qualità restrizioni e valutazione operativa separata.
4. **TomTom Orbis v3** — includere nel confronto map/search e come benchmark generale, ma non assegnare per ora il ruolo di route provider camper a causa del gap documentato sulle dimensioni.
5. **OSM public tiles/Nominatim** — includerli solo come baseline documentale, non come raccomandazione production.

### Spike raccomandato (massimo tre giorni, solo dopo autorizzazione dell'attività)

1. **Giorno 1 — matrice di prova e termini.** Fissare con l'utente i campi camper da testare, la definizione di “route utile” e i casi sintetici (dimensioni e pesi diversi, tappe, warning, route impossibile); confrontare i requisiti senza inviare coordinate reali. Ottenere da HERE/PTV/TomTom/GraphHopper conferma su retention, localizzazione, DPA, storage dei risultati, overlay/attribution, quote, free tier e soglie.
2. **Giorno 2 — valutazione tecnica con dati sintetici.** Se autorizzata e con account/test credential esplicitamente approvati, provare solo itinerari sintetici. Confrontare route geometry, limiti, warning/violations, parametri veicolo effettivamente accettati e route-display. Se l'utente non autorizza account/API test, rimanere alla verifica documentale e usare schemi/mocks offline: il mancato test live va riportato, non aggirato.
3. **Giorno 3 — decision packet.** Valutare map/tiles, geocoding/search e routing separatamente; stimare costo a volumi espliciti ma non inventati; riportare failure semantics, privacy transfer, diritti storage, vendor exit cost e lacune. Preparare ADR `geo-data-strategy` con una scelta per capability oppure un'opzione differita.

**Gate prima dell'ADR:** non approvare conservazione della geometria o invio di coordinate reali finché non sono documentati termini applicabili e decisione privacy/consenso. Non introdurre ora tipi spatial o migrazioni; ADR-0001 resta invariato.

**Aggiornamento decisionale (2026-10-13):** l'utente ha scelto di proseguire solo con analisi documentale/OpenAPI e di non selezionare un provider. L'OpenAPI HERE dichiara autenticazione Bearer o API key; non è stata effettuata alcuna chiamata, né richiesta o trasmessa credenziale. La valutazione delle API resta documentale: nessuna capacità o qualità di routing è stata verificata live, e la decisione #3 rimane aperta.

### Verifica documentale aggiuntiva (2026-10-20)

Verifica limitata a documentazione pubblica ufficiale; nessuna API è stata chiamata, non sono stati creati account né usati credenziali o coordinate. I dettagli completi e i link sono nella sezione [Fonti ufficiali](#10-fonti-ufficiali-consultate).

- **HERE Routing v8:** la pagina ufficiale sul truck routing, aggiornata il 2026-10-07, conferma `transportMode=truck`, restrizioni legali e parametri fisici (altezza, larghezza, lunghezza, peso corrente/lordo e peso per asse). I parametri omessi assumono `0`/`none`; inviare un profilo incompleto non dimostra un percorso compatibile. La pagina pricing pubblica invita a descrivere le esigenze per ricevere una soluzione: non stabilisce un costo applicabile a Roamly. Termini di retention, persistenza/riuso delle geometrie e condizioni contrattuali del prodotto restano da confermare.
- **TomTom Orbis Routing v3:** la guida ufficiale corrente consultata conferma `car` come unico `travelMode`; usa il peso e la velocità massima, ma non dimensioni, peso per asse o restrizioni ADR. La guida riporta i test dei campi del veicolo effettuati il 2026-09-01. È quindi un candidato possibile per map/search, non un candidato dimostrato per il routing camper. Prezzi applicabili e diritti di conservazione/riuso della geometria restano da verificare nei termini del piano scelto.
- **PTV Developer:** la pagina ufficiale Routing API descrive routing per camion e veicoli commerciali, citando dimensioni, peso, profili di velocità e restrizioni. Non basta però a confermare i parametri e la copertura esatti per un camper, né retention, storage della geometria, attribution o prezzi applicabili. La pagina pricing non è stata verificabile all'URL consultato; non si deduce da ciò che non esista una tariffa o un'offerta.
- **GraphHopper hosted:** la pagina pricing corrente descrive un piano gratuito e prezzi basati su crediti, con piani indicati tra 500 e oltre 5.000.000 chiamate/giorno, ma non determina un costo e un plafond applicabili allo specifico uso Roamly. La privacy policy, ultima modifica 2025-08-25, dichiara che le request API e i relativi body, header, IP e timestamp sono conservati fino a cinque settimane su server Hetzner in Germania; la geocodifica può inoltrare richieste a provider ulteriori. I termini pubblici consultati risultano ultima modifica 2021-08-09 e non sono una base sufficiente per approvare oggi persistenza o riuso dei risultati.
- **OSMF public tiles:** la policy conferma best-effort senza SLA, attribution visibile e caching secondo gli header (o almeno sette giorni se il client non può leggerli); vieta bulk download e prefetch. I tile pubblici non sono un backend production gratuito garantito. La policy riguarda i tile server, non la licenza del dataset OSM né la copertura di un routing engine camper.

**Esito:** la verifica aggiorna alcune capacità dichiarate, ma non chiude la #3. Mancano ancora prove pratiche di qualità delle restrizioni, condizioni applicabili per retention/storage e un costo verificabile per il caso d'uso. La selezione di un provider, l'invio di posizioni dell'utente, l'uso di credenziali/API live, contatti vendor e implementazione restano non autorizzati; la Phase 3 resta bloccata.

### Matrice preparatoria e domande ai vendor — non eseguita

Materiale di preparazione approvato il **2026-10-20**. È una traccia per il futuro spike, non una prova eseguita né un'autorizzazione a inviare richieste. I profili camper sono sintetici e i loro valori numerici andranno fissati prima di qualunque test live; non rappresentano il camper dell'utente. I corridoi e le coordinate di test non sono ancora scelti.

| ID | Caso | Verifica prevista | Evidenza richiesta / limite |
|---|---|---|---|
| T0 | Schema e parametri | Confrontare i campi del profilo camper supportati dalla documentazione/OpenAPI con altezza, larghezza, lunghezza, peso totale e peso per asse; includere unità e comportamento dei campi omessi. | Matrice versionata dei campi supportati; distinguere rifiuto esplicito da campo ignorato o default non sicuro. Verifica documentale, nessuna request. |
| T1 | Profilo sintetico compatto | Itinerario A→B non personale con un profilo sintetico più piccolo, completo e con dimensioni/pesi espliciti. | Solo dopo eventuale autorizzazione live: geometria parsabile, riepilogo e parametri effettivamente applicati. Non interpretare una route come garanzia di sicurezza. |
| T2 | Profilo sintetico grande | Stesso itinerario e stessi punti, ma con dimensioni/pesi sintetici maggiori; confronto del percorso e delle notifiche. | Per concludere serve un corridoio di prova con restrizione documentata e indipendentemente nota; senza ground truth si registra solo la risposta del provider, non la correttezza. |
| T3 | Profilo incompleto | Omettere un attributo alla volta (per esempio altezza o peso) e confrontare la semantica documentata. | Il prodotto non deve dichiarare “compatibile” quando un parametro necessario è assente o non verificato. |
| T4 | Warning, violazione critica e nessun percorso | Separare route normale, route con warning/violazione critica e impossibilità di calcolo. | Conservare una distinzione esplicita tra compatibile verificato, stima, warning e non verificato; nessun esito positivo dedotto dalla sola assenza di warning. |
| T5 | Geometria e visualizzazione | Verificare formato, precisione, CRS, limiti di waypoint e compatibilità della route con i tiles del provider o di terzi. | Il test tecnico non concede diritti di storage/cache/overlay; questi richiedono termini applicabili e attribution documentati. |
| T6 | Ricerca e privacy | Separare ricerca testuale generica, geocoding e invio di origine/destinazione o posizione del dispositivo. | Per ciascuna capability documentare dati trasferiti, destinatari/sub-processori, regione, retention, DPA e necessità di consenso. Nessun dato reale in questo spike. |
| T7 | Costi e limiti | Identificare unità fatturata, quota inclusa, overage, limiti di rate e condizioni di test. | Chiedere tariffa/formula ufficiale; stimare il costo solo dopo avere un volume Roamly approvato, senza inventare una previsione d'uso. |
| T8 | Failure e limiti operativi | Documentare timeout, rate limit, errori di quota, indisponibilità, retry e stabilità delle API. | Definire una risposta di prodotto degradata e verificabile; nessun retry/costo aggiuntivo senza limiti espliciti. |

#### Domande comuni da sottoporre ai provider, solo dopo approvazione dell'invio

1. Quale API/prodotto/versione supporta in modo contrattuale un camper con altezza, larghezza, lunghezza, peso totale e, se disponibile, peso per asse? Quali campi vengono ignorati, rifiutati o impostati a default quando mancanti?
2. Qual è la copertura verificata in Italia/Europa per i limiti fisici e legali? Come sono datate e aggiornate le fonti? Quali warning indicano violazioni note, dati mancanti o copertura incerta?
3. L'assenza di warning significa soltanto “nessun vincolo noto nel dataset” o costituisce una verifica di compatibilità? Quali sono le semantiche di route impossibile e violazione critica?
4. Roamly può conservare nel viaggio la geometria, il riepilogo e gli identificatori dei risultati? Per quanto tempo, con quali cache/refresh, deletion e limiti di uso? È consentito visualizzare la geometria sopra tiles di un altro provider?
5. Quali dati di request, IP, log e coordinate sono trattati; in quale regione; per quanto tempo; con quali sub-processori e DPA? Il provider riceve coordinate anche quando la chiamata passa da un proxy backend?
6. Quali sono unità di fatturazione, quote incluse, overage, rate limit, policy d'uso personale/commerciale e condizioni per un test senza impegno? Fornire il tariffario o una formula applicabile, non solo una fascia generica.
7. Quali sono obblighi di attribution, branding, caching e termini separati per routing, search/geocoding e map tiles? Sono ammesse le combinazioni di prodotti previste?
8. Sono disponibili credenziali sandbox e casi di prova con restrizioni documentate? Quali limiti di chiamata e costi si applicano alla sandbox?

#### Domande specifiche per i candidati

- **HERE:** confermare il prodotto e il profilo corretti per un camper europeo; distinguere `transportMode=truck` da `car` con proprietà fisiche; indicare quali restrizioni per camper sono effettivamente coperte e i default di ogni campo omesso.
- **PTV Developer:** indicare i nomi/tipi/unità esatti dei parametri di veicolo, i limiti dimensionali supportati e una risposta di esempio che mostri geometria, warning e violazioni.
- **GraphHopper hosted:** chiarire quali attributi camper sono realmente applicati dal profilo scelto, l'eventuale inoltro di geocoding, le condizioni attuali di storage/riuso e il costo a consumo. Per self-hosted, separare i costi/obblighi del servizio dai diritti e aggiornamenti dei dati OSM.
- **TomTom Orbis v3:** confermare i limiti documentati di `car`/peso e quali capability map/search restano disponibili senza attribuire al routing v3 supporto per dimensioni camper.

**Gate:** prima di qualunque test live servono approvazione separata del provider, del perimetro, della credenziale di test e dell'eventuale tetto di spesa. Prima di inviare domande servono approvazione del destinatario e del testo finale. In entrambi i casi sono esclusi coordinate o dati personali. Nessuna selezione provider, persistenza di geometrie, migrazione spatial o implementazione Phase 3 è approvata da questa matrice.

### Esito decisionale (2026-10-20)

La policy pubblica Nominatim, verificata il 2026-10-20, vieta autocomplete e l'invio di dati personali/confidenziali, limita l'uso a un massimo assoluto di una richiesta al secondo e può cambiare senza preavviso. La policy dei tile OSMF dichiara servizio best-effort senza SLA e richiede attribution e caching. Dopo aver considerato questi vincoli, l'utente ha scelto di **rinviare la Phase 3 e differire la decisione geo #3**. La precedente valutazione dei servizi pubblici OSM non è quindi un'adozione. Nessun account, API, coordinata, contatto vendor o provider è stato usato/selezionato; il routing camper resta fuori scope fino a una decisione futura.

## 10. Fonti ufficiali consultate

Fonti consultate il **2026-10-13** e, per la verifica aggiuntiva, il **2026-10-20**. Le pagine di documentazione API e prezzi sono dinamiche. Quando una pagina non ha reso accessibili dettagli puntuali, la relativa lacuna è stata mantenuta esplicita invece di dedurre valori o diritti.

### Roamly — fonti interne

- [ADR-0001 — SQL Server come database applicativo, tipi spaziali rimandati](../adr/0001-use-sql-server.md)
- [FEATURES.md §3–4 — Trip Planner e Vehicle-Aware Routing](FEATURES.md)
- [ROADMAP.md §2 — Phase 3 e Phase 4](ROADMAP.md)
- [OPEN-DECISIONS.md — decisione #3 e privacy/consenso](../OPEN-DECISIONS.md)

### HERE

- [Routing API v8 — Truck routing](https://docs.here.com/routing/docs/routing-v8-truck-routing) — restrizioni e parametri truck; pagina aggiornata 2026-10-08.
- [Routing API v8 — Vehicle properties](https://docs.here.com/routing/docs/routing-v8-vehicle-properties) — proprietà veicolo e esempio polyline/notices; pagina aggiornata 2026-08-07.
- [HERE Geocoding & Search — docs](https://docs.here.com/geocoding-and-search/docs/overview) — documentazione ufficiale del prodotto; la pagina aperta in questa verifica è un overview di versione, non un catalogo completo di limiti commerciali.
- [HERE Pricing](https://www.here.com/get-started/pricing) — il contenuto pubblico consultato invita a descrivere le esigenze; non dà qui una tariffa comparabile.
- [HERE Platform Terms](https://legal.here.com/en/terms/here-platform) — termini standard; possono applicarsi condizioni aggiuntive o modificate nel customer agreement.
- [HERE Platform Data Processing Agreement](https://here-legal-files-prod.s3.amazonaws.com/files/HERE_Platform_Data_Processing_Agreement.pdf) — DPA da associare al prodotto/contratto effettivamente selezionato.
- [HERE Base Plan Restrictions](https://www.here.com/get-started/pricing/base-plan-restrictions) — restrizioni del piano base da rivedere se si considera quel piano.
- [HERE Routing API v8 — Reference](https://docs.here.com/routing/reference/routing-api-v8-calculateroutes) — riferimento dei parametri e della risposta.

### TomTom

- [Orbis Routing v3 — Routing service](https://docs.tomtom.com/routing-api/documentation/tomtom-orbis-maps/v3/routing-service) — overview ufficiale corrente e accesso per evaluation.
- [Orbis Routing v3 — Vehicle configuration (Maps SDK for JavaScript)](https://docs.tomtom.com/maps-sdk-js/guides/services/routing/vehicle) — tabella di parametri e verifica documentata il 2026-09-01, inclusi i campi non accettati.
- [Orbis Routing v3 — Migration guide](https://developer.tomtom.com/routing-api/documentation/tomtom-orbis-maps/v3/product-information/migration-guide) — differenze request/response e geometria (`path` GeoJSON, `compressedPath`).
- [TomTom Search API — Introduction](https://docs.tomtom.com/search-api/documentation/product-information/introduction) — search, autocomplete, categorie e Place by ID.
- [TomTom Maps Routing v1 — Calculate Route](https://docs.tomtom.com/routing-api/documentation/tomtom-maps/v1/calculate-route) — generazione precedente; documenta route geometry e parametri veicolo. Non prova che v1 abbia lo stesso ciclo di vita o termini di Orbis v3.
- [TomTom Pricing / piattaforma](https://docs.tomtom.com/pricing) — pagina corrente consultata; distingue generazioni Orbis e TomTom Maps, ma non ha reso qui un listino numerico API applicabile.
- [TomTom Map Display API — Introduction](https://docs.tomtom.com/map-display-api/documentation/tomtom-maps/v1/product-information/introduction) — prodotto tiles; i termini storage/offline vanno verificati separatamente.
- [TomTom Privacy Policy](https://www.tomtom.com/privacy/) — informativa da associare a DPA/condizioni API specifiche.

### PTV

- [PTV Routing API — Guide](https://developer.myptv.com/en/documentation/routing-api) — documentazione del prodotto; la pagina aperta non ha esposto in modo leggibile in questa verifica i dettagli completi dei parametri/risposta.
- [PTV Logistics Developer APIs](https://www.ptvlogistics.com/en/logistics-developer-apis) — catalogo ufficiale delle API logistiche/geografiche.
- [PTV Developer — Product Description](https://www.ptvlogistics.com/en/product_description_ptv_developer?inline) — descrizione ufficiale del prodotto, documento PDF.
- [PTV Developer Maps API](https://www.ptvlogistics.com/en/ptv-developer-maps-API) — overview ufficiale map API.
- [PTV Terms & Privacy portal](https://www.ptvgroup.com/en/terms-privacy) — portale legal/privacy generale, non sostituisce DPA/termini specifici del servizio Developer.
- [PTV Data Privacy Statement Communication](https://www.ptvlogistics.com/en-us/Data_Privacy_Statement_Communication_EN.pdf) — informativa accessibile per comunicazioni; non è stata considerata prova sufficiente della retention dei payload routing API.

### GraphHopper

- [GraphHopper Directions API — Routing](https://docs.graphhopper.com/openapi/routing) — API di routing.
- [GraphHopper Directions API — Custom Profiles](https://docs.graphhopper.com/openapi/custom-profiles) — profili personalizzati.
- [GraphHopper Directions API — Map Data and Routing Profiles](https://docs.graphhopper.com/openapi/map-data-and-routing-profiles) — dati e profili routing.
- [GraphHopper Directions API — Pricing](https://www.graphhopper.com/pricing/) — pagina ufficiale; la pagina consultata indica aggiornamento 2025-08-05, quindi prezzi e soglie vanno riconfermati prima di un business case.
- [GraphHopper Privacy](https://www.graphhopper.com/privacy/) — last modified 2025-08-25; descrive request retention fino a 5 settimane, hosting API in Germania presso Hetzner e possibili inoltri a provider terzi per geocoding.
- [GraphHopper Terms](https://www.graphhopper.com/terms/) — termini pubblici consultati; last modified 2021-08-09, quindi non considerati sufficienti per autorizzare persistenza o uso commerciale attuale.

### OpenStreetMap / OSM Foundation

- [OpenStreetMap — Copyright and ODbL](https://www.openstreetmap.org/copyright) — licenza, attribuzione e nota che OSMF non fornisce un map API/tiles gratuito a terzi.
- [OSMF Tile Usage Policy](https://operations.osmfoundation.org/policies/tiles/) — limiti del tileserver pubblico, SLA, attribution, caching e divieto bulk/prefetch.
- [OSMF Nominatim Usage Policy](https://operations.osmfoundation.org/policies/nominatim/) — massimo una request al secondo per app, no autocomplete, uso diretto end-user solo con moderazione, privacy, cache e switchabilità.
- [Open Data Commons ODbL 1.0](https://opendatacommons.org/licenses/odbl/1-0/) — testo della licenza dei dati OSM.
