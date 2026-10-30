# Stima incrementale dei costi per pubblicare Roamly su Azure

Stato: **stima preliminare; nessun provisioning o rilascio autorizzato da questo documento**  
Consultazione prezzi: **2026-10-23**  
Area: **Italy North** · Resource group: `rg-roamly-dev`  
Valuta dei listini: **USD**, IVA, cambio e condizioni commerciali esclusi
Origine: documentazione Microsoft/Azure e inventario Azure in sola lettura

> **Snapshot storico:** questo documento fotografa prezzi e risorse al
> 2026-10-23, non è un inventario né una checklist live. Un successivo
> preflight read-only ha confermato che ACR Basic esiste e che non esiste
> ancora una Container App; le informazioni sottostanti non sono state
> aggiornate per riflettere tale stato.

## Sintesi

Roamly non è ancora pubblicato: nel resource group non risulta alcuna
Container App né un Azure Container Registry (ACR), e non esiste un workflow
di deploy attivo. Sono già presenti le fondamenta usate dalla soluzione:
ambiente Azure Container Apps (ACA), Azure SQL server e database, Key Vault,
Storage Account, Log Analytics e managed identity runtime. L’inventario è
stato letto in Azure in sola lettura il 2026-10-23; non sono stati letti valori
di secret.

Il costo incrementale più prevedibile per un alpha leggero è l’ACR Basic,
indicativamente **circa 5 USD per 30 giorni**, più lo storage oltre la quota
inclusa. Il meter restituito dalla Retail Prices API per Italy North non è
primario, quindi questo prezzo è un’indicazione da confermare nel Pricing
Calculator o nella fatturazione. Con ACA Consumption e `minReplicas: 0`, il
compute della Container App può restare entro i grant mensili gratuiti per un
uso alpha sporadico: ad esempio una replica da 0,25 vCPU e 0,5 GiB attiva
un’ora al giorno usa meno dei grant, se questi non sono già consumati. Non è
però corretto interpretare scale-to-zero come costo totale nullo: ACR, SQL,
storage, log, richieste, rete ed eventuali altre caratteristiche possono
continuare a generare costi.

Queste cifre sono scenari di consumo, **non un preventivo personalizzato né un
limite di spesa**. Prima di creare risorse o rilasciare l’app vanno ancora
chiusi i gate operativi e approvato un budget massimo.

## Stato Azure osservato

Inventario read-only del 2026-10-23 nel resource group `rg-roamly-dev`:

| Servizio | Stato rilevato | Rilevanza per la stima |
|---|---|---|
| Azure Container Apps | Ambiente gestito presente; nessuna Container App | Non esiste ancora un host pubblico dell’app |
| Azure Container Registry | Nessun registry presente | ACR Basic è un costo incrementale previsto |
| Azure SQL | Server e database presenti; database in pausa al controllo | Il consumo SQL causato dall’app non è incluso nei totali |
| Key Vault | Presente | Operazioni e accessi ai secret non inclusi |
| Storage Account | Presente, `Standard_LRS`, access tier Hot, accesso Blob pubblico disabilitato | Capacità, operazioni e traffico aggiuntivi non inclusi |
| Log Analytics | Workspace presente, retention 30 giorni, nessun limite giornaliero configurato | Ingestione di log aggiuntiva dipende dal volume |
| Managed identity runtime | Presente | Nessun costo diretto stimato per l’identità |
| GitHub Actions di deploy | Non attivo; documentazione locale di preparazione soltanto | Non è conteggiato alcun workflow operativo |

Il consuntivo degli ultimi 30 giorni (23/09–23/10/2026 UTC) è stato verificato
in sola lettura tramite Azure Cost Management Query API, con `ActualCost` e
aggregazione `PreTaxCost`/`Sum`, limitata a questo resource group. Il breakdown
per tipo di risorsa attribuisce i costi non nulli a SQL. Gli importi
account-specific non sono riportati in questo documento; il dato osservato è
uno snapshot dei consumi passati, non una previsione del costo dell’app
pubblicata né un limite di spesa. Microsoft precisa che i dati Cost Management
possono avere ritardo e cambiare prima della fattura; `PreTaxCost` esclude
crediti e imposte.

La configurazione SQL osservata è `GP_S_Gen5`, General Purpose Serverless,
minimo 0,5 e massimo 1 vCore, auto-pause dopo 15 minuti e dimensione massima
1 GiB. È infrastruttura già esistente: il suo costo di base non è quindi
attribuito alla pubblicazione, ma le richieste dell’app possono aumentare il
tempo di calcolo fatturato e il consumo di storage.

## Componenti e assunzioni

- La bozza `infra/container-app.bicep` prevede ACA Consumption, ingress HTTPS
  pubblico, `minReplicas: 0` e porta 8080.
- CPU, memoria e massimo di repliche sono parametri obbligatori senza default:
  non sono ancora scelte approvate dal template.
