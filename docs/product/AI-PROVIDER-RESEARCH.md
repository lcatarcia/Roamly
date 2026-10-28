# Ricerca preliminare sui provider AI per Roamly

**Verifica documentale:** 2026-10-21
**Stato:** shortlist completata; provider di riferimento scelto dall'utente per configurazione futura, ancora rimandata
**Perimetro:** fonti ufficiali pubbliche. Nessuna API di inferenza è stata chiamata e nessun dato Roamly o dell'utente è stato trasmesso.

## Confini già approvati

- L'AI esterna è facoltativa; l'utente sceglie e conferma il contesto minimo per ogni richiesta.
- Documenti, foto e posizione precisa sono esclusi.
- Il modello restituisce suggerimenti o bozze non vincolanti; non esegue azioni né scrive dati.
- Roamly resta autorevole per dati, vincoli, calcoli, validazioni e persistenza.

Questa ricerca non è un parere legale, una DPIA o una dichiarazione di conformità GDPR. I requisiti effettivi dipendono da modello, endpoint, regione, configurazione, contratto e sub-responsabili. La documentazione pubblica non sostituisce la verifica del DPA e dell'ordine applicabile.

## Confronto sintetico

| Servizio | Elaborazione UE | Training e retention | Costi | Fit e cautele |
|---|---|---|---|---|
| **Azure OpenAI / Microsoft Foundry — Data Zone EU** | Data Zone EU mantiene l'elaborazione entro gli Stati membri UE; non equivale a una singola regione o paese. La disponibilità dipende dal modello e dal deployment. | Microsoft dichiara che prompt e completamenti non sono usati per addestrare i modelli senza permesso. Resta l'abuse monitoring; il monitoraggio modificato richiede approvazione. Evitare API stateful e Files se non necessari. | Consumo per token, PTU o Batch; listino e prezzo effettivo dipendono da modello, SKU, deployment e area, da calcolare con la configurazione reale. | Candidato per la prima valutazione per l'infrastruttura Azure già presente. Da verificare disponibilità/quota, condizioni di abuse monitoring e offerta applicabile. |
| **OpenAI API diretta — endpoint EU** | Opzioni regionali EU per modelli ed endpoint eleggibili; l'idoneità non è universale e la residenza del contenuto non copre necessariamente i dati di sistema. | I dati API non sono usati per training salvo opt-in. Abuse logs possono contenere prompt e risposte e sono conservati fino a 30 giorni per default; ZDR/MAM richiedono approvazione e hanno eccezioni per endpoint e funzionalità. | Prezzi pubblici per token e modello; gli endpoint regionali e i controlli di retention possono avere condizioni o maggiorazioni. | Buon confronto diretto, ma verificare per iscritto eleggibilità EU/ZDR, endpoint e snapshot esatti prima della scelta. |
| **Google Cloud Gemini Enterprise Agent Platform — endpoint EU** | Gli endpoint multi-regione UE limitano il processing ML alla giurisdizione UE per i modelli/capacità supportati; l'endpoint globale non offre garanzia geografica. Storage a riposo e inferenza sono controlli distinti. | Google dichiara che i dati non sono usati per training senza permesso/istruzione. Logging, cache, grounding e API stateful hanno condizioni specifiche: serve configurare l'endpoint e le feature con attenzione. | Listino pubblico per modello e regione. Il prezzo di Gemini 4 Argon indicato per Global/Non-global cambia dal **1° dicembre 2026**; non usarlo come budget stabile senza ricontrollo. | Alternativa con un confine cloud diverso; richiede progetto, identità e billing Google Cloud. Verificare capacità e retention esatte del modello e dell'endpoint scelti. |
| **Mistral API diretta** | La sede francese e il DPA non dimostrano da soli che l'inferenza della API diretta avvenga nell'UE; nelle fonti consultate non è stata trovata una garanzia generale di processing UE. | Il DPA consente il training salvo opt-out. ZDR è distinto dall'opt-out, può richiedere approvazione e copre solo alcune API stateless. | Model card pubblica per Mistral Small 4: **$0,15 input / $0,60 output per milione di token** alla data della ricerca; prezzi e modelli possono cambiare. | Candidato per benchmark di costo solo dopo conferma contrattuale di processing/retention UE, opt-out e copertura ZDR. |
| **Anthropic Claude API diretta** | La documentazione API consultata espone inferenza `global` o `us`, non un'opzione EU-only; `global` può instradare in qualsiasi area geografica. | Per uso commerciale i dati non sono usati per training di default; retention standard e ZDR dipendono da endpoint, modello e approvazione. | Prezzo per modello e utilizzo; verificare listino e condizioni correnti. | Solo benchmark condizionale se il processing UE non è un requisito, oppure valutare un distinto percorso tramite un cloud partner con i suoi termini e processor. |

I prezzi pubblici non sono direttamente comparabili senza fissare modello, input/output, regione, volume e piano. Le pagine di pricing non stabiliscono un costo Roamly né una latenza applicabile; latenza e qualità richiedono un test successivo con dati sintetici, se autorizzato.

## Valutazione preliminare

**Valutazione preliminare:** Azure OpenAI Data Zone EU era il candidato raccomandato per la due diligence per l'infrastruttura già presente e il processing documentato entro l'UE. L'utente ha scelto Google Cloud Gemini Enterprise Agent Platform su endpoint UE come provider di riferimento per una futura configurazione; tale scelta prevale sulla raccomandazione comparativa e non autorizza configurazione o invii. Se serve una garanzia più ampia che includa storage, supporto e sub-responsabili esclusivamente UE, le fonti pubbliche non bastano a concludere che un candidato la soddisfi.

Per ogni candidato, prima di decidere: esaminare DPA e ordine applicabile, retention di abuse/telemetria, sub-responsabili e trasferimenti, endpoint stateless, copertura geografica effettiva, disponibilità e quote. Nessun failover silenzioso verso un provider o una geografia diversa.

## Fonti ufficiali consultate

- Microsoft: [data, privacy e security dei modelli venduti da Azure](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy); [pricing Azure OpenAI](https://azure.microsoft.com/en-us/pricing/details/azure-openai/).
- OpenAI: [data controls, retention e ZDR/MAM](https://developers.openai.com/api/docs/guides/your-data); [pricing API](https://developers.openai.com/api/docs/pricing).
- Google Cloud: [data residency e processing](https://docs.cloud.google.com/gemini-enterprise-agent-platform/resources/data-residency); [pricing](https://cloud.google.com/gemini-enterprise-agent-platform/generative-ai/pricing).
- Mistral: [DPA](https://legal.mistral.ai/terms/data-processing-addendum/); [model card Mistral Small 4](https://docs.mistral.ai/models/mistral-small-4-0-26-03); [ZDR](https://docs.mistral.ai/admin/monitor-comply/zero-data-retention).
- Anthropic: [data residency](https://platform.claude.com/docs/en/manage-claude/data-residency); [API data retention](https://platform.claude.com/docs/en/manage-claude/api-and-data-retention); [pricing](https://platform.claude.com/docs/en/about-claude/pricing).
