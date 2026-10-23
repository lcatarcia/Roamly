# Mappa decisionale — completamento prima della pubblicazione

Aggiornata: 2026-10-22

## Perimetro approvato

- Per la pubblicazione generale, completare le Phase 2-5 di [`ROADMAP.md`](ROADMAP.md). Il 2026-10-22 l'utente ha approvato una deroga limitata per preparare un alpha privato con scope fino alla Phase 2, senza attendere le Phase 3-5; questa scelta non autorizza provisioning o rilascio. Le fondamenta Azure già presenti in `rg-roamly-dev` precedono questa mappa e non costituiscono un'autorizzazione a estenderle.
- Validare ogni fase localmente/in CI e con Janus prima di iniziare la successiva.
- La Community (Phase 6) resta esclusa dal perimetro attuale.
- Questa mappa non autorizza provisioning, invio di dati a terzi o rilascio.

## #1: Sorgente delle stime di percorso del Trip Planner

Bloccato da: Nessuno; il CRUD Trip può procedere mentre si risolve la stima<br>
Tipo: Research<br>
Severità: MEDIUM<br>
Stato: Resolved

### Domanda

Come ottenere distanza e durata indicative per il Trip Planner Phase 2, che non è vehicle-aware?

### Opzioni

1. Inserimento manuale di distanza e durata
   - Pro: nessun provider, costo o trasferimento dati.
   - Contro: nessun calcolo automatico.
   - Impatto: l'itinerario è registrato, ma la stima dipende dall'utente.
   - Complessità: bassa.
2. API di routing stradale esterna — RACCOMANDATA solo dopo uno spike su costi, licenza e limiti
   - Pro: stime stradali automatiche senza gestire un motore cartografico.
   - Contro: dipendenza, possibili costi e trasferimento di origine/destinazione.
   - Impatto: richiede un provider e un confine d'integrazione.
   - Complessità: media.
3. Motore di routing open data autogestito
   - Pro: controllo sui dati e nessun costo per singola chiamata a un provider.
   - Contro: hosting, aggiornamento mappe e operatività a carico dell'app.
   - Impatto: introduce infrastruttura.
   - Complessità: alta.
4. Distanza in linea d'aria
   - Pro: calcolo semplice ed economico, se sono disponibili coordinate.
   - Contro: non rappresenta la distanza stradale e può produrre stime fuorvianti.
   - Impatto: deve essere etichettata esplicitamente come approssimazione.
   - Complessità: bassa.
5. Altro / proposta custom

### Risposta

Decisione dell'utente (2026-10-09): inserire manualmente distanza e durata **totali del viaggio**. Entrambi i valori sono indipendenti e facoltativi; la UI li etichetta come stime manuali. Non si interroga un provider, non si calcola un percorso e le stime non vengono ricalcolate quando cambiano le tappe. I campi persistiti su `Trip` sono `EstimatedDistanceKm decimal(18,2)?` e `EstimatedDurationMinutes int?`.

### Asset

- [`FEATURES.md` §3](FEATURES.md)
- Decisione provider geografico #3 in [`OPEN-DECISIONS.md`](../OPEN-DECISIONS.md)

## #2: Provider per Maps & Places

Bloccato da: Decisione esistente #3<br>
Tipo: Research<br>
Severità: HIGH<br>
Stato: Open

### Domanda

Quale fonte e provider sono sostenibili per mappe, geocodifica, ricerca luoghi e routing, rispettando costi, licenze e privacy?

### Opzioni

1. Differire la Phase 3 e proseguire con dati inseriti dall'utente.
2. Usare un provider esterno dopo uno spike su costi, licenza, limiti e trattamento dati.
3. Gestire dati geografici open data e motore proprio, accettandone l'onere operativo.
4. Approccio ibrido, con fallback manuale e provider sostituibile.
5. Altro / proposta custom.

### Risposta

