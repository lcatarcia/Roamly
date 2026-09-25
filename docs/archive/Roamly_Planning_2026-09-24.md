# Roamly — Product & Technical Planning

> **Roamly** — The operating system for your camper life.

## 1. Visione

Roamly è una webapp pensata per trasformare la gestione del camper e l'organizzazione dei viaggi in un'unica esperienza digitale.

Non deve essere percepita come una semplice app per:
- trovare aree di sosta;
- pianificare itinerari;
- ricordare manutenzioni;
- registrare spese.

Il concetto centrale è invece:

> **Roamly conosce il mio camper, conosce il mio modo di viaggiare e usa queste informazioni per aiutarmi a prendere decisioni migliori.**

Il prodotto dovrà quindi evolvere verso un **personal travel operating system** per camperisti.

---

## 2. Principi di prodotto

### 2.1 Camper-first

Il camper è il centro del dominio.

Dimensioni, peso, autonomia, equipaggiamento, consumi e caratteristiche del mezzo devono influenzare le funzionalità di viaggio.

### 2.2 Personalizzazione progressiva

Roamly deve diventare più utile con l'utilizzo.

Ogni viaggio può alimentare:
- preferenze;
- luoghi visitati;
- costi;
- consumi;
- valutazioni;
- abitudini;
- checklist;
- memoria personale.

### 2.3 UX prima della complessità

La complessità deve stare nel backend.

L'interfaccia deve sembrare semplice, veloce e quasi "magica".

### 2.4 Dati strutturati

Le informazioni importanti devono essere modellate come dati strutturati, non affidate esclusivamente a testo libero.

Questo permetterà in futuro:
- analytics;
- suggerimenti;
- automazioni;
- AI;
- raccomandazioni personalizzate.

### 2.5 API-first

Il backend deve esporre API solide e versionabili.

Il frontend React non deve contenere logica di dominio.

---

# 3. Stack tecnologico

## Backend

- **C#**
- **ASP.NET Core 10**
- Entity Framework Core
- REST API
- OpenAPI / Swagger
- Dependency Injection nativa
- CQRS
- Vertical Slice Architecture
- MediatR o equivalente per request/handler orchestration, se utile
- FluentValidation o equivalente
- Authentication / Authorization
- Structured logging
- Health checks
- Background services quando necessari

## Database

Scelta esplicita:

- **Microsoft SQL Server**
- Entity Framework Core
- SQL Server migrations versionate
- Seed data controllati
- dati geografici gestiti tramite le funzionalità spatial di SQL Server quando necessario

> Il database applicativo sarà SQL Server. Non verrà utilizzato PostgreSQL.

## Frontend

- **React**
- **Vite**
- TypeScript
- React Router
- TanStack Query
- libreria componenti/design system da definire
- gestione form tipizzata
- client API generato o fortemente tipizzato

## DevOps

- Git
- GitHub
- **GitHub Actions**
- CI automatizzata
- build backend
- test backend
- build frontend
- lint/test frontend
- database migration strategy
- Docker
- environment separation

---

# 4. Architettura proposta

```text
                           ┌──────────────────────┐
                           │       Browser        │
                           │ React + Vite + TS    │
                           └──────────┬───────────┘
                                      │ HTTPS
                                      ▼
                           ┌──────────────────────┐
                           │    ASP.NET Core 10   │
                           │       Web API        │
                           └──────────┬───────────┘
                                      │
                  ┌───────────────────┼───────────────────┐
                  │                   │                   │
                  ▼                   ▼                   ▼
           ┌────────────┐      ┌────────────┐      ┌────────────┐
           │ PostgreSQL │      │ External   │      │ AI /       │
           │ + PostGIS  │      │ Services   │      │ Intelligence│
           └────────────┘      └────────────┘      └────────────┘
```

## Struttura repository

La struttura backend seguirà **Vertical Slice Architecture**, non una classica separazione globale per layer (`Controllers`, `Services`, `Repositories`, ecc.).

