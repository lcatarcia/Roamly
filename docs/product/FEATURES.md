# Roamly — Funzionalità di prodotto

Stato: attivo · Ultimo aggiornamento: 2026-10-21
Origine: `docs/archive/Roamly_Planning_2026-09-24.md` §6bis, §7-§13

> Questo documento descrive **cosa** fa il prodotto. Lo **scope e l'ordine** sono in `docs/product/ROADMAP.md`.

---

## 1. Dashboard

La home risponde rapidamente a:

> "Come sta il mio camper e cosa devo fare?"

La dashboard MVP è riferita a un camper e si apre da `GET /api/v1/campers/{camperId}/dashboard`. La selezione usa la collection owner-scoped `GET /api/v1/campers` (paginata secondo R52): con un camper si entra direttamente, con più di uno si sceglie, senza camper si apre la creazione.

Mostra il profilo essenziale del mezzo, l'ultima lettura del contachilometri qualificata dalla sua data e lo stato delle manutenzioni ricorrenti attive. Tempo e chilometri sono **assi indipendenti**: `DueSoon` significa entro 30 giorni o 1.000 km sul rispettivo asse; `Overdue` significa soglia raggiunta o superata. Non si dichiara un asse vincolante e non si calcola uno stato complessivo.

Se manca una lettura o è più vecchia di 90 giorni, l'asse km è «non verificabile». Una lettura vecchia resta visibile solo come valore datato, non attuale. Una manutenzione senza storico mostra «mai eseguito» e non genera un promemoria. La dashboard gestisce anche le liste vuote; non crea dati dimostrativi.

Spese, viaggi, km annuali, checklist e prossime partenze sono fuori dalla dashboard di questa slice e non vengono simulati.

---

## 2. My Camper

### Anagrafica

Dati tecnici, dimensioni, pesi, consumi, capacità.

### Equipaggiamento

Gestione degli accessori (pannelli solari, batteria, inverter, bombole, portabici, tendalino, livellatori).

### Manutenzione

L'utente crea una regola ricorrente per camper, con intervallo in km, mesi o entrambi, e registra interventi con data, chilometraggio facoltativo, costo multi-valuta, officina e note. I log sono fatti storici immutabili: l'interfaccia mostra lo storico paginato e la dashboard riflette la proiezione aggiornata dall'intervento più recente secondo la data di esecuzione e, a parità, l'ordine di registrazione.

Una regola appena creata senza interventi mostra **«Mai eseguito»**: non ha scadenze e non genera reminder. Dopo il primo intervento, la dashboard espone separatamente lo stato di scadenza temporale e chilometrico; il promemoria è disponibile **solo in dashboard**, senza email, push o job in background. Non si aggiungono scadenze simulate né dati seed: le regole vengono create dall'utente.

Le ricorrenze possono essere **per km, per tempo o entrambi** (es. "ogni 15.000 km o 12 mesi"). Se l'intervento include i km, la lettura del contachilometri dello stesso giorno viene riutilizzata quando concorde; una lettura discordante blocca atomicamente la registrazione. Modellazione e politica di proiezione in `docs/architecture/CONTEXT.md`.

### Documenti

Assicurazione, revisione, documenti del mezzo, altri documenti.

> Le immagini del Journal usano lo storage definito da [`ADR-0010`](../adr/0010-journal-image-storage.md). I documenti generici del Camper restano previsti in Phase 4; un nuovo tipo di file richiede una decisione distinta sul suo ciclo di vita e sullo storage.

### Manuale tecnico e mappa delle specifiche — in progettazione

L'utente può caricare un PDF oppure indicare un URL HTTPS diretto a un PDF pubblico. Il file viene conservato in storage privato associato al Camper e l'URL originale, se presente, viene mantenuto. Il primo prototipo di parsing non invia il documento a servizi esterni e parte dall'estrazione del testo selezionabile; il supporto OCR per i PDF scansiti sarà valutato dopo la prova con un manuale reale. L'estrazione considera solo valori espliciti, normalizza le unità e lascia vuote le informazioni mancanti.

Il risultato è una bozza da rivedere e confermare prima di aggiornare il profilo. I campi principali esistenti (`Dimensions`, `Weights` e `Capacities`) restano tipizzati; si aggiornano solo quando la bozza contiene un valore esplicito, mentre un campo assente non cancella il valore corrente. Le misure puntuali aggiuntive sono persistenti e conservano punto/area, valore, unità, descrizione fedele al manuale e pagina-fonte. Ogni Camper ha un solo manuale attivo: confermare un nuovo manuale attiva il suo insieme completo di misure, mentre la fonte e l'insieme precedenti restano archiviati; non vengono fusi automaticamente.

