# Wishlist luoghi: costi e condizioni dei provider geografici

- **Verifica documentale:** 2026-11-03.
- **Stato:** ricerca completata; Azure Maps scelto dall'utente come candidato da approfondire il 2026-11-03, dopo presentazione del confronto. Nessun account creato o costo autorizzato.
- **Perimetro:** mappa, ricerca luoghi e geocodifica per una wishlist personale autenticata; routing escluso.

La priorità espressa dall'utente è un servizio gratuito o il meno costoso possibile; Azure Maps è un candidato accettabile. La ricerca usa fonti ufficiali pubbliche, senza query geografiche live o trasmissione di posizioni personali. Prezzi e condizioni possono cambiare; gli importi sono USD prima di imposte, cambio e condizioni contrattuali dell'account. Non sono preventivi.

## Confronto

| Opzione | Quota gratuita pubblicata | Oltre quota | Ricerca e salvataggio |
|---|---|---|---|
| **MapTiler Free** | 5.000 map sessions/mese, 1.000 search sessions/mese, 100.000 API requests/mese; le unità dipendono dall'integrazione e non sono intercambiabili. | Nessuna carta per Free; servizio sospeso fino al mese successivo salvo upgrade. Free per uso personale/non commerciale; Flex da $30/mese, con overage. | Autocomplete disponibile; i termini permettono esportazione dei risultati Search e database da geocoding con attribuzione. Questo non autorizza cache server-side dei tile. [1–3] |
| **Azure Maps Gen2** | 5.000 transazioni Base Map Tiles/mese e 5.000 Search/mese. 15 tile = 1 transazione; ricerca ordinaria = 1 transazione; Geocode Autocomplete = 1 transazione ogni 10 richieste. | Consumo fatturabile oltre franchigia. Retail API West Europe: $4,50/1.000 transazioni oltre la quota gratuita per entrambi i meter considerati. Budget/avvisi non fermano gli addebiti. | Geocodici conservabili con account Azure attivo; non estendere l'eccezione indiscriminatamente a nome, indirizzo e metadati POI. Verificare termini del contratto effettivo. [4–8] |
| **Geoapify Free** | 3.000 crediti/giorno, non un pool mensile. Tile: 0,25 crediti; geocoding/reverse/autocomplete: 1 credito; Places: 1 credito fino a 20 risultati, poi aumenta. | Nessuna carta per iniziare; limiti soft e termini con possibili overage, quindi nessuna garanzia di costo massimo zero. Primo piano mensile pubblicizzato: $59. | Autocomplete e archiviazione/riuso dei risultati dichiarati consentiti; Free anche commerciale secondo FAQ correnti, con attribuzione. [9–12] |
| **OSM pubblico + Leaflet/MapLibre** | Nessuna quota mensile garantita. Nominatim pubblico: massimo assoluto 1 richiesta/secondo aggregata sull'app. | Nessun canone per gli endpoint pubblici, ma accesso revocabile, servizio best effort e nessuno SLA. | Autocomplete Nominatim pubblico vietato; ricerca esplicita, identificazione app, attribuzione e rispetto delle policy. Non usarlo come infrastruttura gratuita illimitata. [13–15] |

MapLibre e Leaflet sono renderer, non servizi di tile o ricerca gratuiti illimitati. Pan e zoom scaricano nuovi tile; digitare può generare più ricerche. MapTiler SDK/plugin e integrazioni generiche possono usare unità diverse: una schermata non equivale automaticamente a una sessione fatturabile o a una sola richiesta. [2,5,16]

## Raccomandazione

**MapTiler Free è il primo candidato se prevale evitare addebiti automatici:** nessuna carta, sospensione oltre quota, uso personale ammesso e condizioni di esportazione dei risultati documentate. Non sceglierlo se l'interruzione della mappa al raggiungimento del limite è inaccettabile o se l'uso diventa commerciale senza rivalutare il piano. Conservare attribuzione e logo richiesti. [1–3]

**Azure Maps è una valida alternativa se prevale restare su Azure:** franchigie significative per uso leggero e prezzo retail verificabile, ma modello a consumo e conservazione dei record POI da chiarire. Un budget Azure non è un hard cap. Nessuna garanzia di spesa zero o di copertura completa per piccoli musei/locali. [4–8]

Geoapify offre maggiore flessibilità dichiarata sulla persistenza, ma i limiti soft non sono una promessa di costo massimo nullo. OSM pubblico richiede adozione informata delle policy, senza autocomplete né affidabilità garantita; non è il default raccomandato. [9–15]

## Dettaglio del prezzo Azure verificato

La pagina prezzi mostrava quote ma importi dinamici non valorizzati. La ricerca ha interrogato la Retail Prices API pubblica Microsoft, senza credenziali, filtrando:

```text
serviceName eq 'Azure Maps'
and armRegionName eq 'westeurope'
and (meterName eq 'Maps Base Map Tiles' or meterName eq 'Location Insights Search')
```

Per entrambi i meter: `currencyCode=USD`, `unitOfMeasure=1K`; fascia `tierMinimumUnits=0`, `retailPrice=0`; fascia `tierMinimumUnits=5`, `retailPrice=4.5`. Il record aveva `effectiveStartDate=2023-06-01`, distinto dalla data di verifica. Nessun prezzo EUR, sconto o contratto specifico verificato. [6]

Esempio puramente aritmetico: 90.000 tile = 6.000 transazioni; dopo 5.000 gratuite restano 1.000 transazioni, ossia $4,50 di rendering, esclusi Search e altri consumi. Il consumo reale va misurato: non è una previsione.

