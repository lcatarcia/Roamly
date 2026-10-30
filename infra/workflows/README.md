# Workflow OIDC di release

Il workflow eseguibile è `.github/workflows/release.yml`; `release.yml` in
questa directory ne è la copia di revisione e deve restare sincronizzata. Il
preflight aggiunto localmente non è ancora stato pubblicato tramite PR/CI
né eseguito sul branch protetto `main`. I run riusciti del workflow legacy
non validano questo candidato. La presenza del file non sostituisce i check
della pull request e le approvazioni per ciascun job protetto.

I gate di configurazione e verifica HIGH elencati in
[`../../docs/architecture/DEVOPS.md`](../../docs/architecture/DEVOPS.md), nella
sezione "Stato operativo: preparazione locale release", devono essere chiusi
prima del dispatch, salvo la verifica con OIDC fresco: questa avviene nel
job protetto, dopo la prima approvazione e prima della prima scrittura live.

## Workflow `release.yml`

Il workflow è dispatch-only, rifiuta esplicitamente qualsiasi ref diverso da
`refs/heads/main` e offre le modalità `release` (predefinita) e `rollback`.
Entrambe richiedono due approvazioni dell'Environment protetto `azure-release`:
la prima prima della preparazione/what-if, la seconda dopo la revisione del
riepilogo e prima dell'applicazione. In modalità `release`, i job richiedono
solo `contents: read` e `id-token: write`, usano OIDC, costruiscono il
`Dockerfile` e pubblicano su ACR con il nome del repository GitHub in minuscolo
e il tag `${{ github.sha }}-${{ github.run_id }}-${{ github.run_attempt }}`.
In modalità `rollback` è richiesto un digest OCI `sha256` valido, viene saltato
build/push e lo stesso digest passa attraverso what-if, confronto e approvazioni.
Subito dopo `azure/login`, in entrambe le modalità, il job `prepare` esegue un
preflight read-only fail-closed: confronta tipo `servicePrincipal`, client ID,
tenant e subscription dell'account autenticato con le tre variabili OIDC non
segrete, validate come GUID. Esegue GET ARM esplicite della app esistente,
delle revisioni, dell'environment e della managed identity runtime, verificando
gli ID restituiti, i collegamenti app/environment/identity e gli identificativi
della identity. Una lista revisioni vuota, malformata o paginata viene rifiutata.
Qualsiasi richiesta fallita o identificativo assente/diverso interrompe il job,
senza build/push né what-if. Non ci sono retry di autenticazione, token nei log,
letture di secret, `listSecrets`, exec o probe distruttivi.

Questo è il posizionamento approvato del gate OIDC fresco: dopo la prima
approvazione Environment, che permette il login federato, ma prima di qualunque
scrittura live. Non è una verifica pre-dispatch, né un inventario RBAC: non
richiede `Microsoft.Authorization/permissions/read` sulla app e non prova il
diniego di operazioni sensibili. L'inventario dei ruoli effettivi resta una
verifica separata dell'operatore, descritta sotto. Le due approvazioni,
il guard dispatch/main, il rollback e il confronto what-if rimangono invariati.

Il job di preparazione esegue il what-if con il
digest OCI restituito dal push e ne pubblica il risultato nel riepilogo della
run insieme all'impronta SHA-256 delle modifiche normalizzate. Dopo la seconda
approvazione, il job di deploy ricalcola il what-if con lo stesso commit,
parametri e digest; applica Bicep solo se l'impronta coincide, altrimenti si
ferma senza modificare risorse e richiede un nuovo dispatch e una nuova
revisione. Il deploy usa lo stesso digest con `infra/container-app.bicep` e i
riferimenti versionless ai secret Key Vault. Il controllo riduce il rischio di
drift tra revisione e deploy, ma non elimina la finestra residua tra il
confronto e l'applicazione ARM. Non legge valori Key Vault, non applica
migration e non fa bootstrap.