La mappa è uno schema vettoriale illustrativo, non una geometria esatta del modello, con viste laterale, frontale e dall'alto. La prima versione si concentra su ingombro esterno e accessi; un dettaglio mostra misura, descrizione, fonte e apertura del PDF alla pagina citata.

> **Decisioni ancora aperte prima dell'implementazione:** fase e priorità nella roadmap, esito della valutazione OCR sui PDF scansiti, elenco dei punti cliccabili, limiti di upload e fetch sicuro degli URL. La forma dei dati, il ciclo di vita e la strategia di storage richiedono un ADR separato da ADR-0010; F1-F7 di ADR-0004 restano applicabili.

---

## 3. Trip Planner

Prima versione:

```text
Partenza → Destinazione → Tappe → Itinerario → Costi stimati → Checklist
```

Funzionalità: creazione viaggio, date, destinazione, tappe, note, budget, stima carburante, spese, checklist, luoghi salvati.

> ⚠️ **Scope v1 (chiude parte di C2).** Il Trip Planner v1 **non è vehicle-aware**: la distanza e la durata dell'intero viaggio sono stime manuali. Non è disponibile il routing automatico. Per `Planned` e `Active` è disponibile una stima locale del solo costo carburante: distanza totale manuale × consumo medio dichiarato nel profilo Camper ÷ 100 × prezzo per litro inserito nel Trip. Prezzo e risultato mantengono la stessa valuta `Money`, senza conversione; il risultato è derivato e live, non persistito, mentre le `Expense` restano consuntive. Se mancano distanza, consumo o prezzo non si mostra una stima parziale o un costo zero. Per `FuelKind.Electric` la stima è dichiarata non disponibile, senza conversione da kWh. `Completed` e `Cancelled` non mostrano la stima.

La distanza e la durata complessive del viaggio sono inserite manualmente e indicate come stime; non vengono calcolate da un provider né ricalcolate quando cambiano le tappe. La decisione #1 di [`DECISION-MAP-PRE-PUBBLICAZIONE.md`](DECISION-MAP-PRE-PUBBLICAZIONE.md) definisce questa scelta. Il prezzo per litro è facoltativo, inserito manualmente per Trip e persistito come `Money`; la UI legge il profilo Camper dall'endpoint esistente e ricalcola la stima senza provider o logica server-side.

Evoluzione futura del motore di viaggio: dimensioni del camper, altezza, peso, restrizioni, preferenze personali, durata, budget, meteo, stagionalità, autonomia.

---

## 4. Vehicle-Aware Routing

Feature strategica. Non chiedere soltanto "Come arrivo da A a B?" ma:

> **"Come arrivo da A a B con QUESTO camper?"**

Fattori da valutare: altezza, lunghezza, larghezza, peso, strade limitate, ponti, ZTL, strade strette, pendenze, accessibilità, caratteristiche del percorso.

Output previsto:

```text
🟢 Compatibile

237 km
3h 42m
€42 carburante stimato

2 soste consigliate
1 attenzione sul percorso
```

Il sistema deve distinguere sempre e in modo visibile **dato verificato**, **stima** e **informazione fornita dagli utenti**.

> 🔴 **Rischio principale di progetto (C2).** Nessuna fonte dati è stata scelta: né routing engine, né dati di restrizione, né aree di sosta.
> Domande aperte: chi fornisce il routing per veicoli alti/pesanti (HERE, TomTom, PTV, GraphHopper self-hosted su OSM)? Costo per chiamata e limiti del tier gratuito? La licenza permette di **memorizzare** i risultati? Da dove arrivano le aree di sosta (Park4Night e simili non sono licenziabili)?
> Azione: **spike tecnico time-boxed (max 3 giorni)** + ADR `geo-data-strategy` **prima** della Phase 3, non durante. Owner: @archimedes.

---

## 5. Checklist intelligente

L'utente può generare una checklist contestuale scegliendo **Partenza**, **Arrivo** o **Rimessaggio**. Prima di salvarla avvia una preview read-only; la checklist viene creata solo dopo una conferma esplicita. La preview non modifica il Trip. La conferma crea una nuova checklist con provenienza persistita: se esiste già una checklist dello stesso momento, resta invariata e la nuova viene aggiunta separatamente. Il CRUD manuale delle checklist resta disponibile.

Il primo catalogo controllato usa queste voci di base:

**Partenza:** chiudi finestre, chiudi gas, scollega corrente, controlla acqua, controlla gomme, controlla luci, blocca oggetti interni.

