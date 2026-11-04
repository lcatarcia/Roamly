# Decision Map: Wishlist dei luoghi da visitare

## #1: Scegliere la composizione della sezione

Bloccato da: Nessuno  
Tipo: Discuss  
Severità: LOW  
Stato: Answered

### Domanda

Come presentare una raccolta personale di luoghi da visitare, considerando desktop e mobile?

### Opzioni

1. Mappa e elenco sincronizzati.
2. Mappa a tutto schermo.
3. Elenco principale con mappa apribile.
4. Raccolte tematiche, ciascuna con mappa.
5. Altro / proposta custom

### Risposta

Scelta dell'utente (2026-11-03): **mappa ed elenco sincronizzati**. La mappa è la vista principale; l'elenco resta disponibile per ricerca e consultazione, con selezione sincronizzata tra voci e segnaposto. Su mobile, l'elenco può occupare un pannello espandibile.

### Assets

- Nessuno.

## #2: Verificare l'esperienza mappa + elenco

Bloccato da: Nessuno  
Tipo: Prototype  
Severità: MEDIUM  
Stato: Answered

### Domanda

La composizione scelta consente di esplorare, trovare e consultare facilmente i luoghi su desktop e mobile?

### Opzioni

1. Mappa principale con elenco laterale su desktop e pannello espandibile su mobile — **RACCOMANDATA**.
   - Motivo: rispetta la scelta dell'utente e conserva accessibile la wishlist anche su schermi piccoli.
   - Pro: confronto spaziale e scansione dell'elenco nella stessa vista.
   - Contro: richiede attenzione a spazio disponibile, accessibilità e sincronizzazione dello stato.
   - Impatto: definisce il flusso della sezione, senza impegnare un provider geografico.
   - Complessità: media.
2. Mappa a tutto schermo con risultati sovrapposti.
   - Pro: massimizza lo spazio cartografico.
   - Contro: può coprire la mappa e rende meno immediato consultare molti risultati.
   - Impatto: maggiore dipendenza da pannelli e overlay.
   - Complessità: media.
3. Elenco dominante con mappa ridotta o collassabile.
   - Pro: ricerca e accessibilità dell'elenco più semplici.
   - Contro: la mappa perde centralità.
   - Impatto: la selezione sincronizzata resta necessaria tra viste.
   - Complessità: medio-bassa.
4. Mappa statica illustrativa con interazioni simulate.
   - Pro: permette di valutare gerarchia e disposizione senza provider.
   - Contro: non verifica l'interazione cartografica reale.
   - Impatto: prototipo isolato, nessun servizio esterno.
   - Complessità: bassa.
5. Altro / proposta custom

### Risposta

Prototipo disponibile dal 2026-11-03, solo in Development, alla route `/__dev/places-wishlist?variant=A`. Avvio dalla cartella `frontend\roamly-web`: `npm run dev`. Tre composizioni mantengono mappa ed elenco sincronizzati: **A** elenco laterale e dettaglio sotto la mappa, **B** pannello flottante sulla mappa, **C** mappa sopra ed elenco/dettaglio sotto. Il selettore inferiore aggiorna `?variant=A|B|C`.