Scelta del percorso di ricerca (2026-10-13): l'utente approva la valutazione di provider esterni tramite spike, con verifica di costi, licenze, trattamento dati e routing camper. La ricerca documentale preliminare è completata; propone HERE Routing v8 come primo candidato tecnico per routing camper, con PTV condizionato a specifiche/offerta, e separa mappe/search dal routing. L'utente ha poi scelto di proseguire solo con analisi documentale/OpenAPI, senza selezionare un provider. La specifica ufficiale HERE richiede autenticazione Bearer o API key; non è stata effettuata alcuna chiamata né richiesta o trasmessa credenziale. **Il provider finale resta da decidere** e la decisione canonica #3 in [`OPEN-DECISIONS.md`](../OPEN-DECISIONS.md) resta aperta fino all'ADR, prima della Phase 3.

### Asset

- [`FEATURES.md` §4](FEATURES.md)
- [`ROADMAP.md` — Phase 3](ROADMAP.md)
- [Ricerca preliminare provider geo](GEO-PROVIDER-RESEARCH.md)
- [Ricerca Azure Maps: quote, costi e vincoli](AZURE-MAPS-RESEARCH.md)

#### Aggiornamento ricerca Azure Maps (2026-10-22)

L'utente ha chiesto di valutare una possibile integrazione Azure Maps puntando a restare nelle quote mensili gratuite. La ricerca documentale rileva quote Gen2 pubblicate di 5.000 transazioni/mese per Base Map Tiles (15 tile per transazione), 5.000 per Search (una transazione per richiesta ordinaria; 10 richieste autocomplete per transazione) e 1.000 per Routing (una transazione per richiesta ordinaria). Le quote non sono un tetto di spesa: budget e alert Azure non bloccano il consumo. Azure Maps non è ancora selezionato; scope, spesa massima accettabile, autenticazione e autorizzazione a chiamate live restano da decidere. Nessuna risorsa è stata creata né alcuna chiamata eseguita.

Decisione dell'utente (2026-10-22): iniziare con un prototipo locale/mock del flusso mappa. Non creare risorse, chiavi o account Azure e non effettuare chiamate live; nessuna coordinata reale viene usata. Il prototipo serve solo a confrontare interazioni e layout, non misura costi, consumo tile o qualità del servizio.

Decisioni successive dell'utente (2026-10-22): alla proposta di avviare provisioning e chiamate, autorizzare per ora solo codice/configurazione locale, senza risorse né chiamate; poi mantenere il prototipo mock esistente come unico scaffolding, senza seam production-facing. La decisione provider #3 resta aperta. Non sono autorizzati né eseguiti provisioning, credenziali, chiamate Azure, endpoint o adapter production-facing.

Aggiornamento successivo (2026-10-22): l'utente ha chiesto di preparare il deploy Roamly e il percorso Azure Maps. Per il deploy ha selezionato ACR Basic con push OIDC e pull tramite managed identity, SQL Authentication runtime tramite secret Key Vault referenziato da ACA, e due identità OIDC distinte per bootstrap infrastruttura e release. Ha inoltre confermato che il secret SQL runtime conterrà la connection string completa richiesta dall'API. È stata aggiunta solo configurazione locale Bicep; durante questa preparazione non sono state create ulteriori risorse né eseguiti deploy e le fondamenta Azure già presenti non sono state modificate. La Phase 3 resta differita e la decisione provider #3 aperta. L'utente ha approvato una deroga al gate di pubblicazione generale per preparare un alpha privato con scope fino alla Phase 2 e ha scelto un massimo iniziale di 3 account totali, incluso il titolare, nominativi e provisionati manualmente; la registrazione Production resta chiusa. I dati di camper, viaggio e Journal saranno sintetici per tutti; è ammessa un'email reale solo come identificativo di login. Gli account alpha dovranno cambiare password al primo accesso tramite un flag persistito su `ApplicationUser`, con backfill degli account già provisionati. Le password temporanee saranno casuali e univoche, condivise tramite password manager sicuro; in caso di smarrimento della password definitiva resta previsto un recupero amministrativo fuori banda. Test e rollback restano da definire; la deroga non autorizza provisioning o rilascio. Azure Maps resta mock-only: nessuna risorsa Maps o chiamata live è stata autorizzata/eseguita.

## #3: Foto e documenti di viaggio

