# Roamly

> **The operating system for your camper life.**

Roamly è un **personal travel operating system** per camperisti: conosce il tuo camper, conosce il tuo modo di viaggiare e usa queste informazioni per aiutarti a prendere decisioni migliori.

**Stato: pre-implementazione.** Nessun codice scritto. Il progetto è in fase di chiusura delle decisioni architetturali.

---

## Stack previsto

| Area | Scelta |
|---|---|
| Backend | ASP.NET Core 10 · C# · EF Core |
| Architettura | Modular monolith · Vertical Slice · CQRS logico |
| Database | **Microsoft SQL Server** + Spatial |
| Frontend | React · Vite · TypeScript |
| API | REST + OpenAPI |
| CI/CD | GitHub Actions · Docker |
| Mobile | Responsive web first |

---

## Documentazione

Tutta la documentazione vive in [`docs/`](docs/README.md).

Per iniziare: [`docs/OPEN-DECISIONS.md`](docs/OPEN-DECISIONS.md) — le decisioni da chiudere prima di scrivere codice.

## Agenti e skill

Vedi [`AGENTS.md`](AGENTS.md).

## Aggiornamento locale «Da visitare» — 2026-11-10

Su richiesta esplicita dell'utente, il nuovo percorso personale supera i precedenti
passaggi «Consenti e apri mappa» e link opaco senza estrazione descritti sotto:

- **Lista resta predefinita**, senza richieste Maps. Scegliendo **Mappa** il canvas si
  apre direttamente, anche vuoto (Italia), con una breve informativa IP/viewport Azure EU.
  Nessuna lettura/scrittura del consenso all'apertura; storico Identity, export/erasure e
  percorso rigoroso `MapsAdmissionService` restano invariati.
- Il link Google Maps originale resta conservato. Estrazione **solo locale**, da una
  allowlist esatta di host Google: `!3d…!4d…`, `api=1&query=lat,lon` o `q=lat,lon`.
  Mai coordinate viewport `@`, fetch Google, nomi importati o risoluzione short-link.
  Link brevi, ambigui o non supportati richiedono coordinate manuali.
- Create/edit precompilano campi vuoti o ancora automatici; cambiare/rimuovere il link
  aggiorna/pulisce solo quelli automatici. Un campo corretto manualmente resta protetto
  per la sessione di editing; le posizioni già salvate sono considerate manuali.
  Nell'API una posizione esplicita prevale; se nulla, viene derivata dal link.
- GET/list espongono la posizione effettiva anche per i vecchi luoghi con posizione nulla
  (incluso Bunker Soratte), **senza scrivere sul database né cambiare ETag**.
  Pin, conteggi, centramento e dettagli usano tale posizione.
- Token: sessione, CSRF, rate limit, `no-store`, managed identity, controllo account
  prima/dopo Entra e sospensione economica persistita restano obbligatori, non il consenso Maps.
  Nessuna migration o modifica di ruoli/cloud; 1 EUR resta un riferimento, non un hard cap.

Queste modifiche sono **solo nel checkout locale, non distribuite**; nessuna prova con
traffico Azure reale. I resoconti del rilascio precedente restano storici.

## Configurazione tecnica — mappa «Da visitare» (integrazione precedente)

I parametri seguenti restano validi; i passaggi di consenso sono superati
dall'aggiornamento locale del 2026-11-10 sopra, non ancora distribuito.

La lista è il default e funziona senza Azure Maps. Il Web SDK è incluso nel bundle locale,
ma SDK e CSS vengono scaricati soltanto aprendo la vista Mappa e confermando «Consenti e apri mappa».
I luoghi senza coordinate restano consultabili e selezionabili; non vengono geocodificati.

Su un host con managed identity già disponibile, configurare le impostazioni API:

```text
Maps__Enabled=true
Maps__AccountClientId=<client ID dell'ACCOUNT Azure Maps>
Maps__ManagedIdentityClientId=<client ID della managed identity user-assigned, se usata>
```

Il client ID dell'account Maps **non** è il client ID della managed identity. Lasciare
`ManagedIdentityClientId` vuoto per una system-assigned identity. Non sono richiesti
tenant/subscription ID o subscription key nel frontend. `Azure.Identity` è già una dipendenza
transitiva dell'API. In produzione viene usata esclusivamente `ManagedIdentityCredential`,
senza fallback a credenziali CLI/developer o chiavi. In locale, senza MI, la mappa comunica
l'indisponibilità: non eseguire accessi Azure reali per i test.

La MI usa il ruolo **Render Reader** già assegnato sul solo account scelto; l'accesso di
rendering effettivo va verificato nell'host, senza assumere la propagazione
RBAC immediata. Gli eventuali ruoli Metrics/Cost non sostituiscono il ruolo dati per il
rendering. Queste istruzioni non creano risorse né assegnano permessi.

`GET /api/v1/maps/config` restituisce solo disponibilità, account client ID e dominio EU
`eu.atlas.microsoft.com`. `POST /api/v1/maps/token` richiede sessione, CSRF e consenso corrente;
usa il rate limit per account già esistente e restituisce un bearer Entra per
`https://atlas.microsoft.com/.default`, con scadenza reale e `Cache-Control: no-store`.
Account e revisione del consenso sono verificati prima dell'acquisizione e nuovamente
sotto il lock account dopo la chiamata Entra; nessuna chiamata remota avviene sotto lock SQL.
Il bearer rimane solo in memoria del controllo e non va copiato in log/storage.

Il registro Identity è l'unico registro dei consensi: con host configurato la definizione
è `azure-maps-render`, versione `v1`, riferimento `roamly:azure-maps-render:v1`.
Il testo tecnico mostrato spiega IP/viewport ad Azure Microsoft in EU, area rivelata dal
centramento e titoli/note come overlay DOM locali; non certifica conformità legale.
Senza configurazione valida resta `DRAFT-1`, inattiva. La revoca chiude la mappa e interrompe
i rinnovi locali, ma non invalida immediatamente bearer già emessi o altre schede aperte.
L'attribuzione del provider resta visibile; telemetria SDK e fallback geocoding di accessibilità
sono disabilitati. Non sono usati Search, routing, geocoding o espansione dei link Google.
Il build rimuove soltanto gli `@font-face` remoti dal CSS vendor: layout e attribuzione
restano quelli del SDK, senza richieste aggiuntive per font al dominio globale.

**Scelta per uso personale:** l'emissione non chiama `MapsAdmissionService` /
`MapsEconomicGate`, che rimangono invariati e inattivi per gli usi più rigorosi.
Rispetta comunque qualsiasi sospensione economica già persistita in `MapsEconomicStates`
(anche di un periodo precedente). Non vengono inventati costi pari a zero o coperture complete.
La soglia **1 EUR** resta un riferimento operativo: non è un hard cap e non viene promesso
monitoraggio/alert automatico. Nessun nuovo osservatore, scheduler o permesso è introdotto.

Verifiche senza chiamate Azure: `npm run test:saved-places-map` (helper Node e Playwright con
SDK/API simulati), `npm run test:saved-places-flow` (lista e creazione) da
`frontend/roamly-web`; `dotnet test --project tests/Roamly.IntegrationTests -- --filter-class
'*MapsRenderingApiTests'` per l'API con credenziale fake. Le migration preesistenti restano
non applicate al database applicativo; la feature non aggiunge nuove migration.