La mappa e i luoghi sono inventati, senza significato geografico. Ricerca, filtro, selezione e aggiunta funzionano solo in memoria; ricaricare ripristina i dati demo. I link Google Maps sono testo inerte: nessuna risoluzione, geocodifica, richiesta esterna o apertura automatica. Un'aggiunta resta senza posizione e compare soltanto nell'elenco. Il prototipo non decide il comportamento definitivo dei link (#3).

La verifica locale copre interazioni sincronizzate, aggiunta senza posizione, varianti e pannelli mobile senza overflow orizzontale. Il 2026-11-03 l'utente ha delegato le scelte restanti privilegiando il risparmio: **scelta la variante A** come base del piano, perché mantiene lista e dettaglio semplici senza overlay aggiuntivi. È una scelta su delega, non una validazione d'usabilità dell'utente; navigazione geografica, zoom e densità di segnaposto restano da verificare nella vera integrazione. Non promuovere automaticamente il codice throwaway.

### Assets

- [Pagina del prototipo](../../frontend/roamly-web/src/features/places/prototype/PlacesWishlistPrototypePage.tsx)
- [Selettore delle varianti](../../frontend/roamly-web/src/features/places/prototype/PrototypeSwitcher.tsx)

## #3: Definire il comportamento dei link Google Maps

Bloccato da: Nessuno  
Tipo: Discuss  
Severità: MEDIUM  
Stato: Answered

### Domanda

Quando si incolla un link Google Maps, quali dati deve estrarre Roamly e come deve gestire link abbreviati o non interpretabili?

### Opzioni

1. Conservare il link originale soltanto; l'utente inserisce manualmente nome e posizione — **SCELTA per la prima versione**.
   - Pro: comportamento prevedibile, senza dipendenze esterne.
   - Contro: più inserimento manuale.
   - Impatto: il link è un riferimento, non una fonte di dati.
   - Complessità: bassa.
2. Conservare sempre il link e provare a leggere i dati da formati URL supportati; chiedere conferma e consentire la correzione manuale.
   - Motivo: offre comodità senza rendere il salvataggio dipendente dall'estrazione; i formati non supportati degradano in modo esplicito.
   - Pro: mantiene il link e gestisce i casi incompleti senza inventare coordinate.
   - Contro: i formati supportati vanno definiti e testati; un link abbreviato può non fornire dati sufficienti.
   - Impatto: estrazione facoltativa, posizione verificata dall'utente.
   - Complessità: media.
3. Risolvere automaticamente ogni link tramite un servizio Google.
   - Pro: potenzialmente più dati precompilati.
   - Contro: introduce dipendenza, costi/licenze e trasferimento di dati da valutare; non è garantito che ogni link sia risolvibile.
   - Impatto: richiede una decisione esplicita sul provider e sulle relative condizioni.
   - Complessità: alta.
4. Conservare il link e chiedere sempre di selezionare o confermare la posizione sulla mappa.
   - Pro: posizione esplicita e verificata dall'utente.
   - Contro: richiede la mappa anche nel flusso di inserimento.
   - Impatto: dipende dal provider cartografico scelto, ma non dalla risoluzione automatica del link.
   - Complessità: media.
5. Altro / proposta custom

### Risposta

Decisione sul piano del 2026-11-03, sulla delega dell'utente: **opzione 1**, link HTTPS opaco, conservato e apribile solo volontariamente. Nessun fetch, espansione short-link, anteprima remota o estrazione di nome/coordinate. La precedente raccomandazione di precompilazione lascia il posto alla soluzione manuale per ridurre dipendenze, consumo e incertezze sui diritti di importazione. Nome obbligatorio, posizione facoltativa; click sulla mappa o coordinate manuali per localizzare. Un luogo senza posizione resta salvabile e non ha marker.

### Assets

- Nessuno.

## #4: Verificare il provider geografico per Maps & Places

Bloccato da: decisione #3 di [`OPEN-DECISIONS.md`](../OPEN-DECISIONS.md) e relativo spike/ADR  
Tipo: Research  
Severità: HIGH  
Stato: Open

### Domanda

Quale provider e quali fonti geografiche soddisfano la wishlist, tenendo conto di visualizzazione mappa, ricerca, costi, licenze di memorizzazione e privacy?

### Opzioni

1. Google Maps Platform.
   - Pro: ecosistema integrato per mappe e luoghi.
   - Contro: costi, condizioni d'uso e regole di memorizzazione da verificare.
   - Impatto: scelta di un provider esterno per mappa e, se approvato separatamente, ricerca.
   - Complessità: media.
2. Azure Maps.
   - Pro: opzione già documentata nella ricerca costi e privacy del progetto.
   - Contro: non equivale a una scelta già approvata; copertura e condizioni vanno validate sul caso d'uso.
   - Impatto: introduce dipendenza Azure nella feature.
   - Complessità: media.
3. Provider basato su OpenStreetMap o servizio compatibile.
   - Pro: più possibilità di separare i dati dalla UI del provider.
   - Contro: copertura, licenze, affidabilità e costi dipendono dal servizio concreto.
   - Impatto: richiede selezionare e verificare una fonte e un servizio specifici.
   - Complessità: media-alta.
4. Differire la mappa live e realizzare solo salvataggio/elenco.
   - Pro: evita una dipendenza geografica immediata.
   - Contro: non soddisfa il requisito della mappa per la sezione completa.
   - Impatto: rinvia Maps & Places.
   - Complessità: bassa inizialmente.
5. Altro / proposta custom

### Risposta

**Candidato scelto dall'utente il 2026-11-03: Azure Maps**, dopo confronto con MapTiler Free, Geoapify Free e servizi OSM pubblici. Priorità al costo minimo, con uso delle quote gratuite; l'utente ha scelto esplicitamente l'opzione che ammette possibili costi a consumo. Gli avvisi Azure non sono un blocco degli addebiti. La scelta identifica il provider da approfondire per mappa e ricerca della wishlist, non autorizza creazione di risorse, richieste live o spese; nessun budget massimo è stato concordato.

Sulla delega successiva dell'utente, **prima versione limitata al rendering Azure su richiesta e ai dati manuali**: niente Search, autocomplete o reverse geocoding. La ricerca interna usa la raccolta owner-scoped in Roamly. Rinunciare alla ricerca esterna riduce consumi e questioni di conservazione dei POI, ma richiede inserimento manuale e non rende gratuita la mappa.

Prima dell'integrazione: verificare termini di rendering/coordinate, trattamento di viewport/IP, autenticazione e controllo dei consumi con uno spike autorizzato separatamente; completare l'ADR e i gate pertinenti. Il ticket resta **Open** per queste verifiche. Search e qualità dei POI diventano un approfondimento futuro distinto; la wishlist non decide il routing vehicle-aware e il gate geografico generale #3 non viene chiuso implicitamente.

**Ricognizione del 2026-11-03:** approfonditi documentazione autenticazione/licenza e comportamenti impliciti del Web SDK ([ricerca §11](AZURE-MAPS-RESEARCH.md#11-approfondimento-per-la-wishlist-manuale--2026-11-03)). Mancano consenso runtime e prova del flusso reale; va disabilitato il fallback geocoding per l'accessibilità. Dopo login interattivo autorizzato, verificata la corrispondenza di subscription e tenant con `azure-release`: l'inventario read-only restituisce zero account Maps nell'intera subscription. La managed identity runtime esiste ed è già collegata alla Container App. L'accesso corretto è ripristinato; non sono stati autorizzati o eseguiti provisioning, modifiche RBAC o traffico Maps, né verificate quote residue o prezzo effettivo dell'offerta.

**Autenticazione e modello di consenso confermati per il piano:** l'utente ha scelto «Token Entra tramite API esistente (Raccomandata)» e «Registro unico grant/revoke (Raccomandata)» in Identity, senza autorizzare implementazione, migration, modifiche cloud o spesa. Restano da definire schema dettagliato, testo e retention della prova, oltre a licenza, configurazione concreta, limiti economici e spike. Il disegno di consenso e le prove richieste sono nel [piano](PLACES-WISHLIST-PLAN.md#consenso-trasversale-modello-scelto-e-dettagli-aperti).

### Assets

- [`OPEN-DECISIONS.md` #3](../OPEN-DECISIONS.md)
- [`AZURE-MAPS-RESEARCH.md`](AZURE-MAPS-RESEARCH.md)
- [`PLACES-MAP-PROVIDER-COST-RESEARCH.md`](PLACES-MAP-PROVIDER-COST-RESEARCH.md): confronto documentale del 2026-11-03; l'utente ha scelto Azure Maps come candidato, senza autorizzare attivazione o spese.

## Perimetro corrente

- [Piano della prima versione](PLACES-WISHLIST-PLAN.md): decisioni su delega, compromessi economici, sequenza e criteri di accettazione.
- La sezione è una wishlist personale indipendente da un viaggio; riusa il concetto e lo schema strutturale di `SavedPlace` già descritti in [`architecture/CONTEXT.md`](../architecture/CONTEXT.md), evitando un secondo catalogo personale parallelo.
- Le categorie devono coprire città, paesi, locali, musei e attrazioni, oltre ai tipi di sosta e servizio già previsti.
- Un luogo senza coordinate resta consultabile nell'elenco e non appare come segnaposto.
- Il riuso come tappa sfrutta il collegamento previsto tra `TripStop` e `SavedPlace`; la tappa mantiene il proprio snapshot.
- Raccolte tematiche, stato «visitato», recensioni, suggerimenti AI e routing non sono requisiti approvati per la prima versione.
- La decisione sul provider geografico resta un gate HIGH: nessuna mappa live o trasmissione di posizioni a terzi prima della decisione e delle verifiche di privacy.