**Arrivo:** livella camper, collega corrente, controlla acqua, configura area esterna.

**Rimessaggio:** scarica acqua, controlla batteria, chiudi gas, controlla frigorifero, proteggi il mezzo.

Alle voci di base si aggiungono, senza duplicarle:

| Contesto | Momento | Voce |
|---|---|---|
| Le planned dates indicano almeno 7 notti | Partenza | Pianifica i rifornimenti di acqua e gas per la durata del viaggio |
| Equipaggiamento `BikeRack` | Partenza | Blocca il carico sul portabici |
| Equipaggiamento `Awning` | Partenza | Chiudi e blocca il tendalino |
| Equipaggiamento `Levellers` | Arrivo | Prepara i livellatori |
| Equipaggiamento `Battery` | Rimessaggio | Verifica la batteria servizi |

Date pianificate mancanti non attivano la regola sulla durata. In questa slice la checklist considera **solo** planned dates e equipaggiamento del Camper: geolocalizzazione, meteo/temperatura, AI e storico degli errori restano fuori.

> **Nota (review §4).** Le checklist tipiche e le manutenzioni standard sono **contenuto di prodotto, non dati di test**: vanno versionate e gestite come seed controllato.

### Manutenzioni tipiche da seed — pianificazione di prodotto, non incluse in questa slice

Decisa in [`ADR-0007`](../adr/0007-design-system.md) (D9): alla creazione del camper Roamly **precompila una lista curata di manutenzioni tipiche** (tagliando, revisione, gomme, bombole, filtri, batteria servizi, guarnizioni, antigelo circuito idrico…). È una mitigazione **parziale** del problema "account nuovo = schermate vuote". **La slice di manutenzione corrente non inserisce seed**: l'utente registra le proprie regole, così non compare alcuna attività mai scelta né si crea uno storico dimostrativo. L'eventuale catalogo controllato resta pianificato e richiederà implementazione esplicita.

| Requisito | Vincolo |
|---|---|
| **Lista curata e versionata** | contenuto di prodotto, non fixture di test. Non dipende dal modello del camper in Phase 1 |
| **Modificabili ed eliminabili** dall'utente | singolarmente **e in blocco con una sola azione** |
| **Origine persistita** | il flag "da seed" è **un dato**, non uno stato di UI: serve per l'export di ADR-0004 e per una eventuale rimozione futura della feature |
| **Marcatura visibile (R24)** | badge testuale "suggerita" tramite `SeedTag`, **in entrambi i temi** e **mai solo con il colore** |

> ⚠️ **Tradeoff accettato, non risolto.** Il seed crea righe che l'utente non ha inserito: per quanto marcate, potrebbe credere di averle registrate lui, o ritrovarsele in un export senza riconoscerle. **Un dato suggerito che sembra un dato reale è peggio dell'assenza di dato** — su una manutenzione è un dato su cui si prendono decisioni sul mezzo. La marcatura e la cancellazione in blocco sono la mitigazione; non è completa.
>
> Costo di inversione **MEDIO**: toglierla dopo lascia dati creati da una feature che non esiste più su account reali — servirebbe una migrazione, ed è il motivo per cui l'origine va persistita fin dal primo giorno.

---

## 6. Cost Management

Roamly deve conoscere il costo reale del camper.

Metriche: costo totale, costo per km, costo per giorno, costo per viaggio, carburante, autostrade, campeggi, manutenzione, assicurazione, gas, parcheggi, altre spese.

```text
2027

Carburante       € 2.430
Autostrade         € 310
Campeggi           € 640
Manutenzione       € 890
Assicurazione      € 720
Gas                € 190
Parcheggi          € 120
-----------------------
Totale           € 5.300

12.400 km
€ 0,43 / km
```

> ⚠️ **Vincolo di dominio.** Gli importi sono `Money` (importo + valuta), non `decimal` nudo: il multi-valuta è reale per chi viaggia in camper (CHF, GBP, valute non-euro). Unità canoniche di persistenza: **km** e **litri**. Vedi `docs/architecture/CONTEXT.md`.

> 🔴 **Il mockup qui sopra non è realizzabile così com'è.** Il domain model ha confermato `Money` **multi-valuta senza conversione**: senza tassi di cambio, un **totale unico in euro non esiste**. Sommare valute diverse ignorando il cambio produrrebbe un numero falso, e inventare un tasso sarebbe peggio.
>
> Le vie possibili, da decidere con @pixel quando si progetta la schermata: **totali separati per valuta**; oppure un totale nella sola valuta prevalente con le altre elencate a parte; oppure introdurre più avanti una valuta di reporting con tassi storici — che è la scelta che il domain model ha **deliberatamente rimandato**.
>
> Lo stesso vale per **`€ 0,43 / km`**. Nota inoltre che i costi di manutenzione **non sono righe di `Expense`**: vivono sul log dell'intervento, quindi la voce "Manutenzione" di questo prospetto va sommata da una seconda fonte.


