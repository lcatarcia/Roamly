# Roamly — Product Vision

> **Roamly** — The operating system for your camper life.

Stato: attivo · Ultimo aggiornamento: 2026-09-24
Origine: `docs/archive/Roamly_Planning_2026-09-24.md` §1, §2, §26, §31

---

## 1. Visione

Roamly è una webapp pensata per trasformare la gestione del camper e l'organizzazione dei viaggi in un'unica esperienza digitale.

Non deve essere percepita come una semplice app per trovare aree di sosta, pianificare itinerari, ricordare manutenzioni o registrare spese.

Il concetto centrale è:

> **Roamly conosce il mio camper, conosce il mio modo di viaggiare e usa queste informazioni per aiutarmi a prendere decisioni migliori.**

La direzione è un **personal travel operating system** per camperisti.

---

## 2. Principi di prodotto

### 2.1 Camper-first

Il camper è il centro del dominio, non una sezione del profilo utente.

Dimensioni, peso, autonomia, equipaggiamento, consumi e caratteristiche del mezzo influenzano le funzionalità di viaggio.

### 2.2 Personalizzazione progressiva

Roamly diventa più utile con l'utilizzo. Ogni viaggio alimenta preferenze, luoghi visitati, costi, consumi, valutazioni, abitudini, checklist e memoria personale.

### 2.3 UX prima della complessità

La complessità sta nel backend. L'interfaccia deve sembrare semplice, veloce e quasi "magica".

### 2.4 Dati strutturati

Le informazioni importanti sono modellate come dati strutturati, non affidate a testo libero. Questo abilita analytics, suggerimenti, automazioni, AI e raccomandazioni personalizzate.

### 2.5 API-first

Il backend espone API solide e versionabili. Il frontend React non contiene logica di dominio.

### 2.6 Trasparenza sui dati personali

Roamly tratta posizione geografica storica, foto, documenti del veicolo, spese e abitudini di viaggio: la memoria personale deve essere sempre **ispezionabile, modificabile ed esportabile dall'utente**.

Questo principio ha requisiti verificabili in `docs/architecture/SECURITY.md`.

**Concretamente**, dal 2026-09-24 non è più un proposito ma tre verificatori automatici (R8, R9, R10 in `SECURITY.md` §2.2) e un contratto: export ZIP dei propri dati, cancellazione **fisica** dell'account con 30 giorni di grazia, e una ricevuta che prova l'avvenuta cancellazione. Vedi [`ADR-0004`](../adr/0004-privacy-and-erasure.md).

> **Limite dichiarato onestamente:** l'export dell'MVP è **machine-readable, non curato**. È un archivio di JSON, non un album. Diventa presentabile quando serve — costa poco aggiungerlo dopo.

---

## 3. I tre pilastri

```text
             ROAMLY
                │
     ┌──────────┼──────────┐
     ▼          ▼          ▼
  MY CAMPER   MY TRIPS   MY MEMORY
     │          │          │
 gestione    pianifica   impara
     │          │          │
     └──────────┼──────────┘
                ▼
        ROAMLY INTELLIGENCE
```

I tre pilastri condividono lo stesso modello dati: ogni nuova informazione inserita dall'utente aumenta il valore delle funzionalità successive.

Roamly non è un **camper manager**: è un **Camper + Travel OS**.

Visione finale:

> **"Dimmi dove vuoi andare. Io conosco il tuo camper, conosco il tuo modo di viaggiare, conosco quello che hai già fatto e ti aiuto a organizzare tutto il resto."**

---

## 4. Metriche di successo

Non misurare solo utenti registrati.

### Activation

- camper configurato;
- primo viaggio creato;
- prima checklist completata.

### Engagement

- viaggi creati;
- viaggi completati;
- luoghi salvati;
- manutenzioni registrate;
- spese registrate.

### Retention

- ritorno dopo un viaggio;
- uso della dashboard;
- uso del diario;
- pianificazione del viaggio successivo.

### Product value

- tempo risparmiato nella pianificazione;
- errori/preparazioni evitate;
- accuratezza delle stime;
- riutilizzo delle checklist.

### Strumentazione

> ⚠️ **Gap noto (review §4).** Le metriche sopra non hanno oggi alcun meccanismo di raccolta.
> Requisito: ogni metrica di activation/engagement deve corrispondere a un **evento di dominio tracciato** (`CamperCreated`, `TripCreated`, `TripCompleted`, `ChecklistCompleted`), altrimenti la metrica non esiste.
> Owner: @archimedes. Da definire prima della Phase 1.
>
> **Vincolo da [`ADR-0004`](../adr/0004-privacy-and-erasure.md):** la strumentazione va su **due binari**. Gli eventi **identificabili** (riconducibili a un utente) sono entità owned: entrano nell'export e **spariscono con l'account**. I **contatori aggregati**, privi di identificatori, sopravvivono. Mescolarli significa o perdere tutte le metriche a ogni cancellazione, o conservare dati che si è dichiarato di aver cancellato.

---

## Documenti collegati

- Funzionalità: `docs/product/FEATURES.md`
- Roadmap e scope: `docs/product/ROADMAP.md`
- UX e design system: `docs/product/UX.md`
- Architettura: `docs/architecture/ARCHITECTURE.md`