Collegata alla decisione #8, ora chiusa in [`ADR-0010`](../adr/0010-journal-image-storage.md)<br>
Tipo: Discuss<br>
Severità: MEDIUM<br>
Stato: Resolved

### Domanda

Le foto sono supportate dal Journal Phase 2; i documenti del camper restano Phase 4.

### Opzioni

1. Rinviare file e foto; mantenere note e contenuti testuali.
2. Usare object storage gestito, con policy di accesso e cancellazione.
3. Salvare i binari in SQL.
4. Salvare i file nel filesystem dell'app.
5. Altro / proposta custom.

### Decisione (2026-10-09)

Le immagini appartengono a `JournalEntry` tramite `JournalEntryAttachment`; storage Azure Blob Standard LRS Hot in Italy North, Azurite in locale/test. Limiti: JPEG/PNG/WebP, 10 MiB per file, 10 per voce. Upload mediato dall'API, container privato, accesso via managed identity, export ZIP e cancellazione owner-prefix. Gli originali conservano EXIF (incluse eventuali coordinate GPS) e non è prevista scansione antimalware nell'uso personale attuale. Nessun provisioning Azure è autorizzato o effettuato. Vedi [`ADR-0010`](../adr/0010-journal-image-storage.md).

### Asset

- [`FEATURES.md` §§2, 7](FEATURES.md)
- [`SECURITY.md` §5](../architecture/SECURITY.md)

## #4: Suggerimenti e confine dell'Intelligence

Bloccato da: Dati utili di Phase 2-3<br>
Tipo: Discuss<br>
Severità: HIGH<br>
Stato: Resolved

### Domanda

Quali suggerimenti devono essere locali e deterministici, e quali — se ce ne sono — possono usare un provider AI esterno?

### Opzioni

1. Solo regole deterministiche eseguite localmente — RACCOMANDATA per costo e privacy.
2. AI esterna opzionale, con provider, costi, informativa e consenso definiti prima dell'invio.
3. Approccio ibrido: risultati locali sempre disponibili, AI solo dietro flag e consenso.
4. Rinviare l'AI e implementare solo le funzioni non-AI di Intelligence.
5. Altro / proposta custom.

### Risposta

Decisione dell'utente (2026-10-21): AI esterna **opzionale**. Per ogni richiesta, l'utente sceglie e conferma il contesto minimo da inviare; documenti, foto e posizione precisa sono esclusi. Il modello può generare suggerimenti e bozze non vincolanti, ma non scrive dati né esegue azioni di dominio. Roamly resta autorevole per dati, vincoli, calcoli, validazioni e persistenza.

La selezione del provider è registrata nella #7 e non costituisce consenso al trasferimento effettivo. Prima della prima chiamata vanno definiti gli accordi, il costo per richiesta, la latenza attesa, il comportamento in caso di errore, i campi esatti trasferiti, il consenso versionato e la valutazione dei trasferimenti. Il feature flag non sostituisce il consenso.

### Asset

- [`FEATURES.md` §8](FEATURES.md)
- [`ROADMAP.md` — Phase 4](ROADMAP.md)
- [`ADR-0004`](../adr/0004-privacy-and-erasure.md)

## #5: Trasparenza della Personal Memory

Bloccato da: Dati di viaggio e decisione #4<br>
Tipo: Discuss<br>
Severità: HIGH<br>
Stato: Resolved

### Domanda

Quale memoria inferita l'utente deve poter vedere, correggere, esportare e cancellare?

### Opzioni

1. Nessuna inferenza: solo preferenze inserite manualmente.
2. Profilo inferito esplicito, modificabile e cancellabile, senza invio a terzi — RACCOMANDATA per il primo rilascio della feature.
3. Profilo inferito usato per raccomandazioni automatizzate, con controlli utente.
4. Rinviare la Personal Memory finché i dati di viaggio e l'UX di controllo non siano sufficienti.
5. Altro / proposta custom.

### Risposta

Decisione dell'utente (2026-10-21): mantenere un profilo inferito esplicito, privato a Roamly, visibile e modificabile voce per voce, esportabile e cancellabile. Usarlo solo in funzioni richieste esplicitamente dall'utente; non trasmetterlo automaticamente a terzi. Un'eventuale richiesta a un provider AI resta soggetta ai limiti della decisione #4, alla scelta puntuale del contesto e ai prerequisiti di consenso di [`ADR-0004`](../adr/0004-privacy-and-erasure.md).

