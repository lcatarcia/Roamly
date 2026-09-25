# Roamly — Funzionalità di prodotto

Stato: attivo · Ultimo aggiornamento: 2026-09-24
Origine: `docs/archive/Roamly_Planning_2026-09-24.md` §6bis, §7-§13

> Questo documento descrive **cosa** fa il prodotto. Lo **scope e l'ordine** sono in `docs/product/ROADMAP.md`.

---

## 1. Dashboard

La home risponde rapidamente a:

> "Come sta il mio camper e cosa devo fare?"

Widget previsti:

- prossime manutenzioni;
- scadenze;
- ultimo viaggio;
- km annuali;
- spese;
- stato checklist;
- prossima partenza.

> ⚠️ **Vincolo tecnico (review §4).** `GET /api/v1/dashboard` aggrega 6-7 fonti: va progettata come **query dedicata con projection e misurata**, non come composizione di N query per widget. Owner: @oracle.

---

## 2. My Camper

### Anagrafica

Dati tecnici, dimensioni, pesi, consumi, capacità.

### Equipaggiamento

Gestione degli accessori (pannelli solari, batteria, inverter, bombole, portabici, tendalino, livellatori).

### Manutenzione

Attività, scadenze, storico, costo, km al momento dell'intervento.

Le ricorrenze possono essere **per km, per tempo o entrambi** (es. "ogni 15.000 km o 12 mesi"): è la regola che genera i reminder della dashboard. Modellazione in `docs/architecture/CONTEXT.md`.

### Documenti

Assicurazione, revisione, documenti del mezzo, altri documenti.

> ⚠️ **Dipendenza aperta.** Documenti e foto richiedono uno storage file, non ancora deciso (decisione #8 in `docs/OPEN-DECISIONS.md`).

---

## 3. Trip Planner

Prima versione:

```text
Partenza → Destinazione → Tappe → Itinerario → Costi stimati → Checklist
```

Funzionalità: creazione viaggio, date, destinazione, tappe, note, budget, stima carburante, spese, checklist, luoghi salvati.

> ⚠️ **Scope v1 (chiude parte di C2).** Il Trip Planner v1 **non è vehicle-aware**: itinerario e costi sono una **stima distanza/carburante**, dichiarata come tale nella UI. Il routing consapevole del mezzo arriva solo dopo la decisione #3.

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

Le checklist non sono statiche.

**Partenza:** chiudi finestre, chiudi gas, scollega corrente, controlla acqua, controlla gomme, controlla luci, blocca oggetti interni.

**Arrivo:** livella camper, collega corrente, controlla acqua, configura area esterna.

**Rimessaggio:** scarica acqua, controlla batteria, chiudi gas, controlla frigorifero, proteggi il mezzo.

In futuro la checklist potrà dipendere da stagione, durata del viaggio, destinazione, temperatura, equipaggiamento e storico degli errori.

> **Nota (review §4).** Le checklist tipiche e le manutenzioni standard sono **contenuto di prodotto, non dati di test**: vanno versionate e gestite come seed controllato.

### Manutenzioni tipiche da seed — feature dell'MVP

Decisa in [`ADR-0007`](../adr/0007-design-system.md) (D9): alla creazione del camper Roamly **precompila una lista curata di manutenzioni tipiche** (tagliando, revisione, gomme, bombole, filtri, batteria servizi, guarnizioni, antigelo circuito idrico…). È una mitigazione **parziale** del problema "account nuovo = schermate vuote".

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
> Restano da dichiarare: provider, costo per richiesta, latenza attesa, comportamento in caso di fallimento.

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

Questa memoria è un **profilo comportamentale geolocalizzato**: deve essere trasparente, modificabile, esportabile e cancellabile dall'utente. Requisiti in `docs/architecture/SECURITY.md`.
