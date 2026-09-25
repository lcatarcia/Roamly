# ADR-0001 — SQL Server come database applicativo, tipi spaziali rimandati

- **Stato:** Accettato
- **Data:** 2026-09-24
- **Owner:** @oracle
- **Decisore:** utente
- **Severità:** HIGH
- **Chiude:** decisione #1 · finding C1 della review

> ## ⚠️ Emendato il 2026-09-24 dal domain model (@archimedes)
>
> **`Coordinates` è modellato come *complex type* di EF Core 10, non come *owned type*.** La decisione sostanziale di questo ADR — coordinate come `decimal` WGS84, nessun `geography`, nessun NetTopologySuite — **resta invariata**. Cambia solo il meccanismo di mappatura.
>
> **Perché.** Un owned type è a tutti gli effetti una **entità** nel modello EF: comparirebbe quindi nei verificatori automatici di ADR-0003 e ADR-0004 (R1 ownership, R8 copertura di export e cancellazione), costringendo ad allargare le whitelist per qualcosa che non è un'entità ma un **value object**. Il complex type di EF Core 10 lo rappresenta per ciò che è: non ha identità, non ha chiave, non entra nel change tracker come entità distinta e sparisce dal problema.
>
> Lo stesso vale per **`Money`** (importo + valuta) e per i **tre gruppi di specifiche del camper** (dimensioni, pesi, capacità), introdotti dal domain model.
>
> **Costo di questa correzione:** nullo oggi — non esiste ancora codice né migration. Sarebbe stato alto dopo la prima migration.


---

## Contesto

Roamly è un personal travel OS per camperisti con una componente geografica **crescente ma differita**:

- **MVP 1.0** (Login → Create Camper → Dashboard → Maintenance) non contiene **nemmeno un campo coordinata**;
- **1.1** (Trip Planner) calcola stime di distanza su poche tappe (tipicamente 2-10), esposte in UI come stime dichiarate;
- **Phase 3** (Maps & Places) introduce luoghi salvati e ricerca per raggio, ma su dati **owner-scoped**: poche migliaia di righe per utente;
- il **vehicle-aware routing** arriva in Phase 4 e dipende da un **provider esterno** (decisione #3, ancora aperta), non dal database.

Vincoli reali: sviluppatore singolo, stack .NET, priorità alla velocità di delivery. PostgreSQL/PostGIS è stato valutato e **rifiutato esplicitamente** prima di questo ADR.

Il planning originale conteneva una contraddizione (finding C1): dichiarava SQL Server ma lasciava PostgreSQL/PostGIS nel diagramma di architettura, nel docker compose, nella roadmap e nella tabella delle decisioni. La documentazione è stata allineata il 2026-09-24.

### Fatti verificati

| Fatto | Esito |
|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` esiste, è Microsoft (`dotnet/efcore`), linea **10.x** per EF Core 10 / .NET 10 | ✅ |
| EF Core 10 è **LTS**, supporto fino a nov. 2028 | ✅ |
| `NetTopologySuite` 2.6.0 (mar. 2025), port di JTS 1.20.0 — cadenza bassa ma progetto maturo, non abbandonato | ✅ |
| L'immagine Docker `mcr.microsoft.com/mssql/server` (Linux) **include il supporto spaziale nativamente**, a costo zero | ✅ |
| **Azure SQL Edge è stato ritirato (set. 2025)** — non è un'alternativa | ✅ |
| EF Core **non genera indici spaziali**: servono `migrationBuilder.Sql` manuali (`dotnet/efcore#12538`) | ✅ |
| Una migration con colonna `geography` **fallisce su SQLite** (`dotnet/efcore#23812`) | ✅ |
| Numero di patch esatto del pacchetto spaziale | ⚠️ da riconfermare al momento dell'eventuale installazione |

---

## Opzioni considerate

### Opzione A — NetTopologySuite + `geography` fin da subito

Modellare `Place.Location` e `TripStop.Location` come `Point` SRID 4326 su colonna `geography`, con indice spaziale creato a mano in migration.

**Pro:** modello corretto dal primo giorno; distanze geodetiche esatte in metri senza codice custom; punto-in-poligono subito disponibile; una decisione chiusa e archiviata.

**Contro:** paga oggi un costo che serve fra due fasi; **inverte l'ordine delle decisioni** forzando la #6 (test) prima che @argus la prenda; introduce subito la trappola SRID 0 e la left-hand rule; SQL manuale in ogni migration con `Down()` da mantenere; complica il seed data.

**Costo di inversione (A → B): ALTO.** Migration di rimozione colonna, riscrittura delle query LINQ spaziali, e **perdita di dati** se la posizione è persistita solo come `geography`.

### Opzione C — Tipi spaziali con `geometry` invece di `geography`

**Contro decisivo:** `STDistance` restituirebbe **gradi, non metri**. La conversione gradi→km dipende dalla latitudine (1° di longitudine ≈ 111 km all'equatore, ≈ 74 km a Roma, ≈ 62 km a Oslo): su un'app che copre l'Europa e mostra "237 km" all'utente, l'errore è a doppia cifra percentuale. Evitarlo richiede riproiezione (UTM/LAEA), cioè **più** complessità di `geography`, non meno. Cumula gli svantaggi di A senza il suo vantaggio principale.

**Scartata.**

### Opzione D — PostgreSQL + PostGIS

Oggettivamente superiore su geografia avanzata (KNN indicizzato componibile, clustering, vector tiles, pgRouting) e con estensione Docker più leggera.

**Scartata per decisione dell'utente.** Si registra però un fatto emerso dall'analisi: **per il perimetro reale di Roamly i vantaggi di PostGIS sarebbero in larga parte inutilizzati**, perché il routing verrà da un provider esterno. Il rimpianto potenziale è minore di quanto il planning lasciasse intendere.

---

## Decisione

1. Il database applicativo è **Microsoft SQL Server**. PostgreSQL non viene utilizzato.
2. **NetTopologySuite e i tipi spaziali NON vengono adottati nella v1.**
3. Le coordinate si persistono come **`Latitude decimal(8,6)` / `Longitude decimal(9,6)`** in WGS84 (~11 cm di precisione), modellate come **complex type `Coordinates`** nel dominio (*emendato il 2026-09-24, vedi il banner in testa: originariamente owned type*), con indice composito `(OwnerId, Latitude, Longitude)`.
4. Il filtro per raggio è un **bounding box in SQL + affinamento Haversine in C#**.
5. L'upgrade a `geography` SRID 4326 con `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` è **pre-approvato** al verificarsi di uno dei trigger sotto.

---

## Motivazione

Il pacchetto spaziale è disponibile e affidabile, ma **il progetto non ha oggi un problema che quel pacchetto risolva**, e il costo di adottarlo più tardi è quasi nullo mentre il costo di rimuoverlo è alto.

L'argomento decisivo è l'**asimmetria del costo di inversione**:

- `decimal → geography` è una migration **puramente additiva** su dati che contengono già tutto il necessario (`ADD COLUMN` + `UPDATE ... geography::Point(Lat, Lon, 4326)` + indice). Nessuna perdita di dati, lat/lon restano fonte di verità e fallback.
- `geography → decimal` è una rimozione con rischio concreto di perdita dati.

**Di fronte a incertezza si sceglie la direzione reversibile.** E l'incertezza qui è reale e concentrata sulla decisione #3: finché non si sa quale provider geo si userà e **quali dati geometrici restituirà**, scegliere oggi il tipo spaziale significa decidere in assenza dell'informazione che conta.

Argomenti di supporto:

- **Scala.** Con `WHERE OwnerId = @me` la cardinalità è già ridotta a poche migliaia di righe. Un bounding box su indice composito è un index seek; l'indice spaziale non darebbe vantaggio misurabile e avrebbe un costo fisso di tessellation e storage. Sarebbe ottimizzazione prematura da manuale.
- **Ordine delle decisioni.** B non pregiudica la #6 (@argus) né la #3 (@archimedes).
- **Rischio di bug silenziosi.** Si evitano nella fase iniziale: SRID 0 che fa restituire `NULL` a `STDistance` senza errore, left-hand rule sui poligoni, e la forma-di-query KNN che EF può rompere silenziosamente con un `Include` o uno `Skip`.
- **Sblocco immediato.** `DATA.md` bloccava ogni migration su questa decisione.

### Precisione: perché lo 0,5% non conta qui

Haversine assume una sfera e sbaglia fino a ~0,5% rispetto al modello ellissoidale. È irrilevante nel contesto Roamly perché la distanza in linea d'aria è già uno stimatore grossolano della distanza stradale (fattore reale tipicamente 1,2-1,4×) e `FEATURES.md` §3 dichiara il Trip Planner v1 come **stima esplicita**. Ottimizzare lo 0,5% dentro un errore strutturale del 30% non ha senso ingegneristico.

---

## Conseguenze

### Positive

- Nessun blocco alla scrittura di codice e di migration.
- Migration semplici: finché vale questo ADR, **nessuna migration contiene SQL manuale**, quindi `dotnet ef migrations bundle` copre il 100% dei casi.
- Decisione #6 (test) non pregiudicata.
- Test di distanza e raggio sono **unit test puri, senza database**: veloci.
- Seed data di luoghi più semplici: coppie lat/lon, non geometrie.
- Costo di inversione basso e unidirezionale.

### Negative — accettate consapevolmente

1. **Si scrive codice geografico a mano che il database saprebbe fare meglio**: Haversine, bounding box con correzione per latitudine, e i relativi test. È reinvenzione della ruota.
2. **Si sposta lavoro, non lo si elimina.** Ci sarà una migration aggiuntiva con backfill e indice spaziale manuale. La scommessa è che farlo più tardi e meglio informati costi meno.
3. **Precisione inferiore** (~0,5%). Se un requisito futuro richiedesse distanze in linea d'aria certificate, questa scelta va rivista immediatamente.
4. **Bug latenti all'antimeridiano (±180°) e ai poli**: non verranno gestiti. Accettabile per un prodotto europeo, ma è un limite non testato, non un limite inesistente.
5. **L'indice composito `(OwnerId, Lat, Lon)` non è un indice 2D**: fa seek sulla latitudine e residual filter sulla longitudine. Invisibile a migliaia di righe, visibile a milioni.
6. **Il tradeoff SQL Server vs PostGIS resta, e resta sfavorevole.** Questo ADR non lo risolve, lo rende meno urgente. Se Roamly evolvesse verso clustering di marker, vector tiles o analisi geospaziale seria, SQL Server sarà un vincolo che richiederà lavoro applicativo che PostGIS avrebbe assorbito. **È una scelta consapevolmente subottimale sul piano geospaziale puro, giustificata da integrazione .NET, competenze esistenti e velocità di delivery per un singolo sviluppatore.**
7. **Duplicazione permanente dopo l'upgrade**: lat/lon e `geography` coesisteranno, con lat/lon come fonte di verità. Ridondanza deliberata, con un costo di consistenza da presidiare tramite un unico punto di scrittura.

---

## Trigger di revisione

Al verificarsi di **uno solo** di questi, si passa all'opzione A senza rinegoziare l'ADR:

1. La decisione #3 seleziona un provider che restituisce **poligoni** (ZTL, aree di restrizione, zone di sosta) da **persistire e interrogare**. → *Il più probabile, atteso in Phase 3.*
2. Una tabella geografica supera l'ordine di **~50.000 righe per utente**, oppure si introduce una **ricerca cross-utente** su luoghi pubblici/condivisi (Phase 6).
3. Una query di prossimità **misurata** supera **~200 ms** con dati reali. Misurata, non ipotizzata.
4. Serve **contenimento punto-in-poligono** in qualunque forma.

**Nota di sequenza:** la decisione #3 dovrebbe essere presa **prima** dell'attivazione del trigger 1, non dopo.

### Percorso di upgrade pre-approvato

```sql
ALTER TABLE Places ADD Location geography NULL;
UPDATE Places SET Location = geography::Point(Latitude, Longitude, 4326);
CREATE SPATIAL INDEX IX_Places_Location ON Places(Location);
```

Con, lato EF: installazione del pacchetto spaziale, `UseNetTopologySuite()`, SRID **esplicito** a 4326 su ogni `Point` costruito, e `migrationBuilder.Sql` per l'indice con `Down()` testato prima del deploy.

---

## Riferimenti

Documenti aggiornati da questo ADR:

- `docs/architecture/DATA.md` §1, §2, §2.1, §3
- `docs/architecture/TESTING.md` §2, §4, §5
- `docs/architecture/DEVOPS.md` §2.1, §2.2, §3
- `docs/OPEN-DECISIONS.md` — decisione #1 chiusa