Dopo il completamento ARM, il job usa l'Azure CLI già autenticata per ritentare
la discovery del FQDN fino a 6 volte (timeout per chiamata 10 secondi, pausa
5 secondi). Interroga poi la lista ARM delle revisioni ACA con API
`2024-03-01` e richiede, prima dei probe, esattamente una revisione attiva in
totale e una sola immagine container su quella revisione identica al digest di
`RELEASE_IMAGE`. I campi verificati sono `value[].properties.active` e
`value[].properties.template.containers[].image`; la risposta paginata (`nextLink`
non nullo), revisioni attive multiple o un digest precedente non superano
l'asserzione. Il contratto è documentato da
[Microsoft Learn — Container Apps Revisions: List Revisions](https://learn.microsoft.com/en-us/rest/api/resource-manager/containerapps/container-apps-revisions/list-revisions?view=rest-resource-manager-containerapps-2024-03-01).

Solo dopo l'asserzione il job verifica via HTTPS pubblico `/health/live` e
`/health/ready`, entrambi HTTP 200: al massimo 5 tentativi, timeout di
connessione 5 secondi e timeout complessivo 10 secondi per richiesta, pausa di
10 secondi tra i tentativi. I body HTTP non vengono registrati né conservati.
I due controlli di metadata hanno al massimo 6 tentativi ciascuno, timeout
singolo 10 secondi e pausa 5 secondi. Il limite dello step resta 7 minuti:
il tempo massimo nominale combinato di polling e probe è circa 310 secondi
(5 minuti e 10 secondi), senza aumentare il timeout esistente. Non vengono
aggiunte action o dipendenze; si riusa l'identità OIDC e il set di permessi
esistente, senza leggere secret o stamparne valori.

Se FQDN, asserzione della revisione o probe falliscono, il workflow si chiude
senza annullare un deployment ARM eventualmente già applicato. Verificare lo
stato e procedere con fix-forward o recovery manuale; nessun rollback
applicativo o SQL è automatico. La Container App ha già una revisione live
attiva: prima di un rilascio, acquisire il digest dell'immagine corrente e
verificarne salute e disponibilità in ACR prima di usarlo come candidato di
rollback. Se non è possibile confermarli, fermarsi e procedere con fix-forward
o con una decisione umana.

Il workflow non è stato eseguito. Il deployment è pinning al digest OCI
immutabile; il tag derivato da commit, run ID e tentativo è solo un alias ACR,
non è bloccato e non viene usato per il deploy. L'utente ha approvato questa
strategia come alternativa alla lock policy: riduce le collisioni accidentali,
ma non impedisce a un principal autorizzato di riscrivere il tag. Le
assegnazioni OIDC live documentate includono `AcrPush`, senza una policy ACR
che renda immutabili tag o manifest. Se in futuro servirà una garanzia
registry-level, andranno valutati separatamente i lock di tag e manifest e i
permessi necessari. Il rollback applicativo manuale tramite dispatch protetto
è la strategia scelta: selezionare il digest dell'ultima release sana e
sottoporlo agli stessi due gate e al confronto what-if. Al primo deploy non
esiste ancora una release del workflow da cui derivare automaticamente un
digest; usare la revisione live corrente solo dopo averne verificato salute e
disponibilità in ACR. In caso contrario, fermarsi e procedere con fix-forward
o attendere una decisione umana. Questa modalità non esegue né annulla
migration SQL.

## Pin immutabili delle GitHub Actions

Il workflow usa SHA completi di commit, non tag mutabili:

| Action | Release upstream | SHA di commit | Ref verificato |
| --- | --- | --- | --- |
| `actions/checkout` | `v6.1.0` | `d23441a48e516b6c34aea4fa41551a30e30af803` | [`refs/tags/v6.1.0`](https://github.com/actions/checkout/tree/v6.1.0) |
| `azure/login` | `v2.3.1` | `7184910d9eb2b1c5e48f7073824a90609bb9b6d6` | [`refs/tags/v2.3.1^{}`](https://github.com/Azure/login/tree/7184910d9eb2b1c5e48f7073824a90609bb9b6d6) |

Le SHA sono state risolte dalle tag ufficiali con `git ls-remote --tags`:
`actions/checkout` espone `v6.1.0` direttamente come commit; `azure/login`
usa una tag annotata, quindi è stata usata la SHA di commit peeled da
`refs/tags/v2.3.1^{}`, non la SHA dell'oggetto tag. Questi pin non chiudono i
gate di configurazione o verifica né autorizzano un dispatch o un deployment.

## Separazione da mantenere

1. **Bootstrap infrastruttura**, manuale:
   validazione/what-if e poi provisioning Bicep solo dopo approvazione. Non deve
   essere usato per pubblicare immagini o aggiornare l'applicazione.
2. **Release applicativa**, manuale e OIDC con principal dedicato:
   build dal
   `Dockerfile`, push all'ACR con tag univoco derivato da commit SHA, run ID e
   tentativo e aggiornamento della Container App dal template
   `../container-app.bicep` usando il digest OCI, non il tag. La modalità
   rollback riusa un digest sano precedente e salta build/push, mantenendo gli
   stessi gate di revisione.
   Non deve leggere segreti Key Vault né applicare migration o creare il
   principal SQL.

Il bootstrap resta manuale. La app registration `roamly-github-oidc` è
riservata alla release; il trust `azure-bootstrap` è stato rimosso e resta
solo il subject `azure-release`. Questa scelta evita di esporre le
autorizzazioni release a un futuro workflow bootstrap.

La topologia approvata è same-origin: la stessa app ACA serve la UI e l'API.
Gli Environments `azure-bootstrap` e `azure-release` sono configurati live:
entrambi richiedono approvazione manuale di `lcatarcia` (User ID `70380170`),
impediscono l'auto-approvazione, limitano i deploy ai branch protetti e hanno
`can_admins_bypass=false`. È attivo anche il ruleset `Roamly - protezione main`
(ID `25866835`), applicato solo a `refs/heads/main`: richiede PR e i
quattro check obbligatori con branch aggiornato (`strict:
true`), revoca approvazioni obsolete, blocca eliminazione e force-push e non ha
bypass actor. Non impone CODEOWNERS, approvazione dell'ultimo push o risoluzione
dei thread. Al 2026-10-30 il numero di approvazioni PR richieste è **0**,
per scelta esplicita dell'utente; ciò non rimuove le due approvazioni
Environment del rilascio. La presenza di queste protezioni non
configura OIDC, RBAC o workflow. Dettagli nelle
[regole GitHub per i ruleset](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets).

Il dispatcher `lorenzocatarciaagic` ha accesso `Write` effettivo al repository.
Questo non configura OIDC, le app registration o RBAC.

## Configurazione GitHub ammessa

La release usa l'app registration `roamly-github-oidc` (client ID
`185f6e3a-2b8a-465c-897e-1e561d4d7a10`), distinta dalla managed identity
runtime. La sola federated credential live è limitata al subject
`repo:lcatarcia/Roamly:environment:azure-release`. Gli identificativi non
segreti andranno passati come GitHub Environment Variables:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Solo l'Environment `azure-release` usa il client ID. Le variabili non segrete
OIDC sono state configurate nel GitHub Environment. Al principal OIDC sono stati assegnati il custom role
deployments sul RG, il custom role ACA sulla sola app e il ruolo read/join sul solo environment,
`AcrPush` solo sull'ACR e `Managed Identity Operator` solo sulla runtime
identity. Il repository non deve contenere client secret, PAT, password SQL o
valori dei secret Key Vault; GitHub non deve ricevere alcun valore di secret.
Il workflow userà solo federazione OIDC e permessi `contents: read` +
`id-token: write` strettamente necessari. Gli Environments e le protezioni
`main` sono già configurati live come descritto sopra.

## Gate prima di rendere attivabili i workflow

- Verificare prima dell'attivazione che le protezioni live di `main` e degli
  Environments restino coerenti con i workflow e i branch previsti. Ruleset e
  approvazioni degli Environments sono già configurati; l'app registration
  release e la relativa federated credential sono state verificate live. Il
  trust bootstrap è stato rimosso. Le variabili GitHub non secret sono già
  impostate nell'Environment `azure-release`; riverificarle prima dell'attivazione.
- Le assegnazioni RBAC per la sola release sono state ridotte il 2026-10-30:
  ruolo custom Container Apps sulla sola `ca-roamly-prod`,
  ruolo read/join sul solo environment `cae-roamly-live`,
  `Managed Identity Operator` sulla sola runtime identity, `AcrPush` sull'ACR
  e ruolo custom `Microsoft.Resources/deployments/*` sul resource group. Tutti
  questi ruoli sono limitati al principal OIDC release. Il bootstrap resta
  manuale; non assegnare `Contributor` o `Role Based Access Control
  Administrator` all'identità release. L'assegnazione ACA al RG è stata rimossa.
  Le definizioni e le assegnazioni dirette sono state rilette dopo ogni modifica;
  la verifica con token fresco del principal release resta da eseguire nel
  preflight dopo la prima approvazione Environment e prima di build/push/what-if.
- Il JSON del ruolo app è
  [`../roles/container-apps-release-no-secrets.json`](../roles/container-apps-release-no-secrets.json).
  La definizione live, aggiornata con ID
  `ce62cffa-613a-49db-9fc2-f299757a7e36`, assegnabile sul RG `rg-roamly-dev`,
  concede solo `Microsoft.App/containerApps/read`, `write` e `revisions/read`
  sulla app `ca-roamly-prod`. Il ruolo environment
  [`../roles/container-apps-release-environment-join.json`](../roles/container-apps-release-environment-join.json),
  ID `6f5774ed-a688-4ac7-859a-727bceb591bf`, concede solo
  `Microsoft.App/managedEnvironments/read` e `join/action` su `cae-roamly-live`.
  `AssignableScopes` delimita dove i ruoli possono essere assegnati, non concede
  accesso al RG. Entrambi hanno `NotActions` e permessi data-plane vuoti.
  Non concedono wildcard ACA, delete, exec o `listSecrets`; altri ruoli potrebbero
  comunque concederli. Il permesso app `write` consente di cambiare l'intera
  configurazione e l'immagine della sola app: non è un vincolo image-only.
- La custom role `infra/roles/resource-deployments-release.json`, con la sola
  azione `Microsoft.Resources/deployments/*`, è stata creata live e assegnata
  sul RG; il suo ID è `432216d0-b0ea-4fa7-b2e0-d5e4000a6d72`.
- Gate RBAC separato dal preflight: il 2026-10-30 l'operatore ha verificato
  tramite ARM il filtro `assignedTo('8a816682-a894-450a-9178-7767b8cdd87a')`
  sull'intera subscription, seguendo la paginazione fino alla fine, e ha
  esaminato tutte le definizioni restituite. Risultano esattamente cinque
  assegnazioni dirette `ServicePrincipal` agli scope sopra descritti; nessuna
  assegnazione ereditata/da gruppi restituita, nessun grant ACA `listSecrets`,
  exec o delete. Il filtro include i gruppi transitivi per user/service
  principal senza richiedere Graph:
  [Microsoft Learn — List role assignments](https://learn.microsoft.com/en-us/azure/role-based-access-control/role-assignments-list-rest).
  Questa evidenza usa la sessione dell'operatore, non il token fresco del
  principal release. Quest'ultimo verifica le GET nel preflight protetto,
  senza inventario `permissions/read` sulla app o test negativi sensibili.
  Il percorso write/join non è ancora stato
  provato dal workflow: richiede il what-if e il successivo deploy autorizzati.
  Ripetere la verifica dopo cambi a ruoli built-in, custom role o assegnazioni.
  Se manca un permesso, fermare la release e revisionare l'azione esatta:
  non ripristinare automaticamente il vecchio ruolo ampio.

Riferimenti Microsoft: [Container Apps secrets e `listSecrets`](https://learn.microsoft.com/en-us/azure/container-apps/manage-secrets),
[Azure custom roles e `AssignableScopes`](https://learn.microsoft.com/en-us/azure/role-based-access-control/custom-roles),
[JSON Azure CLI per definizioni custom](https://learn.microsoft.com/en-us/azure/role-based-access-control/custom-roles-cli),
[semantica di `NotActions`](https://learn.microsoft.com/en-us/azure/role-based-access-control/role-definitions#notactions).
- Conservare la password SQL amministrativa nel secret Key Vault
  `sql-admin-password`; lo strumento la legge solo dopo la conferma esplicita
  `READ ADMIN SECRET`, senza mostrarla o salvarla localmente. Il bootstrap del
  contained user e del secret runtime usa lo strumento locale versionato in
  [`infra/tools/runtime-bootstrap`](../tools/runtime-bootstrap/README.md).
  Lo strumento resta separato dai workflow e non applica migration.
- Creare il contained SQL user e il secret Key Vault runtime con la connection
  string completa approvata, senza esporne il valore a GitHub o ai log; la sola
  managed identity runtime avrà accesso ai secret runtime necessari.
- Dopo revisione approvata delle migration, generare script SQL EF idempotente,
  revisionarlo ed eseguirlo localmente. Non eseguire `Down()` automaticamente;
  il workflow app non applica migration e l'API non le esegue all'avvio.
- Usare nel template ACA il profilo approvato (min 0/max 1 replica, 0,25 vCPU,
  0,5 GiB), startup e liveness process-only su `/health/live`, readiness SQL su
  `/health/ready` e le soglie documentate in `DEVOPS.md`. La decisione non
  configura risorse live.
- Il budget Cost mensile da 10 EUR pre-tax è configurato e verificato via API
  sul solo `rg-roamly-dev`, come notifica soltanto e senza cap. Sono attivi gli
  avvisi 80% Actual e 100% Forecasted all'email del proprietario subscription;
  l'utente ha scelto di non aggiungere 100% Actual. Gli altri alert restano da
  configurare separatamente.
- Verificare il costo corrente ACR Basic e il `what-if` Bicep, la corrispondenza
  di nomi/resource ID e la configurazione reale prima di qualunque workflow.
- Strategia tag ACR decisa: usare commit SHA + run ID + tentativo come alias
  univoco e il digest OCI come unico riferimento di deploy. Non è configurato
  un lock ACR; il tag resta tecnicamente mutabile. Un requisito futuro di
  immutabilità registry-level richiede una decisione e una verifica RBAC
  separate.
- Recovery applicativo deciso: rollback manuale via dispatch protetto a un
  digest noto come sano, con gli stessi gate e confronto what-if. Al primo
  deploy di questo candidato esiste già una revisione live sana verificata
  dall'operatore il 2026-10-30: `ca-roamly-prod--0000007`, health live/ready
  HTTP 200, immagine `acrroamlywse7wit7fzpja.azurecr.io/roamly@sha256:9e996dee3f2eb9d5e756469ee6f0fc3bde95916c0ce58a6a8abb78c9e724c275`.
  La richiesta HEAD del manifest ACR è riuscita e `Docker-Content-Digest`
  coincide con quel digest. Riverificare prima di usarlo se lo stato cambia.
  Nessun rollback o restore SQL è autorizzato da questa scelta.
- La cronologia live `__EFMigrationsHistory` è stata verificata in sola lettura
  il 2026-10-30: risultano applicate le 15 migration fino a
  `20261028134346_AddCamperManualSpecifications`. Nessuna migration è stata
  applicata in questa sessione; il workflow non applica migration.
  Il confronto successivo al merge `ca591a5` ha confermato la corrispondenza
  esatta dei 15 ID tra repository e database live e l'assenza di
  `20261023083431_AddAuthenticationInfrastructure` nel registro live.
  Colonne, tipi, default, chiavi e indici degli oggetti di autenticazione
  coinvolti coincidono con le migration del branch: nessuna riconciliazione
  SQL o modifica del registro è necessaria.

Il workflow di release è già nel working tree sotto `.github/workflows/` e ha
una copia di revisione in questa directory; la modifica preflight deve ancora
passare PR/CI e non è stata eseguita. Usa solo `workflow_dispatch`, senza trigger
`push`, `pull_request` o `schedule`. Ogni release richiede un'approvazione
prima della preparazione e una seconda approvazione dopo l'esame del what-if,
prima del deploy; usa un alias univoco derivato da commit/run/tentativo e il
digest OCI come identità effettiva dell'immagine. La modalità `rollback` salta
build/push e richiede un digest `sha256` precedente, con gli stessi gate di
approvazione e what-if. Prima del dispatch, chiudere i gate operatore sopra e
in `DEVOPS.md`; l'evidenza OIDC fresca viene raccolta dal preflight dopo la
prima approvazione, prima di ogni build/push o what-if.
