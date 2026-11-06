# Ricerca documentale — Azure Maps per Roamly

- **Verifica documentale:** 2026-10-22
- **Stato:** ricerca preliminare; nessun provider selezionato e nessuna integrazione autorizzata
- **Perimetro:** costi mensili, Maps/Search/Routing per un possibile MVP della Phase 3, privacy, autenticazione, limiti d'uso e licenza

**Lettura cronologica:** l'intestazione e le prime sezioni descrivono la ricerca iniziale. La sezione 12 registra le successive scelte Azure, letture control-plane e modifiche RBAC approvate del 2026-11-06. Nella stessa data l'utente ha richiesto un'implementazione piu' semplice per uso personale: il contratto economico rigoroso resta distinto e non blocca la nuova vista manuale. La soglia di 1 EUR/mese non viene presentata come controllo automatico attivo o tetto alla fattura. Non sono autorizzati nuovi deploy o test Maps a consumo; [perimetro corrente](PLACES-WISHLIST-PLAN.md#aggiornamento-percorso-personale-semplice--2026-11-06).

> Questa ricerca usa esclusivamente fonti documentali pubbliche Microsoft/Azure e il repository del wrapper React gestito dal team Azure Maps. Non sono stati creati account o risorse, usate credenziali o API live, né trasmessi dati o coordinate di Roamly. Non è stata implementata alcuna integrazione. Le stime sono modelli ipotetici, non garanzie di prezzo o di consumo.

## 1. Sintesi esecutiva

La tariffazione corrente è **Azure Maps Gen2, a consumo**, con quote mensili pubblicate come gratuite per ciascun meter. Non è un servizio illimitato né un impegno che l'intero utilizzo futuro rimanga gratuito: oltre la quota, la tariffa dipende dall'offerta Azure, dalla valuta e dai prezzi applicabili; il listino pubblico consultato mostra `$-` nei prezzi unitari e rimanda al calcolatore/contatto commerciale. Le quote sono per meter e misurate in transazioni, non genericamente per sessione o schermata: ad esempio 15 tile equivalgono a una transazione, mentre una chiamata normale di route o search vale una transazione. [Prezzi Azure Maps](https://azure.microsoft.com/en-us/pricing/details/azure-maps/); [calcolo transazioni](https://learn.microsoft.com/en-us/azure/azure-maps/understanding-azure-maps-transactions)

Un MVP limitato a mappa base, ricerca esplicita e routing su richiesta **potrebbe** restare sotto le quote pubblicate con volumi contenuti. Non è però possibile garantirlo senza il profilo di utilizzo effettivo, telemetria del consumo, verifica dell'offerta Azure e stima aggiornata nel calcolatore. Budget e avvisi Azure non sono un hard cap: non fermano la fatturazione e valutano costi con ritardo. [Budget Cost Management](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets)

Azure Maps documenta routing truck in Italia e vari parametri truck, ma ciò **non dimostra** routing sicuro per camper o copertura completa delle relative restrizioni. Le query Search/Route inviano al servizio input geospaziali; Microsoft documenta un endpoint europeo con richieste e input conservati nell'area geografica scelta. Restano da verificare, per il caso d'uso Roamly e i termini applicabili, retention, riuso/cache dei risultati e condizioni di licenza. [Copertura routing](https://learn.microsoft.com/en-us/azure/azure-maps/routing-coverage); [ambito geografico](https://learn.microsoft.com/en-us/azure/azure-maps/geographic-scope)

## 2. Prezzi Gen2 e quote gratuite pubblicate

La tabella Azure Maps è espressa in **transazioni**, normalmente per 1.000 transazioni per i prezzi a pagamento, e riporta un livello “Free” mensile per ciascun meter. La pagina specifica che la fatturazione è mensile e che i prezzi sono stime, possono variare in base ad accordo, valuta e data; raccomanda di usare il calcolatore per simulare il bill. Le quote riportate sotto sono quindi le quote pubblicate, non una garanzia di spesa totale zero per ogni account o ogni scenario. [Pagina prezzi Azure Maps](https://azure.microsoft.com/en-us/pricing/details/azure-maps/)

| Gruppo / meter pubblicato | Quota gratuita mensile pubblicata | Unità / regola di conteggio documentata |
|---|---:|---|
| Maps — Base Map Tiles | 5.000 transazioni | 15 tile = 1 transazione |
| Maps — Imagery Tiles | 1.000 transazioni | 15 tile = 1 transazione |
| Maps — Traffic Tiles | 5.000 transazioni | 15 tile = 1 transazione |
| Maps — Weather Tiles | 1.000 transazioni | 15 tile = 1 transazione |
| Maps — Static Map Images | 1.000 transazioni | 1 richiesta Static Map = 1 transazione |
| Location Insights — Data | 5.000 transazioni | L'etichetta è pubblicata nel listino; la documentazione transazioni consultata non ne mappa chiaramente il perimetro funzionale. |
| Geolocation | 5.000 transazioni | 1 richiesta = 1 transazione |
| Search | 5.000 transazioni | 1 richiesta Search = 1 transazione; autocomplete: 10 richieste = 1 transazione. Ogni elemento/query di batch conta individualmente. |
| Routing | 1.000 transazioni | 1 richiesta Route = 1 transazione; Matrix: 1 transazione ogni 4 celle, arrotondata per eccesso; ogni query batch conta individualmente. |
| Spatial Calculations | 5.000 transazioni | Per Geofencing: 5 chiamate API = 1 transazione; il dettaglio dipende dall'operazione. |
| Time Zone | 5.000 transazioni | 1 richiesta = 1 transazione |
| Traffic | 1.000 transazioni | 1 richiesta Traffic = 1 transazione, esclusi i tile che hanno meter dedicato |
| Weather | 1.000 transazioni | 1 richiesta Weather = 1 transazione |

Quote e unità sono riprese dal [listino](https://azure.microsoft.com/en-us/pricing/details/azure-maps/) e dal riepilogo ufficiale [Understanding Azure Maps Transactions](https://learn.microsoft.com/en-us/azure/azure-maps/understanding-azure-maps-transactions). La documentazione dichiara non fatturabili le chiamate CORS preflight e le risposte HTTP 5xx, 401, 403, 408 e 429. La pagina dei prezzi indica che sconti per volume possono applicarsi a partire da 100.000 transazioni mensili. [Transazioni e casi non billable](https://learn.microsoft.com/en-us/azure/azure-maps/understanding-azure-maps-transactions); [pricing](https://azure.microsoft.com/en-us/pricing/details/azure-maps/)

### SKU e interpretazione del “free”

La documentazione account indica che Azure Maps usa Gen2 e che Gen1 è stato ritirato il 15 settembre 2026, con conversione automatica degli account Gen1 a Gen2. La pagina prezzi promuove Gen2 per nuovi clienti e dichiara una free monthly tier di transazioni. Nelle fonti consultate non è specificato un plafond distinto per chiave, utente o app, né un criterio sufficiente per presumere che sia possibile moltiplicare le quote creando più account. Per prudenza, non moltiplicare le quote né trattare i diversi account/chiavi come “crediti gratuiti” senza conferma contrattuale. [Pricing tier account](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-manage-pricing-tier); [prezzi](https://azure.microsoft.com/en-us/pricing/details/azure-maps/)

La quota “Free” mensile è distinta dal credito promozionale Azure (la stessa pagina pubblica riporta anche offerte di credito Azure per nuovi clienti): non assumere che il credito introduttivo sia permanente o equivalga al free meter Maps. Il consumo oltre le quote segue i prezzi applicabili all'offerta Azure; la pagina pubblica consultata non espone importi numerici utilizzabili per una stima Roamly, ma indica prezzi per 1.000 transazioni, fasce/contatto commerciale e calcolatore. [Prezzi e FAQ](https://azure.microsoft.com/en-us/pricing/details/azure-maps/); [calcolatore](https://azure.microsoft.com/en-us/pricing/calculator/?service=azure-maps)

## 3. Costo probabile e scenari ipotetici

Per un uso Roamly di mappa di viaggio, le voci più direttamente rilevanti sono Base Map Tiles, Search e Routing. Traffic, Imagery, Weather, Geolocation, Static Map, Spatial/Time Zone non sono necessari a un primo perimetro di sola visualizzazione di mappa, ricerca e calcolo percorso; ogni capability aggiunta ricade nei propri meter e/o soglie. [Prezzi](https://azure.microsoft.com/en-us/pricing/details/azure-maps/); [transazioni](https://learn.microsoft.com/en-us/azure/azure-maps/understanding-azure-maps-transactions)

I modelli seguenti non sono previsioni: assumono un numero fisso di richieste tile per apertura e non modellano ogni stile, zoom, pan, retry, dati caricati o uso combinato dell'account. Per le mappe il calcolo usa 15 tile per transazione; per Search e Route usa la regola ordinaria di 1 richiesta per transazione.

| Scenario ipotetico mensile | Base map tile | Search | Routing | Lettura rispetto alle quote pubblicate |
|---|---:|---:|---:|---|
| 100 utenti × 6 aperture × 50 tile; 4 ricerche e 2 route per utente | 30.000 tile ≈ 2.000 transazioni | 400 transazioni | 200 transazioni | Tutti e tre sotto i rispettivi meter pubblicati (5.000 / 5.000 / 1.000). |
| 500 utenti × 8 aperture × 60 tile; 3 ricerche e 2 route per utente | 240.000 tile ≈ 16.000 transazioni | 1.500 transazioni | 1.000 transazioni | Base Map supera la quota; Search resta sotto; Routing raggiunge la quota, prima di altre chiamate. |

Questi calcoli indicano solo la conseguenza aritmetica delle regole Microsoft, non che il carico effettivo del Web SDK per utente sarà proprio 50 o 60 tile. La documentazione Web SDK dice che la mappa carica risorse in modo asincrono e che l'evento `load` si verifica dopo aver caricato la vista iniziale e tutti i tile di mappa; cambi di vista/stile possono richiedere nuove risorse. [Best practice Web SDK](https://learn.microsoft.com/en-us/azure/azure-maps/web-sdk-best-practices)

### Monitoraggio, avvisi e limiti di spesa

- Azure Maps espone metriche account (richieste, errori, disponibilità e utilizzo), consultabili nel portale. Le metriche sono conservate per 93 giorni, con interrogazioni fino a 30 giorni per volta. Sono utili per osservare consumo e anomalie, ma non costituiscono da sole un blocco di spesa. [View API usage metrics](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-view-api-usage)
- Azure Cost Management consente budget mensili e alert su costo effettivo o previsto. **Gli alert non fermano risorse o consumo**. I dati costo/consumo sono tipicamente disponibili dopo 8–24 ore e i budget sono valutati ogni 24 ore, quindi non sono un cap tempestivo e deterministico. [Create and manage budgets](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets)
- Rate limits/QPS e risposte 429 sono limiti di traffico, non un tetto di spesa mensile contrattuale. Una protezione applicativa (es. disabilitare Search/Route oltre un budget interno) sarebbe un controllo progettuale aggiuntivo; questa ricerca non implementa né autorizza tale controllo. [Limiti QPS](https://learn.microsoft.com/en-us/azure/azure-maps/azure-maps-qps-rate-limits); [transazioni](https://learn.microsoft.com/en-us/azure/azure-maps/understanding-azure-maps-transactions)

**Costo effettivo:** da calcolare con il [pricing calculator Azure Maps](https://azure.microsoft.com/en-us/pricing/calculator/?service=azure-maps) selezionando subscription/offerta, regione, valuta e volumi per meter. Il listino pubblico consultato presenta prezzi non numerici (`$-`) per le fasce visibili e invita a contattare Microsoft per determinate fasce; qui non è possibile attribuire un prezzo di overage affidabile a Roamly. [Pricing](https://azure.microsoft.com/en-us/pricing/details/azure-maps/)

## 4. Autenticazione e sicurezza delle credenziali

Azure Maps supporta **Shared Key**, **Microsoft Entra ID** e **SAS token**. Una Shared Key può essere inserita nel client per il funzionamento tecnico, ma Microsoft la definisce il metodo meno sicuro per un'applicazione pubblica: è estraibile dalle richieste e consente l'accesso alle API REST disponibili per l'account. Non ha durata configurabile e deve essere ruotata manualmente; chiavi primaria e secondaria consentono la rotazione pianificata. [Best practice di autenticazione](https://learn.microsoft.com/en-us/azure/azure-maps/authentication-best-practices); [gestione autenticazione](https://learn.microsoft.com/en-us/azure/azure-maps/azure-maps-authentication)

Per un'app web React:

- Il Map Control è client-side e richiede un account Maps e subscription key **oppure** credenziali Microsoft Entra. La guida ufficiale mostra entrambe le configurazioni del controllo; l'esempio non rende la chiave segreta. [Uso del Web Map Control](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-use-map-control)
- Preferire identità/token con ambito e durata controllati ove adeguato. Entra ID integra RBAC e Microsoft raccomanda managed identity per servizi backend confidenziali. SAS è un'altra modalità supportata; va generato e firmato in un contesto server sicuro e il client riceve soltanto un token con scope/durata adatti. La selezione del flusso concreto per una SPA Roamly resta da progettare. [Autenticazione Azure Maps](https://learn.microsoft.com/en-us/azure/azure-maps/azure-maps-authentication); [best practice](https://learn.microsoft.com/en-us/azure/azure-maps/authentication-best-practices)
- CORS consente di restringere gli origin web autorizzati, ma non trasforma una Shared Key pubblicata nel browser in un segreto. La guida descrive CORS per client web e non lo richiede per un servizio server-to-server. [Best practice di autenticazione](https://learn.microsoft.com/en-us/azure/azure-maps/authentication-best-practices)

Per chiamate REST da un backend Roamly, usare identità Entra/managed identity e RBAC least-privilege quando il deployment lo consente, evitando di esporre segreti. Il role “Azure Maps Search and Render Data Reader” è documentato per lettura Search e Render; il ruolo minimo necessario per un servizio che calcola anche route va verificato rispetto alle API effettivamente selezionate. [Autenticazione e ruoli](https://learn.microsoft.com/en-us/azure/azure-maps/azure-maps-authentication)

## 5. Coordinate, trattamento e residenza

Un'API Search trasmette la query cercata; Route richiede origine/destinazione e parametri di percorso, che possono rivelare spostamenti personali. L'esempio Microsoft di Route usa coordinate nella query, mentre l'esempio geografico Search mette il testo ricercato nel parametro `query`. Un proxy Roamly può centralizzare la chiamata, ma non impedisce a Microsoft di ricevere i dati necessari quando il backend chiama Azure Maps. [Autenticazione ed esempio Route](https://learn.microsoft.com/en-us/azure/azure-maps/azure-maps-authentication); [geographic scope ed esempio Search](https://learn.microsoft.com/en-us/azure/azure-maps/geographic-scope)

Azure Maps permette di limitare la residenza all'area UE o USA: l'endpoint `eu.atlas.microsoft.com` mantiene richieste, incluso l'input, nell'area geografica europea; per alta disponibilità/DR Microsoft può replicare i dati in altre regioni della stessa area geografica. Gli endpoint documentati sono `eu.atlas.microsoft.com` e `us.atlas.microsoft.com`. Verificare l'endpoint per ogni flusso API; la regione Azure dell'account e la scelta dell'endpoint geografico vanno allineate. [Ambito geografico](https://learn.microsoft.com/en-us/azure/azure-maps/geographic-scope)

Microsoft specifica che l'account seleziona una regione e che le richieste vengono elaborate e conservate esclusivamente nell'area indicata, con un meccanismo di consenso per specifiche richieste processate in regione diversa. Queste informazioni descrivono la residenza tecnica, ma non risolvono da sole la valutazione Roamly su base giuridica, consenso utente, minimizzazione, retention e informativa. La dichiarazione di residenza non va interpretata come assenza di trattamento dei dati di posizione da parte del provider. [Consent management](https://learn.microsoft.com/en-us/azure/azure-maps/consent-management); [geographic scope](https://learn.microsoft.com/en-us/azure/azure-maps/geographic-scope)

Fonti legali Microsoft di riferimento: [Azure legal information](https://azure.microsoft.com/en-us/support/legal/), [Microsoft Products and Services DPA](https://www.microsoft.com/licensing/docs/view/Microsoft-Products-and-Services-Data-Protection-Addendum-DPA) e [Microsoft Product Terms](https://www.microsoft.com/licensing/terms/). Prima dell'adozione occorre verificare i termini effettivamente applicabili all'offerta Azure e al tipo di dato e servizio; la documentazione API non sostituisce il DPA o una valutazione privacy.

## 6. Capacità, copertura Europa e limite “camper”

La tabella di copertura ufficiale Route service indica routing truck in **Italia** e in diversi paesi europei. La documentazione afferma che il truck routing considera parametri quali larghezza, altezza, peso, raggio di sterzata e tipo di carico. È copertura dichiarata del servizio/paese, non una misura della completezza dei dati per ogni strada o restrizione, né prova del corretto instradamento di qualsiasi camper. [Copertura Route service](https://learn.microsoft.com/en-us/azure/azure-maps/routing-coverage)

La medesima pagina distingue Route Directions (automobile/pedone/camion commerciale), truck routes, traffic, matrix e route range. La copertura Truck in Italia è indicata; le colonne di truck, routing standard e traffico sono distinte e non si deve assumere che ogni API/attributo abbia identica copertura. [Copertura Route service](https://learn.microsoft.com/en-us/azure/azure-maps/routing-coverage)

**Non dedurre “camper-safe” dal solo profilo car o truck:** l'esistenza di un truck route o di parametri fisici non dimostra copertura delle regole specifiche camper, qualità e aggiornamento dei limiti, né che l'assenza di un warning significhi compatibilità verificata. Richiedere una validazione di casi e restrizioni camper prima di esporre un percorso come compatibile; un itinerario generato è una stima del provider, non attestazione di sicurezza.

Per Places/Search e indirizzi in Italia, la copertura va verificata sul servizio Search/geocoding scelto e distinta dai dati di sosta camper. La ricerca di indirizzi/POI non equivale a una fonte autorizzata e completa di aree sosta o punti d'interesse camper. La documentazione generale servizi/capacità è disponibile nella [documentazione Azure Maps](https://learn.microsoft.com/en-us/azure/azure-maps/); in questa verifica non si assume una copertura di POI camper dedicata.

## 7. React, map control e consumo tiles

Il prodotto web ufficiale è Azure Maps **Web SDK Map Control**, distribuito come `azure-maps-control` e utilizzabile da TypeScript; la guida aggiornata mostra il path CDN v3 e `npm install azure-maps-control@latest`. Microsoft indica che la v1 è ritirata il 19 settembre 2026: nuovo lavoro dovrebbe essere basato sulla linea v3 documentata, con verifica della versione concreta al momento dell'implementazione. [Uso del Web Map Control](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-use-map-control)

Esiste inoltre `react-azure-maps`, wrapper TypeScript React mantenuto dal team Azure Maps nel repo `Azure/react-azure-maps`. È un wrapper sopra il Map Control, non un prodotto separato con contatore/free tier distinto; l'uso dei servizi continua a sottostare ai meter Azure Maps. [Repository React Azure Maps](https://github.com/Azure/react-azure-maps); [transazioni](https://learn.microsoft.com/en-us/azure/azure-maps/understanding-azure-maps-transactions)

Il conteggio non è per “sessione mappa”: i tile di base sono misurati in gruppi di 15 richieste per transazione, con un massimo aritmetico di 75.000 richieste tile per i 5.000 Base Map Tile transaction gratuiti se l'account e il consumo si collocano nella quota. Panning, zoom e altre viste possono generare ulteriori richieste; i tile imagery, traffic e weather usano meter separati. La documentazione Microsoft afferma esplicitamente che Azure Maps non utilizza gli session ID del modello Bing Maps. [Transazioni Azure Maps](https://learn.microsoft.com/en-us/azure/azure-maps/understanding-azure-maps-transactions)

Per ridurre carico e richieste superflue, considerare lazy-load della mappa quando l'utente apre la sezione, inizializzare con camera/stile finali e limitare aggiornamenti che causano ricarichi; queste sono pratiche di efficienza, non garanzia di quota o costo. Il Web SDK carica risorse in modo asincrono. [Web SDK best practices](https://learn.microsoft.com/en-us/azure/azure-maps/web-sdk-best-practices)

## 8. Attribuzione, cache e licenza

Se si visualizza il Render service come mappa di base o layer, Microsoft richiede l'attribuzione dei titolari dei dati sulla mappa. Quando i tile Render sono inseriti in una mappa di terzi, l'attribuzione va comunque mostrata e aggiornata per stile, zoom e bounds: i provider possono cambiare in base all'area e al livello di zoom. Progettare UI che mantenga l'attribuzione sempre leggibile, compresi i layout mobile. [Visualizzare correttamente l'attribuzione](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-show-attribution)

La consultazione documentale svolta non ha stabilito una regola generale sufficiente per autorizzare caching persistente/offline o archiviazione di tiles, risposte Search, dati POI o geometrie Route nel database Roamly. Non inferire diritti di storage dalla disponibilità di una API o dalla possibilità tecnica di memorizzare una risposta; prima di persistere/riutilizzare output occorre esaminare il contratto e i Product Terms applicabili, inclusi eventuali limiti di durata/refresh e uso in combinazione con mappe di terzi. [Azure legal information](https://azure.microsoft.com/en-us/support/legal/); [Product Terms](https://www.microsoft.com/licensing/terms/); [DPA Microsoft](https://www.microsoft.com/licensing/docs/view/Microsoft-Products-and-Services-Data-Protection-Addendum-DPA)

## 9. Perimetro minimo valutabile e raccomandazione non vincolante

### MVP documentale/tecnico da considerare solo dopo decisione

Se il decisore autorizza uno spike, un perimetro contenuto per misurare il fit potrebbe limitarsi a:

1. Mappa base on-demand, con un set essenziale di marker Roamly, senza imagery, traffic layer o refresh automatici.
2. Search esplicita su richiesta dell'utente, evitando autocomplete o polling finché non se ne conoscono costo e necessità.
3. Una route esplicita tra punti scelti, non tracking continuo; provare il profilo car/truck separatamente e non dichiarare compatibilità camper.
4. Raccolta di metriche per meter e budget/alert per visibilità, ma non come hard cap; eventuali limiti applicativi di utilizzo devono essere definiti e testati separatamente.
5. Prima di qualsiasi dato reale: chiarire consenso, endpoint EU, credenziali/token, retention, licenza/attribuzione e piano di disattivazione se si supera la soglia.

**Stima non garantita:** lo scenario contenuto del §3 resta sotto le quote gratuite pubblicate per Base Map Tiles, Search e Routing con le ipotesi date. Questo non garantisce costo zero: il consumo reale può divergere, l'account può avere altro utilizzo condiviso, il prezzo effettivo dipende dall'offerta e non si è verificato alcun account. Validare un carico con dati sintetici e calcolatore aggiornato prima di ogni decisione economica.

### Conclusione e decisioni ancora aperte

Azure Maps è un candidato possibile da approfondire per la mappa/search/routing europeo e pubblica quote mensili che potrebbero rendere economico un prototipo leggero. Questa ricerca **non sceglie il provider**, non autorizza risorse, API o codice e non chiude la decisione geo Roamly. Prima di procedere serve una decisione esplicita su:

- se si vuole soltanto un confronto documentale/prototipo sintetico oppure anche creare un account e usare API;
- volume di utenti/aperture/ricerche/route e massimo costo mensile accettabile, con conferma di offerta/prezzi;
- autenticazione per Web SDK e backend e limiti interni per controllare costi;
- requisito di residenza UE e trattamento di posizione, consenso e informativa;
- uso effettivo previsto per Search/POI e necessità di traffico/meteo;
- accettabilità del truck routing come possibile componente da testare, senza equipararlo a routing camper;
- diritti di caching, conservazione e display di risultati/route geometry e attribution.

## 10. Fonti ufficiali consultate

Documentazione pubblica Microsoft/Azure verificata il **2026-10-22**; le pagine di pricing sono dinamiche e possono cambiare.

- [Azure Maps pricing](https://azure.microsoft.com/en-us/pricing/details/azure-maps/) — meter, quote gratuite, unità generali, fasce e pricing calculator.
- [Understanding Azure Maps Transactions](https://learn.microsoft.com/en-us/azure/azure-maps/understanding-azure-maps-transactions) — regole di fatturazione per servizio e richieste non billable; pagina aggiornata 2026-08-28.
- [Azure Maps account pricing / Gen2](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-manage-pricing-tier) — stato pricing tier e ritiro Gen1; aggiornato 2026-08-28.
- [Azure Maps authentication](https://learn.microsoft.com/en-us/azure/azure-maps/azure-maps-authentication) e [Authentication best practices](https://learn.microsoft.com/en-us/azure/azure-maps/authentication-best-practices) — Shared Key, Entra, SAS, RBAC, credenziali e CORS.
- [Azure Maps geographic scope](https://learn.microsoft.com/en-us/azure/azure-maps/geographic-scope) e [Consent management](https://learn.microsoft.com/en-us/azure/azure-maps/consent-management) — endpoint EU/US, input e data residency.
- [Azure Maps Route service coverage](https://learn.microsoft.com/en-us/azure/azure-maps/routing-coverage) — truck/routing/traffic per regione; pagina aggiornata 2026-10-02.
- [Azure Maps Web Map Control](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-use-map-control) e [Web SDK best practices](https://learn.microsoft.com/en-us/azure/azure-maps/web-sdk-best-practices) — SDK web, versioni, caricamento e pratiche.
- [View Azure Maps API usage metrics](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-view-api-usage) — metriche account e retention metriche.
- [Cost Management budgets](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets) — alert, periodicità valutazione e ritardo, aggiornato 2025-06-26.
- [Show the correct map copyright attribution](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-show-attribution) — obbligo di attribution Render.
- [Azure legal information](https://azure.microsoft.com/en-us/support/legal/), [Microsoft Product Terms](https://www.microsoft.com/licensing/terms/) e [Microsoft Products and Services DPA](https://www.microsoft.com/licensing/docs/view/Microsoft-Products-and-Services-Data-Protection-Addendum-DPA) — riferimenti legali generali, da verificare sul contratto/offerta applicabile.
- [Azure/react-azure-maps](https://github.com/Azure/react-azure-maps) — wrapper React/TypeScript e integrazione con `azure-maps-control`; repository gestito dal team Azure Maps.

### Lacune esplicite

- Il prezzo numerico di overage applicabile alla subscription Roamly non è stato verificato; listino e calcolatore dipendono da offerta, valuta, regione e data.
- Non è stata verificata l'idoneità concreta di un account o la possibilità contrattuale di aggregare/separare quote tra risorse/account.
- Non si è concluso quali limiti di caching/storage/retention si applichino ai singoli dati Azure Maps in un caso Roamly; consultare i Product Terms e condizioni specifiche prima dell'uso.
- Non sono state provate dal vivo accuratezza, aggiornamento o completezza delle restrizioni truck/camper; la copertura tabellare non è un test di qualità su strada.
- Non sono stati effettuati test con coordinate reali o sintetiche verso servizi live, né creati risorse, account o credenziali.

## 11. Approfondimento per la wishlist manuale — 2026-11-03

Questo aggiornamento riguarda il [piano ristretto della wishlist](PLACES-WISHLIST-PLAN.md): mappa base e dati inseriti dall'utente, senza Search o routing. Non approva attivazione, spesa o conformità legale.

### Rendering senza chiamate Search implicite

La reference `atlas.ServiceOptions` documenta `enableAccessibilityLocationFallback`, attivo per default: se l'estrazione dai dati vettoriali fallisce, può chiamare il geocoder REST. Per il perimetro senza Search occorre impostarlo esplicitamente a `false`, mantenendo `enableAccessibility` e un elenco/dettaglio accessibili. Impostare anche `disableTelemetry: true`, usare dominio UE, e verificare nella versione SDK concreta gli endpoint realmente richiesti. Non basta omettere il campo di ricerca per dimostrare assenza di chiamate Search.

Caricare codice/stili SDK dagli asset dell'app, su apertura volontaria della mappa; niente CDN remota prima del consenso, preconnect o geolocalizzazione automatica. `domain` permette di scegliere il dominio dei servizi; va verificato con cattura di rete che stile, tile, sprite e glyph rispettino il perimetro previsto. Non dedurre la residenza di tutte le richieste dal solo endpoint di un esempio Search.

Fonti: [ServiceOptions](https://learn.microsoft.com/en-us/javascript/api/azure-maps-control/atlas.serviceoptions?view=azure-maps-typescript-latest), [lazy loading e best practice](https://learn.microsoft.com/en-us/azure/azure-maps/web-sdk-best-practices), [geographic scope](https://learn.microsoft.com/en-us/azure/azure-maps/geographic-scope).

### Accesso senza chiave permanente nel browser

La documentazione conferma Entra, managed identity, SAS e ruoli custom limitati a `Microsoft.Maps/accounts/services/render/read`. Un ruolo Search+Render ammetterebbe anche Search e non va descritto come render-only.

**Flusso confermato dall'utente per il piano il 2026-11-03:** «Token Entra tramite API esistente (Raccomandata)». Il disegno prevede managed identity dell'API e endpoint same-site autenticato che distribuisce un token Entra al controllo mappa, con ruolo render-only sul singolo account Maps. La conferma non autorizza provisioning, modifica RBAC, implementazione o traffico live; configurazione concreta e prova restano da approvare. Non aggiungere login Entra agli utenti Roamly, una nuova app di autenticazione utenti o una chiave permanente nel bundle. Applicare consenso corrente, flag, restrizioni di sessione e controllo emissione prima di restituire il token; risposta non memorizzabile, token soltanto in memoria e mai nei log.

Il browser riceve comunque una credenziale bearer riutilizzabile fino alla scadenza. La durata effettiva del token Entra non è liberamente abbreviabile impostando un TTL locale: chiudere la mappa, revocare il consenso o spegnere il flag impedisce nuova emissione ma non garantisce invalidazione immediata di token già rilasciati. CORS non è un presidio contro client non-browser. Il rischio residuo va esplicitato, non trasformato in una promessa di costo massimo.

SAS è un'alternativa con durata configurabile, regioni consentite e rate limit approssimativo per token/region. La reference List Sas richiede una user-assigned managed identity associata all'account; la durata massima documentata è 24 ore. Token multipli possono ampliare il consumo e il rate non è un plafond mensile. Non copiare il tutorial che usa un anno come durata: contrasta con la reference corrente. La scelta del flusso definitivo richiede conferma prima di modificare identità o RBAC.

Fonti: [authentication e ruolo render-only](https://learn.microsoft.com/en-us/azure/azure-maps/azure-maps-authentication), [best practice autenticazione](https://learn.microsoft.com/en-us/azure/azure-maps/authentication-best-practices), [endpoint token per Web SDK](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-secure-webapp-users), [List Sas](https://learn.microsoft.com/en-us/rest/api/maps-management/accounts/list-sas?view=rest-maps-management-2023-06-01), [tutorial SAS](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-secure-sas-app).

### Licenza e consenso: evidenza e limiti

I [Product Terms MCA, sezione Azure Maps](https://www.microsoft.com/licensing/terms/productoffering/MicrosoftAzure/MCA) distinguono dati del cliente sovrapposti, restrizioni sui database e risultati API; prevedono limiti di conservazione dei risultati e un'eccezione per i geocodici con account Azure attivo. Non risolvono automaticamente la classificazione contrattuale di ogni dato ottenuto selezionando un punto sulla mappa. La wishlist manuale non deve copiare nomi, indirizzi o metadati dai tile né importarli come catalogo POI; serve comunque verificare il contratto effettivamente applicabile. Questa analisi non certifica la conformità legale.

L'attribuzione non può essere coperta dai pannelli o eliminata. Per Render la documentazione richiede attribuzione coerente con area/zoom: verificarla nel controllo scelto, senza hardcodare un unico copyright per ogni vista. [Guida attribuzione](https://learn.microsoft.com/en-us/azure/azure-maps/how-to-show-attribution).

Nel repository, il trigger 1 di ADR-0004 richiede consensi con finalità versionate e prova del consenso al primo trasferimento pertinente. Il controllo del viewport può rivelare zone scelte dall'utente anche senza inviare i marker. La sola informativa vista (`PrivacyPolicyVersionSeen`) o il pulsante «Apri mappa» non sostituiscono il consenso richiesto dal progetto. Nella ricognizione del runtime non è stato trovato un meccanismo di consensi già implementato. L'utente ha poi scelto per il piano un **registro unico grant/revoke in Identity**: [disegno e limiti](PLACES-WISHLIST-PLAN.md#consenso-trasversale-modello-scelto-e-dettagli-aperti). Schema dettagliato, testo, retention, base giuridica e implementazione restano aperti; revoca, export ed erasure fanno parte del disegno richiesto, non di funzionalità già presenti.

### Ricognizione cloud: accesso ripristinato e inventario read-only

La subscription e il tenant di destinazione sono stati letti dalle variabili **non segrete** del GitHub Environment `azure-release`. Il primo tentativo era fallito con `Subscription ... not found`: quel risultato non dimostrava l'assenza di risorse. Dopo autorizzazione esplicita dell'utente e accesso interattivo completato, `account show` sul target ha confermato subscription e tenant corrispondenti e stato `Enabled`. Tutte le letture successive hanno indicato esplicitamente la subscription, senza cambiare quella predefinita.

Risultati osservati il 2026-11-03:

- L'inventario ARM di `Microsoft.Maps/accounts` ha restituito **zero account** nell'intera subscription target, non soltanto nel resource group. Non ci sono account Maps restituiti dall'inventario da riutilizzare; non è una verifica di altre subscription o di risorse non visibili all'identità corrente.
- `id-roamly-runtime-prod` esiste in `rg-roamly-dev`, regione Italy North, ed è già associata a `ca-roamly-prod` come user-assigned managed identity. Il suo riuso per Maps resta una proposta soggetta ad autorizzazione RBAC separata.
- La lettura ARM delle assegnazioni per il principal runtime nella subscription ha restituito soltanto scope Storage e singoli segreti Key Vault; nessuno scope Maps. La lista dei ruoli custom visibili non contiene permessi che nominino esplicitamente `Microsoft.Maps`. Non è un audit completo dei permessi effettivi ereditati o attribuiti tramite gruppi.
- Non essendoci un account Maps restituito dall'inventario, non sono stati verificati SKU, CORS, configurazione di autenticazione, metriche o quota residua di un account. Il prezzo applicabile all'offerta e la disponibilità effettiva delle quote gratuite restano da verificare prima dell'attivazione.

Non sono stati letti valori Key Vault, richieste chiavi, emessi token Maps, assegnati ruoli, create risorse o chiamate API tile/Search. L'autorizzazione ricevuta copriva login e ricognizione read-only, non provisioning o spesa. Il blocco di accesso è risolto; rimangono i gate di licenza, consenso, autenticazione, consumi e spike/ADR.

## 12. Verifica aggiornata dei gate — 2026-11-06

L'utente ha chiesto di proseguire con i prerequisiti della mappa reale dopo l'implementazione del registro locale Identity. Le verifiche seguenti sono documentali e ARM/Monitor read-only: nessuna modifica cloud, acquisizione di token Maps o chiamata al data plane.

### Stato cloud osservato

- Subscription e tenant sono stati nuovamente confrontati con le variabili non segrete di `azure-release`; il target corrisponde ed e' `Enabled`. Nessun cambio della subscription predefinita.
- L'inventario attuale restituisce **un account esistente**, `maps-roamly-dev`, SKU `G2`, location ARM `global`. Supera il solo dato storico "zero account" del 3 novembre; questa sessione non ha creato la risorsa. La location ARM non prova il dominio geografico usato dal futuro SDK.
- `disableLocalAuth: true`: le credenziali locali sono disabilitate; non sono state lette chiavi. `properties.cors` e' un oggetto vuoto: non risulta una allowlist esplicita nella risposta osservata, senza dedurne l'effetto sulle singole richieste browser.
- La lista ARM delle assegnazioni allo scope Maps restituisce un'assegnazione per `id-roamly-runtime-prod`: **Azure Maps Data Reader**, con `DataActions: ["Microsoft.Maps/accounts/*/read"]`. Non e' render-only e non soddisfa il minimo privilegio previsto dal piano. Prima di distribuire bearer al browser, restringere l'assegnazione a `Microsoft.Maps/accounts/services/render/read`, previa autorizzazione esplicita. La lettura non e' un audit completo di gruppi o permessi effettivi ereditati.
- Azure Monitor espone la metrica `Usage` e le dimensioni `ApiCategory`, `ApiName`, `ResultType`, `ResponseCode`. La lettura `Count`, aggregazione oraria delle ultime 24 ore, non restituisce campioni non nulli: **dato assente, non consumo verificato pari a zero**, ne' prova di gratuita' o di monitoraggio pronto.

### Evidenze documentali riconfermate

- [Autenticazione Azure Maps](https://learn.microsoft.com/en-us/azure/azure-maps/azure-maps-authentication): managed identity supportata; ruolo custom render-only e scope sul singolo account. Il bearer Entra resta riutilizzabile fino alla scadenza; il registro Roamly non lo revoca presso Azure.
- [Geographic scope](https://learn.microsoft.com/en-us/azure/azure-maps/geographic-scope): usare `eu.atlas.microsoft.com` per il perimetro geografico europeo delle richieste. Stili, tile e asset effettivi vanno comunque osservati nella versione SDK scelta.
- [Pricing pubblico](https://azure.microsoft.com/en-us/pricing/details/azure-maps/) e [transazioni](https://learn.microsoft.com/en-us/azure/azure-maps/understanding-azure-maps-transactions): 5.000 transazioni mensili gratuite Base Map Tiles, con 15 richieste tile per transazione. Non equivalgono a 5.000 aperture della mappa. La pagina pubblica letta mostra prezzi di overage non risolti (`$-`): nessun prezzo numerico applicabile all'offerta e' verificato.
- [Budget Cost Management](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets): il budget non arresta il consumo; i dati di costo hanno ritardi e la valutazione e' periodica. Non sostituisce un hard cap.
- [Product Terms MCA, Azure Maps](https://www.microsoft.com/licensing/terms/productoffering/MicrosoftAzure/MCA): overlay proprietari e risultati del provider hanno condizioni diverse; restano restrizioni su database derivati, conservazione dei risultati e attribuzione. L'applicabilita' di MCA alla subscription non e' stata verificata. La lettura non costituisce approvazione legale; nessuna licenza del provider e' stata accettata per conto dell'utente.
- La pagina Microsoft [consent management](https://learn.microsoft.com/en-us/azure/azure-maps/consent-management) riguarda anche la configurazione geografica del servizio: non e' il consenso personale versionato di Roamly e non lo sostituisce.

### Gate ancora aperti

Registro locale implementato, finalita' Maps ancora DRAFT/inattiva. Restano testo e flusso privacy approvati, condizioni applicabili, restringimento RBAC autorizzato, contratto del rilascio token e controlli anti-abuso, riscontro di metriche e prezzi effettivi, soglie economiche concordate e autorizzazione separata dello spike live. Nessuna evidenza qui autorizza implicitamente attivazione, spesa, modifica RBAC o applicazione di migration.

### Restringimento RBAC autorizzato ed eseguito — 2026-11-06

Dopo la verifica read-only, l'utente ha scelto **«Approva il restringimento RBAC render-only sul solo account Maps»**. Vulcan ha verificato che l'assegnazione ampia fosse diretta sul singolo account e ha eseguito soltanto:

1. Creazione del ruolo custom `Roamly Maps Render Reader`, con `Actions`, `NotActions` e `NotDataActions` vuoti e unica `DataAction` `Microsoft.Maps/accounts/services/render/read`. `AssignableScopes` limitato alla singola risorsa `maps-roamly-dev`.
2. Assegnazione alla managed identity `id-roamly-runtime-prod` sul solo account Maps, seguita da rilettura della definizione e del grant.
3. Rimozione dell'assegnazione diretta originale `Azure Maps Data Reader`, solo dopo la verifica della nuova.

La rilettura ARM finale conferma nuova assegnazione presente, vecchia assente e altre assegnazioni sull'account invariate. Non sono state modificate definizioni built-in o assegnazioni su altre risorse. Nessuna assegnazione diretta per il principal e' stata rilevata su subscription/resource group; non e' un audit completo dei gruppi e dell'ereditarieta'.

Questa e' una verifica **control-plane**, non una prova funzionale: propagazione RBAC e cache possono ritardare l'effetto. La modifica non garantisce revoca immediata di credenziali gia' emesse. Non sono stati emessi token Maps, chiamati tile/Search, attivati servizi, modificate CORS/identita' dell'app o applicate migration. Il solo restringimento RBAC del gate precedente e' completato; consenso, token, consumi, SDK, spike e autorizzazione all'attivazione restano aperti.

### Soglia scelta e approfondimento economico — 2026-11-06

L'utente ha scelto **1 EUR/mese aggiuntivo come soglia operativa di allerta e sospensione dei nuovi accessi**, non come tetto garantito alla fattura. Ha poi approvato **il controllo all'apertura con diniego se i dati non sono affidabili**, autorizzando il blocco locale SQL/API con osservazioni simulate e migration generata ma non applicata. Non sono approvati adapter Azure attivi, nuove autorizzazioni cloud, token Maps o traffico live.

**Metadati dell'offerta letti sul solo target verificato:** `Microsoft.Billing/billingProperty/default`, API `2024-04-01`, restituisce `billingAccountAgreementType: MicrosoftCustomerAgreement` e `billingCurrency: EUR`. Questo aggiorna il precedente limite sulla famiglia contrattuale non verificata: MCA e' coerente con i metadati osservati. Non sono stati letti o pubblicati identificativi dei profili di fatturazione, prezzi negoziati o dettagli di altre subscription. Il tipo di accordo non certifica la conformita' dell'applicazione o il prezzo effettivamente fatturato.

**Listino pubblico numerico ottenuto:** la [Retail Prices API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices), interrogata in EUR con filtri `serviceName = Azure Maps`, `meterName = Maps Base Map Tiles`, `armRegionName = Global` e `priceType = Consumption`, restituisce due righe, senza ulteriori pagine:

| Unita' API | Soglia minima della fascia | Retail price EUR per unita' | Data di efficacia restituita |
|---|---|---|---|
| `1K` | `0` | `0` | `2023-06-01` |
| `1K` | `5` | `4.039679` | `2023-06-01` |

Il meter pubblico e' `d6513bb0-9af1-5abb-8e29-14c150138878`, prodotto `Azure Maps`, SKU `Maps`. Le soglie sono espresse nell'unita' `1K`, non in aperture della mappa. La documentazione Microsoft precisa che i prezzi non USD sono **riferimenti per stime**: questi dati superano il solo limite dei placeholder della pagina web, non la verifica del prezzo applicabile alla subscription. Non vengono usati automaticamente per impostare il gate o convertire `Usage` in costo.

**Contratto della futura lettura economica:** la [Cost Management Query API](https://learn.microsoft.com/en-us/rest/api/cost-management/query/usage?view=rest-cost-management-2025-03-01) documenta anche lo scope resource group e un filtro per risorsa; il filtro non restringe i privilegi dell'identita'. Lo scope piu' ristretto effettivamente utilizzabile va provato senza estendere implicitamente l'RBAC runtime. L'operazione puo' rispondere `204 No Content`: non interpretarlo come costo pari a zero.

La guida [Understand Cost Management data](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/understand-cost-mgt-data) distingue costi stimati prima della fattura, esclusione dei crediti e delle imposte, risorse gratuite non rappresentate nei dati economici e periodi di fatturazione dipendenti dall'offerta. Il gate non deve confondere osservazione acquisita ora con copertura aggiornata, sommare due volte stima e costo riportato, o considerare l'assenza di righe una prova di gratuita'. Decorrenza del periodo, validita' e riconciliazione delle evidenze reali restano da approvare prima dell'attivazione.

### Prova read-only Cost Management sul target — 2026-11-06

Eseguita con l'identita' operatore gia' autenticata, non con la managed identity runtime, una Query `ActualCost`/`MonthToDate`, granularita' giornaliera, aggregazione `Cost/Sum`, allo scope del resource group dell'account Maps e filtro `ResourceId` esatto su `maps-roamly-dev`. Il POST e' un'operazione di lettura: non crea budget o altre risorse.

Il primo tentativo ha restituito `429 Too Many Requests`; un solo successivo tentativo, dopo altre verifiche, ha restituito le colonne `Cost`, `UsageDate`, `Currency`, **nessuna riga** e nessuna pagina aggiuntiva. Non sono stati dedotti costo zero, copertura temporale o disponibilita' di evidenze sufficienti per aprire il gate. La prova verifica il funzionamento della query filtrata allo scope resource group con l'operatore, non l'accesso runtime o l'aggiornamento dei dati.

Le operazioni RBAC di sola lettura sono state confermate nell'inventario dei provider: `Microsoft.CostManagement/query/read` e `Microsoft.Insights/Metrics/Read` sono entrambe Actions control-plane, non DataActions. La managed identity runtime non ha ricevuto questi permessi durante la prova. Per Cost Management il minimo scope documentato e qui provato e' il resource group: il filtro Maps dell'applicazione **non impedirebbe all'identita' di interrogare i costi delle altre risorse del gruppo** se le venisse concesso quel permesso. Tale estensione richiede approvazione esplicita, distinta dal ruolo render-only.

### Successiva estensione RBAC di sola lettura approvata — 2026-11-06

Dopo aver ricevuto la spiegazione degli scope e dell'accesso ai costi delle altre risorse del gruppo, l'utente ha selezionato **«Approva i due permessi di sola lettura agli scope indicati»**. Vulcan ha creato e assegnato alla managed identity `id-roamly-runtime-prod` due ruoli custom separati:

| Ruolo | Unica Action | Unico AssignableScope e scope del grant |
|---|---|---|
| `Roamly Maps Metrics Reader` | `Microsoft.Insights/Metrics/Read` | Solo account `maps-roamly-dev` |
| `Roamly Maps Cost Query Reader` | `Microsoft.CostManagement/query/read` | Solo resource group `rg-roamly-dev` |

Per entrambi `NotActions`, `DataActions` e `NotDataActions` sono vuoti. Tenant/subscription sono stati riconfermati contro le variabili dell'ambiente GitHub `azure-release`, senza modificare il contesto Azure predefinito. La rilettura ARM ha verificato definizioni, principal e grant: preservate tutte le 30 assegnazioni preesistenti e le definizioni inventariate, aggiunte solo le due definizioni e le due assegnazioni approvate. Ruolo e grant render-only invariati.

La verifica riguarda esclusivamente lo stato **control-plane**: non dimostra propagazione o accesso effettivo della managed identity. Nessun token Maps, chiamata data-plane, deploy, modifica CORS o applicazione di migration. Mappa e gate economico restano inattivi; i nuovi permessi non risolvono l'assenza di campioni o la verifica di completezza e aggiornamento delle evidenze economiche.