---

## 7. Travel Journal

Ogni viaggio deve produrre memoria: percorso, tappe, fotografie, note, luoghi, spese, km, valutazioni, persone, ricordi.

La UI del Journal gestisce note e allegati JPEG/PNG/WebP: upload mediato dall'API, anteprima su richiesta ed eliminazione esplicita. Gli originali conservano gli EXIF; il primo upload verso un account Azure reale richiede la verifica operativa descritta in [`ADR-0010`](../adr/0010-journal-image-storage.md).

Obiettivo futuro:

> "Raccontami il mio viaggio in Toscana dell'estate 2027."

---

## 8. Roamly Intelligence

L'AI non è un chatbot generico aggiunto sopra: è costruita sopra il dominio Roamly.

Esempi di Copilot:

> "Organizzami un weekend al mare entro 250 km."
> "Parto venerdì alle 17 e torno domenica sera."
> "Trova un posto tranquillo adatto al mio camper."
> "Quanto spenderò indicativamente?"
> "Cosa devo controllare prima di partire?"
> "Cosa ho dimenticato l'ultima volta?"

> ⚠️ **Confini obbligatori (I11).** L'Intelligence:
> - vive dietro un'**interfaccia applicativa** con **feature flag**;
> - è sempre **degradabile**: se l'AI non risponde, l'app funziona lo stesso;
> - non è **mai** l'unico modo per ottenere un risultato;
> - non invia dati personali a terze parti senza base giuridica e informativa (vedi `SECURITY.md`).
> Provider di riferimento scelto per la futura configurazione: Google Cloud Gemini Enterprise Agent Platform su endpoint UE. L'utente ha scelto di accettare i default anti-abuso dei modelli Google ordinari, subordinatamente alla verifica dei termini effettivi, e di disabilitare cache implicita in-memory e durevole; Gemini resta disattivato se questo controllo non è verificabile. Gemini 3.1 Flash-Lite è il primo candidato per una futura valutazione con prompt sintetici, non una scelta di produzione. Modello/capacità, applicazione effettiva del controllo cache, costo per richiesta, latenza attesa e comportamento in caso di fallimento restano da verificare; vedi la due diligence e le decisioni #8-#10 della mappa.
>
> L'eventuale AI esterna è opzionale: per ogni richiesta l'utente sceglie e conferma il contesto minimo da inviare. Documenti, foto e posizione precisa sono esclusi. Il modello può produrre solo suggerimenti o bozze non vincolanti: dati, vincoli, calcoli, validazioni e persistenza restano sotto il controllo di Roamly; l'AI non esegue azioni di dominio.

> 🔴 **Il feature flag NON è il consenso** ([`ADR-0004`](../adr/0004-privacy-and-erasure.md)). Sono due assi ortogonali: il flag dice se la funzionalità esiste, il consenso dice se *quell'utente* ha autorizzato il trasferimento dei suoi dati a un terzo. La condizione effettiva è **`flag ∧ consenso`**, valutata in un unico punto.
> Finché non esiste alcun servizio terzo (oltre al provider email) la tabella dei consensi non serve. Il **primo invio effettivo** a un modello esterno la rende obbligatoria: è un prerequisito della Phase 4, non un dettaglio implementativo.

---

## 9. Personal Memory

Roamly impara dallo storico dell'utente.

```text
Preferenze                    Storico
├── mare                      ├── luoghi visitati
├── natura                    ├── valutazioni
├── poca confusione           ├── costi
├── soste tranquille          ├── consumi
└── viaggi 2-4 giorni         ├── itinerari
                              ├── checklist
                              └── note
```

In futuro:

> "L'ultima volta in una situazione simile hai dimenticato il cavo elettrico."

Questa memoria è un **profilo comportamentale geolocalizzato**: deve essere trasparente, modificabile, esportabile e cancellabile dall'utente. Decisione dell'utente (2026-10-21): il profilo inferito è visibile e modificabile voce per voce, esportabile e cancellabile; viene usato solo nelle funzioni richieste esplicitamente dall'utente e non è trasmesso automaticamente a terzi. Ogni eventuale richiesta AI resta soggetta ai confini di §8 e al consenso previsto da `ADR-0004`. Requisiti in `docs/architecture/SECURITY.md`.