- L’architettura selezionata usa ACR Basic, con admin disabilitato; GitHub
  Actions dovrebbe pubblicare via OIDC e ACA scaricare l’immagine con la
  managed identity runtime.
- Il `Dockerfile` contiene API ASP.NET Core e frontend React/Vite nella stessa
  immagine.
- Gli esempi ACA utilizzano combinazioni di risorse documentate da Microsoft:
  0,25 vCPU / 0,5 GiB e 1 vCPU / 2 GiB. Sono ipotesi per misurare sensibilità
  di costo, non decisioni sul sizing Roamly.
- Per rendere confrontabili gli esempi, il mese è assunto di 30 giorni
  (2.592.000 secondi). I grant ACA sono mensili e condivisi con l’intera
  subscription; non è verificata la quota residua.

## Prezzi di riferimento e scenari

La documentazione Microsoft indica per ACA Consumption grant mensili per
subscription pari a 180.000 vCPU-secondi, 360.000 GiB-secondi e 2 milioni di
richieste. Oltre i grant, i prezzi Retail Prices API consultati per Italy
North sono $0,000034 per vCPU-secondo attivo, $0,000004 per GiB-secondo e
$0,40 per milione di richieste. La tariffa inattiva pubblicata è $0,000004 per
vCPU-secondo e per GiB-secondo. Quando la Container App è a zero repliche non
si addebita il compute dell’app; le richieste che arrivano possono risvegliarla.

| Scenario illustrativo | Compute e richieste ACA incrementali | ACR Basic indicativo | Totale incrementale indicativo |
|---|---:|---:|---:|
| App a zero repliche, nessun consumo durante il mese | $0 | circa $5 | circa $5 |
| Alpha sporadico: 0,25 vCPU / 0,5 GiB, 1 ora attiva al giorno | $0 con grant residui; circa $1,13 se i grant sono esauriti | circa $5 | circa $5; fino a circa $6,13 senza grant |
| Una replica 0,25 vCPU / 0,5 GiB attiva 24/7 | circa $19,66 dopo i grant; $27,22 prima dei grant | circa $5 | circa $24,66 dopo i grant; $32,22 prima dei grant |
| Stress illustrativo: 1 vCPU / 2 GiB attiva 24/7 e 3 milioni di richieste esterne | circa $101,70 dopo i grant | circa $5 | circa $106,70 |

I totali aggiungono soltanto il compute/richieste ACA e l’indicazione ACR
Basic: **non includono** SQL, Storage, Key Vault, Log Analytics, egress,
imposte o altre risorse. “Dopo i grant” presume che la quota gratuita sia
disponibile per l’app; il consumo di altri workload nella subscription può
ridurla. ACR Basic è indicato a circa $0,1666/giorno ($4,998 per 30 giorni),
ma il meter Italy North restituito dalla API ha
`isPrimaryMeterRegion: false`; non è una tariffa regionale confermata.

### Formule

Con `S = 2.592.000` secondi in 30 giorni, i costi ACA sono stimati così:

- **vCPU attiva:** `max(0, vCPU × S − 180.000) × 0,000034 USD`.
- **Memoria:** `max(0, GiB × S − 360.000) × 0,000004 USD`.
- **Richieste:** `max(0, richieste − 2.000.000) / 1.000.000 × 0,40 USD`.

Per 0,25 vCPU e 0,5 GiB attive un’ora al giorno si ottengono 27.000
vCPU-secondi e 54.000 GiB-secondi: entrambi inferiori ai rispettivi grant.
Senza grant residui, il medesimo scenario costa circa
`27.000 × 0,000034 + 54.000 × 0,000004 = 1,134 USD`.

Per una replica da 0,25 vCPU e 0,5 GiB attiva tutto il mese, dopo i grant:
`(0,25 × S − 180.000) × 0,000034 = 15,912 USD` per CPU e
`(0,5 × S − 360.000) × 0,000004 = 3,744 USD` per memoria, per un totale di
circa $19,66. Prima dei grant il compute è circa $27,22.

## Costi non inclusi e incertezze

- **Azure SQL:** già presente e serverless; il database era in pausa al
  controllo. La pubblicazione aggiunge query e può prolungare il tempo di
  calcolo dopo il resume. Senza una traccia d’uso realistica e un prezzo
  regionale applicabile non viene stimato un delta SQL. Storage dati e log
  SQL possono anch’essi incidere.
- **Log Analytics:** la Retail Prices API consultata restituisce $2,99/GB per
  Analytics Logs oltre il tier minimo indicato di 5 GB. La documentazione
  Azure Monitor descrive i primi 5 GB mensili gratuiti a livello di billing
  account; non è verificato se tale grant sia già consumato. Il workspace
  esistente non ha un daily cap configurato.
- **Azure Blob Storage e Key Vault:** account e vault esistono già; volume,
  operazioni, accessi e traffico prodotti dall’app sono ignoti e non stimati.