### Asset

- [`FEATURES.md` §9](FEATURES.md)
- [`ROADMAP.md` — Phase 5](ROADMAP.md)
- [`SECURITY.md`](../architecture/SECURITY.md)

## #6: Offline/PWA

Bloccato da: Decisione esistente #10<br>
Tipo: Discuss<br>
Severità: MEDIUM<br>
Stato: Resolved

### Domanda

Quali funzioni devono essere disponibili senza rete, se ce ne sono, al completamento delle Phase 2-5?

### Opzioni

1. Nessuna modalità offline; messaggio esplicito quando manca la connessione.
2. Consultazione in sola lettura dei viaggi già caricati.
3. Modifiche offline con sincronizzazione e gestione dei conflitti.
4. PWA installabile senza promessa di modifiche offline.
5. Altro / proposta custom.

### Risposta

Decisione dell'utente (2026-10-21): nessuna modalità offline, PWA installabile o cache applicativa di dati utente nella v1; mostrare uno stato esplicito quando manca la rete. Una futura PWA o persistenza dei dati richiede una decisione di scope separata e, se si memorizzano dati personali sul dispositivo, policy di purge e informativa esplicita.

### Asset

- [`UX.md` §5](UX.md)
- [`OPEN-DECISIONS.md` #10](../OPEN-DECISIONS.md)

## #7: Candidato provider per AI esterna

Bloccato da: Nessuno per la due diligence; ogni invio resta bloccato dai prerequisiti di [`ADR-0004`](../adr/0004-privacy-and-erasure.md)<br>
Tipo: Research<br>
Severità: HIGH<br>
Stato: Resolved

### Domanda

Quale servizio portare alla verifica contrattuale e tecnica successiva, dopo aver definito il confine AI in #4? La scelta qui è solo del candidato per la due diligence: non seleziona un provider per l'applicazione e non autorizza account, provisioning, chiamate API o trasferimenti.

### Opzioni

1. **Azure OpenAI / Microsoft Foundry — Data Zone EU** — RACCOMANDATA per la prima due diligence
   - Motivo: riusa il cloud già presente e documenta elaborazione entro l'UE.
   - Pro: confine UE e un solo ambiente cloud operativo.
   - Contro: Data Zone non è una singola regione; disponibilità modello/quota, abuso-monitoring e costi vanno verificati.
   - Impatto: nuova superficie Azure AI e relativa configurazione/identità.
   - Complessità: media.
2. **OpenAI API diretta — endpoint EU**
   - Pro: listino pubblico per token e documentazione esplicita su endpoint e controlli di retention.
   - Contro: l'idoneità EU/ZDR/MAM dipende da approvazione, modello ed endpoint; restano eccezioni.
   - Impatto: nuovo processor e gestione di credenziale/retention distinta dall'infrastruttura Azure.
   - Complessità: medio-alta.
3. **Google Cloud Gemini Enterprise Agent Platform — endpoint EU**
   - Pro: endpoint multi-regione UE e pricing pubblico per modello/regione.
   - Contro: l'impegno geografico varia per modello/capacità; logging, caching e API stateful richiedono configurazione attenta.
   - Impatto: aggiunge progetto, identità e billing su un secondo cloud.
   - Complessità: alta.
4. **Mistral API diretta**
   - Pro: provider europeo e prezzi pubblici per alcuni modelli.
   - Contro: la documentazione consultata non garantisce il processing UE della API diretta; il DPA prevede opt-out dal training e ZDR separato.
   - Impatto: nuovo processor; servono conferme contrattuali prima di valutarne l'adozione.
   - Complessità: medio-alta, con incertezza contrattuale.
5. **Rinviare il candidato e mantenere l'AI disattivata**
   - Pro: nessun nuovo processor o trasferimento mentre si chiariscono requisiti e termini.
   - Contro: nessuna capacità basata su AI esterna nel frattempo.
   - Impatto: l'Intelligence resta limitata alle funzioni non-AI.
   - Complessità: bassa.
6. Altro / proposta custom.

### Risposta

Verifica documentale completata il 2026-10-21. Decisione dell'utente (2026-10-21): adottare **Google Cloud Gemini Enterprise Agent Platform su endpoint UE** come provider di riferimento per la futura configurazione; la configurazione è rimandata. La selezione non autorizza provisioning, integrazione, chiamate API o invio di dati.

La due diligence pubblica documenta endpoint UE, modelli candidati con Structured Output, costi/quote e i principali trattamenti. Il modello non è scelto e i termini effettivi dipendono da contratto, categoria del modello e configurazione. La documentazione pubblica non prova che supporto, telemetria e ogni sub-responsabile restino esclusivamente nell'UE; la soglia di logging/retention accettabile è la decisione #8.

### Asset

- [Ricerca preliminare provider AI](AI-PROVIDER-RESEARCH.md)
- [Due diligence Gemini UE](GEMINI-PLATFORM-DUE-DILIGENCE.md)
- [`FEATURES.md` §8](FEATURES.md)
- [`SECURITY.md` §4](../architecture/SECURITY.md)
- [`ADR-0004`](../adr/0004-privacy-and-erasure.md)

## #8: Logging e retention per l'AI esterna

Bloccato da: Selezione del provider #7 (risolta); conferma delle condizioni per modello, capability, endpoint e contratto effettivi
Tipo: Discuss
Severità: HIGH
Stato: Resolved

### Domanda

Quale livello di conservazione di prompt/risposte e logging anti-abuso è accettabile prima di configurare Gemini per richieste che possono contenere dati personali?

### Contesto

La due diligence del 2026-10-21 rileva che, con i termini GCP online e modelli Google ordinari, prompt segnalati dai controlli anti-abuso possono essere registrati fino a 90 giorni; per i clienti con Cloud Master Agreement questo prompt logging è dichiarato esente per default. Modelli Frontier possono prevedere logging di prompt e risposte fino a 30 giorni; alcuni modelli Advanced AI possono richiederlo senza opt-out. La cache implicita può essere attiva per default fino a 24 ore. Sono comportamenti distinti e specifici per modello/contratto: nessuna singola opzione equivale a retention zero complessiva. L'endpoint UE non risolve la scelta.

### Opzioni

1. Accettare le condizioni predefinite per un modello Google ordinario, inclusi gli eventuali prompt sospetti conservati fino a 90 giorni
   - Pro: percorso più semplice, senza attendere un accordo negoziato.
   - Contro: retention e possibili revisioni umane non sono azzerate; l'idoneità dipende dal modello e dai termini attivi.
   - Impatto: richiede informativa, consenso e minimizzazione del contesto prima di ogni invio.
   - Complessità: bassa, rischio privacy più alto.
2. Procedere solo con Cloud Master Agreement o conferma scritta equivalente per il modello esatto, accettando le eccezioni di retention documentate
   - Pro: condizioni contrattuali più verificabili; il regime online indica un'esenzione predefinita dal prompt logging per i clienti Cloud Master Agreement.
   - Contro: non implica retention zero; le eccezioni per categoria/modello, cache, supporto e altri log vanno chiarite.
   - Impatto: richiede verifica del contratto effettivo e dei ruoli/accessi prima dell'uso.
   - Complessità: medio-alta.
3. Richiedere garanzia scritta di zero retention per prompt e risposte del modello/capability/endpoint scelti; non abilitarli se non ottenibile
   - Pro: massima minimizzazione del contenuto conservato.
   - Contro: disponibilità e ambito della garanzia possono escludere logging di sicurezza, cache o altri dati di sistema.
   - Impatto: l'AI resta disattivata fino a prova contrattuale e tecnica completa.
   - Complessità: alta.
4. Rinviare l'AI esterna finché una revisione privacy/legale non stabilisce una soglia di retention, mantenendo le funzioni deterministiche
   - Motivo raccomandazione: non assumere una tolleranza al rischio che l'utente non ha ancora espresso e non bloccare il prodotto non-AI.
   - Pro: nessun trasferimento o dipendenza dal trattamento Gemini nel frattempo.
   - Contro: la feature AI non è disponibile finché la soglia resta indefinita.
   - Impatto: nessuna configurazione o invio di dati; riaprire la decisione con esito della revisione.
   - Complessità: bassa.
5. Altro / proposta custom

### Risposta

Decisione dell'utente (2026-10-21): accettare il regime predefinito di abuse monitoring per un modello Google ordinario, inclusa l'eventuale conservazione fino a 90 giorni dei prompt segnalati, subordinatamente alla verifica che il modello GA, il contratto e i termini effettivi corrispondano a questo regime. La documentazione consultata non designa Gemini 3.1 Flash-Lite come Frontier o Advanced AI; la classificazione va riconfermata prima dell'uso. Non estendere questa scelta ai modelli Frontier/Advanced AI né interpretarla come accettazione della cache implicita, di altre retention o di revisioni non coperte. La decisione non autorizza provisioning, integrazione o invio di dati.

### Asset

- [Due diligence Gemini UE](GEMINI-PLATFORM-DUE-DILIGENCE.md)
- [`ADR-0004`](../adr/0004-privacy-and-erasure.md)
- [`FEATURES.md` §8](FEATURES.md)

## #9: Cache implicita per le richieste Gemini

Bloccato da: Decisioni #7-#8; verifica del controllo disponibile per il modello e l'endpoint effettivi
Tipo: Discuss
Severità: HIGH
Stato: Resolved

### Domanda

La documentazione indica che la cache implicita dei modelli Gemini supportati può essere attiva per default e conservare contenuti fino a 24 ore. Quale politica deve applicare Roamly prima di inviare il contesto selezionato dall'utente?

### Opzioni

1. Accettare la cache implicita predefinita fino a 24 ore per il contesto che l'utente ha scelto e confermato
   - Pro: nessuna configurazione aggiuntiva; può migliorare efficienza e costo.
   - Contro: il contenuto può essere conservato temporaneamente anche se la chiamata è stateless.
   - Impatto: informativa e consenso devono descrivere anche questa retention.
   - Complessità: bassa.
2. Disabilitare cache implicita in-memory e durevole prima di ogni uso con contesto utente; mantenere l'AI disattivata se il controllo non è disponibile o verificabile — RACCOMANDATA
   - Motivo raccomandazione: riduce la conservazione non necessaria e non presume che un endpoint UE o una chiamata stateless implichino cache assente.
   - Pro: minimizzazione più forte e comportamento più chiaro.
   - Contro: può aumentare latenza/costi; la disponibilità e l'efficacia del controllo vanno provate per modello/endpoint.
   - Impatto: requisito tecnico e gate di verifica prima di abilitare richieste.
   - Complessità: media.
3. Consentire la cache solo per richieste con contesto verificato come non personale e minimizzato
   - Pro: compromesso tra efficienza e minimizzazione.
   - Contro: classificare i dati e applicare la regola in modo affidabile aggiunge complessità; contesto di viaggio può comunque essere identificativo.
   - Impatto: richiede una policy dati e un enforcement applicativo esplicito.
   - Complessità: medio-alta.
4. Rinviare la decisione finché modello, impostazioni e contratto effettivi non siano verificati; nessun invio nel frattempo
   - Pro: evita di assumere un comportamento tecnico non ancora confermato.
   - Contro: mantiene l'AI esterna disattivata più a lungo.
   - Impatto: nessuna configurazione o trasmissione dati.
   - Complessità: bassa.
5. Altro / proposta custom

### Risposta

Decisione dell'utente (2026-10-21): disabilitare la cache implicita in-memory e durevole prima di qualsiasi uso con contesto utente; mantenere Gemini disattivato se il controllo non è disponibile o non è verificabile per il modello/endpoint effettivi. Google documenta `cacheConfig` per progetto con `disableCache: true`, efficace su tutte le regioni; il controllo non è stato applicato né verificato su un progetto Roamly. Questa decisione non autorizza configurazione o invio.

### Asset

- [Due diligence Gemini UE](GEMINI-PLATFORM-DUE-DILIGENCE.md)
- [`FEATURES.md` §8](FEATURES.md)

## #10: Candidato Gemini per una futura valutazione

Bloccato da: Decisioni #8-#9; verifica futura di modello, contratto e controllo cache
Tipo: Research
Severità: HIGH
Stato: Resolved

### Domanda

Quale modello UE con Structured Output deve essere il primo candidato per una futura valutazione di qualità, latenza e costo su prompt sintetici? La scelta è solo per una valutazione futura: non seleziona un modello di produzione e non autorizza account, provisioning o chiamate API.

### Opzioni

1. Gemini 3.1 Flash-Lite (`gemini-3.1-flash-lite`)
   - Pro: uno dei prezzi pubblicati più bassi tra i candidati esaminati ($0,275 input / $1,65 output per milione di token).
   - Contro: qualità sul caso d'uso Roamly e latenza reale non misurate.
   - Impatto: candidato economico per suggerimenti/bozze semplici.
   - Complessità: bassa per una valutazione sintetica.
2. Gemini 3.5 Flash-Lite (`gemini-3.5-flash-lite`)
   - Pro: la scheda lo orienta a task semplici, JSON ed esigenze latency-sensitive; supporta Structured Output sull'endpoint UE.
   - Contro: il listino consultato ha output più costoso del 3.1 Flash-Lite; nessuna misura Roamly.
   - Impatto: candidato leggero e più recente da confrontare.
   - Complessità: bassa per una valutazione sintetica.
3. Gemini 3.8 Flash (`gemini-3.8-flash`)
   - Pro: candidato generalista indicato come workhorse, con Structured Output UE.
   - Contro: costo superiore e listino pubblicato in aumento dal 01/01/2027; nessuna misura Roamly.
   - Impatto: candidato di qualità generale, da giustificare rispetto ai modelli Lite.
   - Complessità: bassa per una valutazione sintetica.
4. Gemini 4 Argon (`gemini-4-argon`)
   - Pro: capacità più ampia documentata e Structured Output UE.
   - Contro: costo unitario più alto tra gli esempi esaminati e nessuna evidenza che Roamly richieda tale capacità.
   - Impatto: valutare solo se i modelli leggeri/generalisti non raggiungono la qualità necessaria.
   - Complessità: bassa per una valutazione sintetica, costo potenziale più alto.
5. Rinviare il candidato fino a quando siano verificati contratto, categoria/retention e controllo cache; poi confrontare i candidati con prompt sintetici — RACCOMANDATA
   - Motivo raccomandazione: la scelta basata solo su schede e listini non misura qualità e latenza e non giustifica ancora un modello.
   - Pro: nessuna preferenza prematura; confronto misurabile senza dati personali.
   - Contro: la selezione del modello resta aperta e richiede una futura valutazione controllata.
   - Impatto: nessuna chiamata API né configurazione in questa fase.
   - Complessità: media per la futura valutazione.
6. Altro / proposta custom

### Risposta

Decisione dell'utente (2026-10-21): **Gemini 3.1 Flash-Lite** (`gemini-3.1-flash-lite`) è il primo candidato per una futura valutazione di qualità, latenza e costo con prompt sintetici. Non è una scelta del modello di produzione e non autorizza creazione di account/progetti, provisioning o chiamate API. Una valutazione futura richiede autorizzazione esplicita separata e non deve usare dati Roamly reali.

### Asset

- [Due diligence Gemini UE](GEMINI-PLATFORM-DUE-DILIGENCE.md)
- [Ricerca preliminare provider AI](AI-PROVIDER-RESEARCH.md)

## Prossimo passo raccomandato

La Phase 2 è chiusa. Personal Memory (#5) e Offline/PWA (#6) sono definite. Gemini resta un provider di riferimento per il futuro: qualsiasi valutazione richiede un'autorizzazione esplicita separata e l'eventuale uso reale richiede le verifiche contrattuali e tecniche descritte sopra. La decisione geo #3 resta aperta e blocca Maps & Places e l'avvio della Phase 3; la ricerca #2 non seleziona un provider. Nessuna decisione qui autorizza provisioning, chiamate API o invio di dati.