```text
roamly/
├── src/
│   ├── Roamly.Api/
│   │   ├── Features/
│   │   │   ├── Authentication/
│   │   │   ├── Campers/
│   │   │   ├── Maintenance/
│   │   │   ├── Documents/
│   │   │   ├── Trips/
│   │   │   ├── Places/
│   │   │   ├── Expenses/
│   │   │   ├── Checklists/
│   │   │   ├── Journal/
│   │   │   └── Intelligence/
│   │   ├── Common/
│   │   └── Program.cs
│   │
│   ├── Roamly.Infrastructure/
│   │   └── ...
│   │
│   └── Roamly.Domain/
│       └── ...
│
├── tests/
│   ├── Roamly.Api.Tests/
│   ├── Roamly.IntegrationTests/
│   └── Roamly.Domain.Tests/
│
├── frontend/
│   └── roamly-web/
│
├── database/
│   └── ...
│
├── docs/
│   ├── architecture/
│   ├── product/
│   └── adr/
│
├── .github/
│   └── workflows/
│
├── docker/
│   └── ...
│
└── README.md
```

La struttura potrà essere semplificata ulteriormente se il progetto lo richiede. Il principio importante è che ogni feature verticale contenga vicino tra loro request, handler, validation, mapping e response model.

L'obiettivo è organizzare il codice **per comportamento/feature**, non per tipo tecnico.

---

# 5. Architettura applicativa: Vertical Slice + CQRS

Roamly utilizzerà **Vertical Slice Architecture** come principale criterio di organizzazione del backend.

Non vogliamo questo:

```text
Controllers/
Services/
Repositories/
DTOs/
Validators/
Handlers/
```

Vogliamo invece:

```text
Features/
├── Campers/
│   ├── CreateCamper/
│   │   ├── Endpoint.cs
│   │   ├── Command.cs
│   │   ├── Handler.cs
│   │   ├── Validator.cs
│   │   └── Response.cs
│   │
│   ├── GetCamper/
│   │   ├── Endpoint.cs
│   │   ├── Query.cs
│   │   ├── Handler.cs
│   │   └── Response.cs
│   │
│   └── UpdateCamper/
│       ├── Endpoint.cs
│       ├── Command.cs
│       ├── Handler.cs
│       ├── Validator.cs
│       └── Response.cs
```

## CQRS

Il sistema distinguerà chiaramente:

### Commands

Operazioni che modificano lo stato.

Esempi:

```text
CreateCamper
UpdateCamper
AddMaintenance
CompleteMaintenance
CreateTrip
AddTripStop
AddExpense
CompleteChecklist
```

### Queries

Operazioni di sola lettura.

Esempi:

```text
GetCamperDashboard
GetCamper
GetMaintenanceSchedule
GetTrip
GetTripSummary
GetExpenses
GetJournal
```

Non è necessario introdurre due database o event sourcing.

**CQRS in Roamly è una separazione logica tra write model e read model**, mantenendo inizialmente un singolo SQL Server.

## Vertical Slice principles

Ogni feature deve:

- essere il più possibile autonoma;
- possedere il proprio handler;
- validare il proprio input;
- definire il proprio response contract;
- evitare dipendenze inutili da servizi globali;
- contenere query ottimizzate per il caso d'uso;
- mantenere la logica specifica vicino alla feature.

La duplicazione controllata è preferibile a un'astrazione prematura.

## Database access

Entity Framework Core verrà utilizzato direttamente dalle feature dove appropriato.

Evitare il classico:

```text
GenericRepository<T>
GenericService<T>
```

quando non aggiunge valore.

Per le query read-heavy sarà possibile utilizzare:

- projection LINQ;
- `AsNoTracking`;
- SQL mirato quando necessario;
- query specifiche per feature.

Per le command verrà privilegiata una modifica esplicita e transazionale del modello.

---

# 6. Core Domain

Il dominio iniziale dovrebbe comprendere almeno:

```text
User
 │
 └── Camper
      │
      ├── Specifications
      ├── Equipment
      ├── Documents
      ├── Maintenance
      ├── Expenses
      └── Trips
            │
            ├── Route
            ├── Stops
            ├── Places
            ├── Expenses
            ├── Checklist
            ├── Photos
            └── Journal
```

