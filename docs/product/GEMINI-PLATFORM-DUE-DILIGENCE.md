# Due diligence documentale — Google Gemini per Roamly

**Verifica:** 2026-10-21
**Stato:** due diligence pubblica completata; configurazione rimandata
**Perimetro:** Gemini Enterprise Agent Platform / Vertex AI su endpoint UE. Nessun progetto o account creato, credenziale usata, API chiamata o dato Roamly trasmesso. Questo documento non è un parere legale, una DPIA o una dichiarazione di conformità GDPR.

## Fit tecnico

- L'endpoint multi-region `eu` documenta l'elaborazione ML entro gli Stati membri UE; UK e Svizzera sono fuori dal perimetro. L'endpoint globale non garantisce elaborazione UE. Storage a riposo e inferenza sono controlli distinti.
- La matrice ufficiale consultata elenca sull'endpoint UE modelli con Structured Output, tra cui Gemini 3.5 Flash-Lite (`gemini-3.5-flash-lite`), Gemini 3.8 Flash (`gemini-3.8-flash`) e Gemini 4 Argon (`gemini-4-argon`). Il modello non è stato scelto; disponibilità e capability vanno ricontrollate al momento della configurazione.
- Per una richiesta singola, `GenerateContent` con `responseSchema` può restituire JSON vincolato. Lo schema supportato è un sottoinsieme; l'app dovrà validare formato e semantica. Non servono function calling, agenti o strumenti di dominio.
- Il prezzo UE è per token e modello. Esempi di listino consultati: Gemini 3.5 Flash-Lite $0,33 input / $2,75 output per milione di token; Gemini 3.8 Flash $0,825 / $4,125 fino al 31/12/2026, poi $1,65 / $8,25 dal 01/01/2027; Gemini 4 Argon $2,20 / $11 fino al 30/11/2026, poi $4,40 / $22 dal 01/12/2026. Sono prezzi indicativi, soggetti a modifiche e non una stima dei costi Roamly.
- La capacità dell'endpoint UE usa un pool distinto: le soglie Standard PayGo pubblicate per l'endpoint globale non sono trasferibili all'UE. Quote effettive per progetto/modello vanno verificate; Standard PayGo non garantisce throughput e può restituire 429. Priority PayGo e Provisioned Throughput sono alternative con costi/condizioni ulteriori.

## Trattamento, retention e limiti

- Il Cloud Data Processing Addendum si applica secondo l'accordo Cloud effettivo; Google opera come processor nei casi previsti e dichiara che non usa i dati del cliente per training/fine-tuning senza permesso o istruzione. Questo non significa retention zero. La documentazione indica che i clienti con Cloud Master Agreement sono esenti per default dal prompt logging anti-abuso previsto per i modelli ordinari sotto i termini GCP online; l'accordo effettivo va verificato.
- Per modelli Google ordinari soggetti ai termini GCP online, prompt sospetti possono essere registrati per abuse monitoring fino a 90 giorni e potenzialmente revisionati da personale autorizzato. Modelli Frontier possono avere logging di prompt e risposte fino a 30 giorni; alcuni modelli Advanced AI possono richiederlo senza opt-out. La categoria del modello e il contratto applicabile vanno verificati prima di qualsiasi uso.
- Nella pagina Abuse Monitoring consultata, la sezione Frontier nomina Gemini 4 Argon (Cyber Permissive); la sezione Advanced AI elenca modelli partner selezionati. Gemini 3.1 Flash-Lite non è tra i modelli designati e ricade quindi nella categoria Google ordinaria secondo la documentazione consultata. Riconfermare la classificazione del modello/versione e i termini contrattuali al momento dell'eventuale configurazione.
- La cache implicita può essere attiva per default e avere durata fino a 24 ore; cache durevole e cache in-memory sono controlli distinti. Va verificato e configurato il comportamento richiesto prima di consentire dati personali.
- Google documenta un controllo `cacheConfig` per progetto che disabilita entrambe le cache (`disableCache: true`); la modifica si applica a tutte le regioni. La verifica della configurazione richiede un ruolo IAM di lettura Agent Platform, la modifica il ruolo amministrativo Agent Platform (`roles/aiplatform.admin`). Il controllo è documentato, ma non è stato applicato né verificato su un progetto Roamly.
- Per un flusso stateless sono da escludere Interactions API con `store=true`, request/response logging, file e vector store, agent/session persistenti, RAG e grounding. Google Search grounding può conservare query e contesto per 3 giorni; Maps grounding per 30 giorni.
- Il perimetro UE non dimostra che supporto, telemetria, dati di sistema e ogni sub-responsabile restino nell'UE. La lista dei sub-responsabili e i termini di data location dell'accordo effettivo vanno verificati.
- I servizi o modelli Pre-GA possono essere esclusi da CDPA e impegni di data location salvo accordo scritto: usare solo un modello/capability con condizioni applicabili confermate.

## Prerequisiti prima di una futura configurazione

1. Decidere il livello di retention e abuse monitoring accettabile per le richieste che contengono il contesto selezionato dall'utente.
2. Confermare contratto Cloud, DPA/CDPA applicabile, categoria GA del modello, retention e accessi/sub-responsabili per l'esatto modello, capability ed endpoint.
3. Scegliere modello e verificare quota, disponibilità, listino e capability UE correnti.
4. Definire controlli per cache, logging, consent e cancellazione; l'endpoint UE da solo non determina questi comportamenti.
5. Solo dopo le verifiche e un'autorizzazione esplicita separata, valutare progetto, billing, API, identità e rete. Nessuno di questi è stato configurato in questa ricerca.

## Fonti Google ufficiali

- [Deployment e endpoint](https://docs.cloud.google.com/gemini-enterprise-agent-platform/resources/locations) e [capability supportate/localizzate](https://docs.cloud.google.com/gemini-enterprise-agent-platform/resources/supported-capabilities).
- [Data residency](https://docs.cloud.google.com/gemini-enterprise-agent-platform/resources/data-residency) e [servizi con data location](https://cloud.google.com/terms/data-residency).
- [Structured Output](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/capabilities/control-generated-output) e [pricing](https://cloud.google.com/gemini-enterprise-agent-platform/generative-ai/pricing).
- [Quote](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/quotas), [Standard PayGo](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/standard-paygo) e [Priority PayGo](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/priority-paygo).
- [Cloud Data Processing Addendum](https://cloud.google.com/terms/data-processing-addendum), [Service Specific Terms](https://cloud.google.com/terms/service-terms) e [sub-processors](https://cloud.google.com/terms/subprocessors).
- [Abuse monitoring](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/abuse-monitoring), [Zero data retention](https://docs.cloud.google.com/gemini-enterprise-agent-platform/resources/zero-data-retention) e [context caching](https://docs.cloud.google.com/gemini-enterprise-agent-platform/models/context-cache/context-cache-overview).