- **Traffico di rete:** Microsoft indica 100 GB/mese di trasferimento dati
  Internet in uscita gratuiti; oltre tale soglia il costo dipende dalla
  destinazione e dal routing. La stima non presume che il grant sia ancora
  disponibile.
- **Ambiente ACA:** non sono stati aggiunti eventuali meter di gestione o
  funzionalità speciali al totale. La fatturazione dell’ambiente dipende dalle
  caratteristiche effettivamente abilitate; va confrontata con la
  configurazione e fattura già esistenti prima di attribuire un delta.
- **Cambio e condizioni commerciali:** importi in USD, senza cambio in EUR,
  IVA, sconti, crediti, supporto o accordi individuali. La Retail Prices API
  è una fonte pubblica di prezzi retail, non un preventivo legato alla
  subscription.
- **Budget e alert:** un budget Azure invia notifiche, ma non interrompe
  automaticamente il consumo né impedisce che la fattura superi la soglia.

Prima di autorizzare il deploy è quindi necessario confrontare i prezzi
regionali/SKU nel Pricing Calculator o nel portale di fatturazione, confermare
la quota gratuita residua e approvare un limite mensile esplicito. In
particolare, il prezzo ACR non è confermato come meter primario Italy North.

## Gate prima del rilascio

Questa stima **non autorizza** provisioning, configurazione dei secret o
pubblicazione. Prima di poter ottenere un URL pubblico restano da chiudere e
approvare almeno:

1. budget massimo e verifica del `what-if` contro gli ID delle risorse esistenti;
2. ACR Basic, identità OIDC distinte, ruoli RBAC minimi e GitHub Environments
   protetti;
3. utente SQL runtime, secret Key Vault e autorizzazione secret-scoped della
   managed identity, senza esporre valori nei log o in GitHub;
4. ordine, backup, esecutore e rollback delle migration; l’API e il workflow
   di release non devono applicarle all’avvio o automaticamente;
5. approvazione di CPU, memoria, massimo repliche e probe, considerando il
   resume SQL;
6. approvazione dell’ingress HTTPS pubblico, della chiusura della registrazione
   e dei controlli operativi per l’alpha;
7. revisione finale di sicurezza e readiness prima di invitare utenti.

## Fonti primarie

Consultate il 2026-10-23; i prezzi possono cambiare e i valori regionali vanno
ricontrollati al momento del rilascio.

- [Azure Container Apps — billing](https://learn.microsoft.com/en-us/azure/container-apps/billing): grant mensili, fatturazione di compute, richieste, scale-to-zero e condizioni di idle.
- [Azure Container Apps — container resource allocation](https://learn.microsoft.com/en-us/azure/container-apps/containers): combinazioni ammesse di CPU e memoria.
- [Azure Retail Prices API — documentazione](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices): query, prezzi retail e interpretazione dei meter regionali.
- [Retail Prices API — ACA, Italy North](https://prices.azure.com/api/retail/prices?api-version=2023-01-01-preview&%24filter=serviceName%20eq%20%27Azure%20Container%20Apps%27%20and%20armRegionName%20eq%20%27italynorth%27%20and%20priceType%20eq%20%27Consumption): meter ACA e prezzi consultati.
- [Retail Prices API — Container Registry, Italy North](https://prices.azure.com/api/retail/prices?api-version=2023-01-01-preview&%24filter=serviceName%20eq%20%27Container%20Registry%27%20and%20armRegionName%20eq%20%27italynorth%27%20and%20priceType%20eq%20%27Consumption): meter ACR, incluso il flag di regione non primaria.
- [Azure Container Registry — SKU e limiti](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-skus): capacità e storage inclusi per SKU.
- [Azure Monitor pricing](https://azure.microsoft.com/en-us/pricing/details/monitor/): ingestione e allowance di Log Analytics.
- [Bandwidth pricing](https://azure.microsoft.com/en-us/pricing/details/bandwidth/): allowance e tariffe di trasferimento dati.
- [Cost Management Query API](https://learn.microsoft.com/en-us/rest/api/cost-management/query/usage?view=rest-cost-management-2025-03-01): query e aggregazioni dei costi.
- [Comprendere i dati Cost Management](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/understand-cost-mgt-data): ritardo e variazioni dei dati prima della fattura.
- [Campi dei dettagli di utilizzo](https://learn.microsoft.com/en-us/azure/cost-management-billing/automate/understand-usage-details-fields): significato di `PreTaxCost`.

## Riferimenti Roamly

- [`DEVOPS.md`](../architecture/DEVOPS.md), stato operativo e prerequisiti di release.
- [`infra/main.bicep`](../../infra/main.bicep), template delle fondamenta Azure e ACR.
- [`infra/container-app.bicep`](../../infra/container-app.bicep), bozza della Container App.
- [`infra/workflows/README.md`](../../infra/workflows/README.md), gate per bootstrap e release OIDC.
- [`DECISION-MAP-PRE-PUBBLICAZIONE.md`](DECISION-MAP-PRE-PUBBLICAZIONE.md), perimetro alpha e autorizzazioni.
