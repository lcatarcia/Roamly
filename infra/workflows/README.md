# Bozze locali dei workflow OIDC

**Non sono workflow GitHub Actions attivi.** In questa directory non ci sono file
installati sotto `.github/workflows/`; non si attivano su push, PR, schedule o
dispatch e non eseguono autenticazione, provisioning, build o deployment.

La creazione dei workflow eseguibili resta bloccata: i requisiti HIGH elencati in
[`../../docs/architecture/DEVOPS.md`](../../docs/architecture/DEVOPS.md), nella
sezione "Stato operativo: preparazione locale release",
non sono tutti decisi. Non spostare file in `.github/workflows/` prima di averli
chiusi e approvati.

## Separazione da mantenere

1. **Bootstrap infrastruttura**, manuale e OIDC con principal dedicato:
   validazione/what-if e poi provisioning Bicep solo dopo approvazione. Non deve
   essere usato per pubblicare immagini o aggiornare l'applicazione.
2. **Release applicativa**, manuale e OIDC con un secondo principal: build dal
   `Dockerfile`, push all'ACR con tag immutabile e aggiornamento della Container
   App dal template `../container-app.bicep`. Non deve leggere segreti Key Vault
   né applicare migration o creare il principal SQL.

Ogni job futuro che possa modificare Azure dovrà legare il deployment a un
GitHub Environment protetto, configurato fuori dal repository con approvazione
manuale. Il nome dell'environment non è stato scelto; non viene assunto qui.
La sola presenza del nome nel YAML non garantisce che l'environment abbia
protezioni abilitate.

## Configurazione GitHub ammessa

Gli identificativi OIDC non sono password e, una volta approvati i nomi degli
environment, vanno passati come GitHub Environment Variables:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Bootstrap e release devono ricevere client ID distinti. Il repository non deve
contenere client secret, PAT, password SQL o valori dei secret Key Vault; GitHub
non deve ricevere alcun valore di secret. I workflow futuri useranno solo
federazione OIDC e permessi `contents: read` + `id-token: write` strettamente
necessari.

## Gate prima di rendere attivabili i workflow

- Scegliere e configurare i due GitHub Environments protetti, i reviewer e le
  regole di branch/ref; poi associare a ciascuno la federated credential esatta.
- Decidere ruoli Azure e scope minimi separatamente per bootstrap e release.
  Nessun ruolo a subscription/resource group viene dedotto o assegnato da questa
  bozza.
- Risolvere la gestione della password SQL amministrativa: `main.bicep` richiede
  un parametro `@secure()` e non si deve trasferire tale valore a GitHub.
- Definire bootstrap del contained SQL user e del secret Key Vault runtime con
  la connection string completa approvata, senza passare la password attraverso
  log, argomenti o GitHub.
- Definire ordine, esecutore, approvazione, backup e rollback delle migration.
  Il workflow app non deve applicarle e l'API non le esegue all'avvio.
- Approvare costo ACR Basic aggiornato, CPU/memoria ACA, massimo repliche e
  soglie dei probe; readiness controlla SQL e il resume del DB può durare
  30–60 secondi.
- Verificare il `what-if` Bicep e la corrispondenza dei nomi/resource ID contro
  le risorse realmente inventariate, prima di abilitare qualunque workflow.

Alla chiusura dei gate, i workflow andranno creati separatamente e solo con
`workflow_dispatch`; nessun trigger `push`, `pull_request` o `schedule` dovrà
pubblicare. Ogni release richiederà approvazione dell'environment e userà
un'immagine identificata da commit SHA.