## Entità iniziali

### User

Account e preferenze personali.

### Camper

Identità del mezzo.

Esempi:
- marca;
- modello;
- anno;
- lunghezza;
- larghezza;
- altezza;
- peso;
- portata;
- carburante;
- consumi;
- capacità acqua;
- caratteristiche energetiche.

### Equipment

Oggetti/accessori presenti sul camper.

Esempi:
- pannelli solari;
- batteria;
- inverter;
- bombole;
- portabici;
- tendalino;
- livellatori.

### MaintenanceItem

Manutenzioni e attività ricorrenti.

Esempi:
- cambio olio;
- pneumatici;
- revisione;
- distribuzione;
- controlli tecnici.

### Document

Documenti relativi a camper e viaggio.

### Expense

Spese associate al camper o a uno specifico viaggio.

### Trip

Un viaggio.

### TripStop

Tappa di un viaggio.

### Place

Luogo geolocalizzato.

Potrà rappresentare:
- area sosta;
- campeggio;
- parcheggio;
- punto panoramico;
- ristorante;
- distributore;
- officina;
- supermercato;
- POI.

### Checklist

Lista contestuale al viaggio.

### JournalEntry

Memoria/diario del viaggio.

---

# 6. MVP 1.0

L'obiettivo del primo MVP non è costruire tutto.

Deve dimostrare che Roamly è utile anche senza AI avanzata.

## Dashboard

La home deve rispondere rapidamente a:

> "Come sta il mio camper e cosa devo fare?"

Possibili widget:

- prossime manutenzioni;
- scadenze;
- ultimo viaggio;
- km annuali;
- spese;
- stato checklist;
- prossima partenza.

---

## My Camper

### Anagrafica

- dati tecnici;
- dimensioni;
- pesi;
- consumi;
- capacità.

### Equipaggiamento

Gestione degli accessori.

### Manutenzione

- attività;
- scadenze;
- storico;
- costo;
- km al momento dell'intervento.

### Documenti

- assicurazione;
- revisione;
- documenti del mezzo;
- altri documenti.

---

# 7. Trip Planner

Prima versione:

```text
Partenza
   ↓
Destinazione
   ↓
Tappe
   ↓
Itinerario
   ↓
Costi stimati
   ↓
Checklist
```

Funzionalità:

- creazione viaggio;
- date;
- destinazione;
- tappe;
- note;
- budget;
- stima carburante;
- spese;
- checklist;
- luoghi salvati.

## Evoluzione futura

Il motore di viaggio dovrà poter considerare:

- dimensioni del camper;
- altezza;
- peso;
- eventuali restrizioni;
- preferenze personali;
- durata;
- budget;
- meteo;
- stagionalità;
- autonomia.

---

# 8. Vehicle-Aware Routing

Questa è una delle feature strategiche di Roamly.

Non chiedere soltanto:

> "Come arrivo da A a B?"

ma:

> **"Come arrivo da A a B con QUESTO camper?"**

Il sistema potrà in futuro valutare:

- altezza;
- lunghezza;
- larghezza;
- peso;
- strade limitate;
- ponti;
- ZTL;
- strade strette;
- pendenze;
- accessibilità;
- caratteristiche del percorso.

Output previsto:

```text
🟢 Compatibile

237 km
3h 42m
€42 carburante stimato

2 soste consigliate
1 attenzione sul percorso
```

Questa feature richiederà fonti dati geografiche affidabili e dovrà distinguere chiaramente tra dati verificati, stime e informazioni fornite dagli utenti.

---

# 9. Checklist intelligente

Le checklist non devono essere statiche.

Esempi:

### Partenza

- chiudi finestre;
- chiudi gas;
- scollega corrente;
- controlla acqua;
- controlla gomme;
- controlla luci;
- blocca oggetti interni.

### Arrivo

- livella camper;
- collega corrente;
- controlla acqua;
- configura area esterna.

### Rimessaggio

- scarica acqua;
- controlla batteria;
- chiudi gas;
- controlla frigorifero;
- proteggi il mezzo.