## Licenze e privacy da non saltare

- **MapTiler:** esportazione Search consentita secondo i termini Cloud, attribuzione associata al database; non assumere permessi di proxy/cache server-side dei tile. Free richiede anche il logo. Policy privacy: IP trattati per sicurezza fino a due mesi; nessuna retention universale delle query geocoding confermata. [3,17]
- **Azure:** i termini distinguono geocodici da altri risultati, per cui valgono limiti di caching, normalmente header o sei mesi, il minore. L'eccezione non autorizza automaticamente un catalogo permanente di POI. L'endpoint UE mantiene richieste/input nella geografia prescelta; questo non significa assenza di trattamento né chiude il gate privacy Roamly. [7,18]
- **Geoapify:** termini e attribuzioni delle fonti restano applicabili. FAQ correnti e termini del 2024 non sono perfettamente allineati sulla produzione commerciale/overage: verificare prima di promettere costo zero. La privacy policy dichiara conservazione di body/header/IP/timestamp, normalmente fino a 24 ore per richieste riuscite. [9–12,19]
- **OSM pubblico:** attribuzione, identificazione dell'app e cache HTTP conformi alle policy; niente bulk/prefetch/offline. Nominatim vieta autocomplete e invio di dati personali/confidenziali. La licenza dei dati è ODbL: valutare obblighi sulle future distribuzioni di database derivati, senza dedurre che note private vadano pubblicate. [13–15]

Una wishlist autenticata non rende invisibili le zone visualizzate e le query al provider. Non inviare note, identificativi utente o l'intera raccolta. Non attivare geolocalizzazione automatica. Creazione manuale e lettura dei luoghi devono restare utilizzabili anche con provider sospeso; l'indisponibilità della mappa va dichiarata esplicitamente.

## Link Google Maps

Conservare un URL come collegamento esterno non concede diritti di scraping/importazione dei dati Google su un'altra mappa. La prima versione prudente conserva il link opaco, senza risolverlo per acquisire nome, indirizzo, coordinate, recensioni o immagini; posizione inserita manualmente o ricercata nel provider scelto. L'eventuale estrazione automatica richiede valutazione separata delle condizioni, senza confondere link utente e licenza Google Places. Condizioni SEE/non-SEE differiscono. [20–22]

## Limiti e prossima decisione

Non sono stati verificati con chiamate live qualità dei POI, copertura italiana, consumi effettivi del renderer o integrazione nell'app. L'utente ha scelto **Azure Maps**, consapevole delle quote gratuite e dei possibili costi a consumo, anziché la raccomandazione MapTiler basata sul blocco oltre quota. La scelta del candidato non autorizza provisioning o spese e non chiude il gate generale #3 sul routing. Il ticket provider resta aperto fino alle verifiche richieste dal perimetro e all'ADR pertinente; nessun budget massimo concordato.

## Fonti ufficiali

1. [MapTiler: pricing e FAQ](https://www.maptiler.com/cloud/pricing/)
2. [MapTiler: sessions vs requests](https://docs.maptiler.com/guides/account/sessions-vs-requests/)
3. [MapTiler: condizioni Cloud](https://www.maptiler.com/terms/cloud/) e [condizioni generali](https://www.maptiler.com/terms/)
4. [Azure Maps: pricing](https://azure.microsoft.com/en-us/pricing/details/azure-maps/)
5. [Azure Maps: transazioni](https://learn.microsoft.com/en-us/azure/azure-maps/understanding-azure-maps-transactions)
6. [Azure Retail Prices API: interrogazione dei meter](https://prices.azure.com/api/retail/prices?$filter=serviceName%20eq%20%27Azure%20Maps%27%20and%20armRegionName%20eq%20%27westeurope%27%20and%20%28meterName%20eq%20%27Maps%20Base%20Map%20Tiles%27%20or%20meterName%20eq%20%27Location%20Insights%20Search%27%29)
7. [Microsoft Product Terms: Azure Maps, contratto MCA](https://www.microsoft.com/licensing/terms/productoffering/MicrosoftAzure/MCA)
8. [Azure: budget non bloccanti](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets)
9. [Geoapify: pricing](https://www.geoapify.com/pricing/)
10. [Geoapify: crediti per servizio](https://www.geoapify.com/pricing-details/)
11. [Geoapify: termini](https://www.geoapify.com/terms-and-conditions/)
12. [Geoapify: dichiarazioni su riuso e archiviazione](https://www.geoapify.com/)
13. [OSMF: tile policy](https://operations.osmfoundation.org/policies/tiles/)
14. [OSMF: Nominatim policy](https://operations.osmfoundation.org/policies/nominatim/)
15. [OpenStreetMap: copyright e ODbL](https://www.openstreetmap.org/copyright)
16. [MapLibre GL JS](https://maplibre.org/maplibre-gl-js/docs/) e [Leaflet](https://leafletjs.com/)
17. [MapTiler: privacy](https://www.maptiler.com/privacy-policy/)
18. [Azure Maps: geographic scope](https://learn.microsoft.com/en-us/azure/azure-maps/geographic-scope)
19. [Geoapify: privacy](https://www.geoapify.com/privacy-policy/)
20. [Google Maps: termini utente aggiuntivi](https://www.google.com/help/terms_maps/)
21. [Google Maps Platform: condizioni specifiche non-SEE](https://cloud.google.com/maps-platform/terms/maps-service-terms)
22. [Google Maps Platform: condizioni specifiche SEE](https://cloud.google.com/terms/maps-platform/eea/maps-service-terms)