In futuro la checklist potrà dipendere da:

- stagione;
- durata del viaggio;
- destinazione;
- temperatura;
- equipaggiamento;
- storico degli errori.

---

# 10. Cost Management

Roamly deve conoscere il costo reale del camper.

Metriche:

- costo totale;
- costo per km;
- costo per giorno;
- costo per viaggio;
- carburante;
- autostrade;
- campeggi;
- manutenzione;
- assicurazione;
- gas;
- parcheggi;
- altre spese.

Esempio:

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

---

# 11. Travel Journal

Ogni viaggio deve produrre memoria.

Possibili contenuti:

- percorso;
- tappe;
- fotografie;
- note;
- luoghi;
- spese;
- km;
- valutazioni;
- persone;
- ricordi.

Obiettivo futuro:

> "Raccontami il mio viaggio in Toscana dell'estate 2027."

Roamly potrà generare automaticamente una storia del viaggio.

---

# 12. Roamly Intelligence

L'AI non deve essere aggiunta come chatbot generico.

Deve essere costruita sopra il dominio Roamly.

## Roamly Copilot

Esempi:

> "Organizzami un weekend al mare entro 250 km."

> "Parto venerdì alle 17 e torno domenica sera."

> "Trova un posto tranquillo adatto al mio camper."

> "Quanto spenderò indicativamente?"

> "Cosa devo controllare prima di partire?"

> "Cosa ho dimenticato l'ultima volta?"

> "Quali posti simili a quelli che ho apprezzato posso visitare?"

---

# 13. Personal Memory

Una delle feature strategiche a medio termine.

Roamly deve imparare dallo storico dell'utente.

Esempi:

```text
Preferenze
├── mare
├── natura
├── poca confusione
├── soste tranquille
└── viaggi 2-4 giorni
```

E soprattutto:

```text
Storico
├── luoghi visitati
├── valutazioni
├── costi
├── consumi
├── itinerari
├── checklist
└── note
```

In futuro:

> "L'ultima volta in una situazione simile hai dimenticato il cavo elettrico."

Questa memoria dovrà essere trasparente, modificabile e sotto il controllo dell'utente.

---

# 14. Frontend — Direzione UX

Questa è una parte fondamentale del progetto.

Il frontend **non deve sembrare un gestionale**.

Deve avere una personalità propria.

## Principi visuali

- premium;
- minimal;
- travel-oriented;
- map-centric;
- fotografico;
- micro-animazioni;
- card dinamiche;
- ottima tipografia;
- whitespace;
- dark/light mode ben progettati;
- mobile-first;
- responsive.

## Sensazione desiderata

Non:

> "Sto compilando dati del mio camper."

Ma:

> "Sto entrando nel mio mondo di viaggio."

---

# 15. Home / Dashboard

La dashboard dovrebbe avere una forte componente visuale.

Possibile struttura:

```text
┌─────────────────────────────────────────────┐
│ Good morning, Luca                          │
│                                             │
│        🚐 Your next adventure               │
│                                             │
│  TOSCANA                                    │
│  3 days · 482 km · €180 estimated           │
│                                             │
│              [ CONTINUE PLANNING ]          │
└─────────────────────────────────────────────┘

┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│ 🚐 Camper   │ │ 🗺 Trips    │ │ 💰 Costs    │
│ Healthy     │ │ 12 trips    │ │ €2.430      │
└─────────────┘ └─────────────┘ └─────────────┘
```

Il layout definitivo sarà definito durante la progettazione UX.

---

# 16. Design System

Roamly dovrà avere un design system proprietario.

Da definire:

- color palette;
- typography;
- spacing;
- radius;
- shadows;
- icons;
- buttons;
- cards;
- map components;
- timeline;
- charts;
- status indicators;
- empty states;
- loading states;
- animations.

Non vogliamo una UI "standard React".

Vogliamo una UI riconoscibile.

---

# 17. API Design

Le API saranno progettate intorno alle **feature verticali**, mantenendo endpoint semplici e coerenti.

Esempi:

```text
POST   /api/v1/campers
GET    /api/v1/campers/{id}
PUT    /api/v1/campers/{id}

POST   /api/v1/campers/{id}/maintenance
GET    /api/v1/campers/{id}/maintenance

POST   /api/v1/trips
GET    /api/v1/trips/{id}
POST   /api/v1/trips/{id}/stops
POST   /api/v1/trips/{id}/expenses

GET    /api/v1/dashboard
```

Il mapping endpoint → command/query sarà esplicito.

Il contratto API dovrà essere documentato tramite OpenAPI.

---

# 18. Database

Database relazionale:

## **Microsoft SQL Server**

Motivazioni:

- ottima integrazione con C# e ASP.NET Core;
- Entity Framework Core maturo;
- SQL relazionale completo;
- supporto ai dati geografici tramite SQL Server Spatial;
- tooling e diagnostica solidi;
- ottimo fit con un backend .NET.

## Regole

- migrations versionate;
- foreign keys;
- constraints;
- indici progettati per query reali;
- audit fields dove utili;
- UTC per date/time persistiti;
- `decimal` per denaro;
- `rowversion`/concurrency control dove utile;
- spatial types quando necessari;
- niente logica geografica critica solamente nel frontend.

Non introdurre un database separato per CQRS finché non esiste una necessità concreta.

---

# 19. Testing

## Backend

- unit test;
- integration test;
- API test;
- test del dominio;
- test delle query importanti.

## Frontend

- unit test dove utile;
- component test;
- E2E per flussi critici.

## Priorità

Prima testare:

1. autenticazione;
2. camper;
3. manutenzione;
4. viaggi;
5. spese;
6. checklist;
7. routing/geo logic.

---

# 20. GitHub Actions

Pipeline iniziale:

```text
Push / Pull Request
        │
        ▼
   ┌──────────────┐
   │ Checkout     │
   └──────┬───────┘
          ▼
   ┌──────────────┐
   │ Backend      │
   │ Restore      │
   │ Build        │
   │ Test         │
   └──────┬───────┘
          │
          ▼
   ┌──────────────┐
   │ Frontend     │
   │ Install      │
   │ Lint         │
   │ Test         │
   │ Build        │
   └──────┬───────┘
          │
          ▼
   ┌──────────────┐
   │ Docker Build │
   └──────┬───────┘
          │
          ▼
        Deploy
```

Workflow separati consigliati:

- `ci.yml`
- `backend.yml`
- `frontend.yml`
- `docker.yml`
- `deploy.yml`

La separazione effettiva verrà definita in base all'infrastruttura scelta.

---

# 21. Docker

Ambiente locale:

```text
docker compose
│
├── roamly-api
├── roamly-web
└── postgres
```

Possibile sviluppo futuro:

```text
PostgreSQL
Redis
Object Storage
API
Frontend
Background Worker
```

Redis e worker non vanno introdotti prematuramente.

---

# 22. Sicurezza

Baseline:

- HTTPS;
- authentication;
- authorization;
- password hashing gestito da ASP.NET Identity o soluzione equivalente;
- secrets fuori dal repository;
- environment variables;
- validation input;
- rate limiting dove necessario;
- CORS configurato esplicitamente;
- protezione upload;
- logging senza dati sensibili.

---

# 23. Observability

Da prevedere fin dall'inizio, ma senza overengineering.

- structured logging;
- correlation ID;
- health checks;
- error handling centralizzato;
- metriche applicative in futuro;
- tracing distribuito quando il sistema lo richiederà.

---

# 24. Roadmap

## Phase 0 — Foundation

- repository;
- solution .NET;
- React/Vite;
- PostgreSQL;
- Docker;
- GitHub Actions;
- environment configuration;
- authentication foundation;
- design system foundation.

## Phase 1 — Camper

- camper profile;
- specifications;
- equipment;
- maintenance;
- documents;
- dashboard.

## Phase 2 — Trips

- trip CRUD;
- stops;
- route;
- expenses;
- checklist;
- journal.

## Phase 3 — Maps & Places

- map;
- geolocation;
- places;
- search;
- saved places;
- geographic data.

## Phase 4 — Intelligence

- vehicle-aware constraints;
- recommendations;
- contextual checklist;
- cost estimation;
- AI Copilot.

## Phase 5 — Personal Memory

- travel history;
- preference extraction;
- recommendations based on history;
- personalized insights.

## Phase 6 — Community

Solo se il prodotto lo richiederà:

- places shared by users;
- ratings;
- reviews;
- camper-specific accessibility data;
- collaborative data quality.

---

# 25. Cosa NON fare inizialmente

Evitare:

- microservizi;
- Kubernetes;
- event-driven architecture complessa;
- Redis senza necessità;
- social network;
- marketplace;
- app mobile native;
- AI ovunque;
- decine di integrazioni esterne;
- sistema di recensioni complesso.

La prima versione deve essere **solida, bella e realmente utilizzabile dal proprietario di un camper**.

---

# 26. Metriche di successo

Non misurare solo utenti registrati.

Metriche più significative:

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

---

# 27. Decisioni architetturali iniziali

| Decisione | Scelta |
|---|---|
| Backend | ASP.NET Core 10 |
| Linguaggio | C# |
| Frontend | React + Vite |
| Frontend language | TypeScript |
| Database | PostgreSQL |
| Geo | PostGIS |
| ORM | Entity Framework Core |
| API | REST + OpenAPI |
| CI/CD | GitHub Actions |
| Container | Docker |
| Repository | GitHub |
| Architecture | Modular monolith |
| AI | Layer applicativo separato |
| Mobile | Responsive web first |

---

# 28. Principio architetturale chiave

## Modular Monolith

Roamly partirà come **modular monolith**, non come microservices.

Moduli iniziali:

```text
Roamly
│
├── Identity
├── Campers
├── Maintenance
├── Documents
├── Trips
├── Places
├── Expenses
├── Checklists
├── Journal
└── Intelligence
```

Ogni modulo deve avere confini chiari.

Questo permette di:

- sviluppare velocemente;
- mantenere semplice il deployment;
- evitare distributed systems complexity;
- estrarre successivamente un servizio se realmente necessario.

---

# 29. ADR (Architecture Decision Records)

Le decisioni importanti devono essere documentate.

Esempi:

```text
docs/adr/
├── 0001-use-postgresql.md
├── 0002-modular-monolith.md
├── 0003-react-vite.md
├── 0004-api-versioning.md
└── 0005-geo-data-strategy.md
```

Questo renderà il progetto più facile da evolvere e mantenere.

---

# 30. Prossimo step

Prima di implementare il codice è opportuno definire, in quest'ordine:

1. **Product Vision definitiva**
2. **Personas**
3. **User journeys**
4. **MVP scope**
5. **Domain model**
6. **Database schema**
7. **API contract**
8. **Frontend information architecture**
9. **Roamly Design System**
10. **GitHub repository structure**
11. **CI/CD**
12. **First vertical slice**

## Prima vertical slice consigliata

La prima funzionalità completa da implementare dovrebbe essere:

```text
Login
  ↓
Create Camper
  ↓
Camper Dashboard
  ↓
Add Maintenance
  ↓
Maintenance reminder
```

Ogni passaggio sarà implementato come una o più **vertical slices CQRS**.

Questo permette di validare contemporaneamente:

- authentication;
- SQL Server;
- EF Core;
- CQRS;
- Vertical Slice Architecture;
- API;
- React;
- routing;
- design system;
- validation;
- GitHub Actions;
- deployment.

Da lì si può costruire il resto in modo incrementale.

---

# 31. Visione finale

Roamly dovrebbe arrivare a questo punto:

> **"Dimmi dove vuoi andare. Io conosco il tuo camper, conosco il tuo modo di viaggiare, conosco quello che hai già fatto e ti aiuto a organizzare tutto il resto."**

Il prodotto non è quindi semplicemente un **camper manager**.

È un:

## **Camper + Travel OS**

con tre pilastri:

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

La direzione futura è fare in modo che questi tre pilastri condividano lo stesso modello dati, così ogni nuova informazione inserita dall'utente aumenta il valore delle funzionalità successive.
