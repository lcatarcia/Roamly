targetScope = 'resourceGroup'

@allowed([
  'italynorth'
])
@description('Regione approvata per Roamly.')
param location string = 'italynorth'

@description('Nome univoco della Container App; non viene inventato nel template.')
param containerAppName string

@description('Resource ID dell ambiente ACA Consumption gia esistente.')
param containerAppsEnvironmentResourceId string

@description('Resource ID della managed identity runtime gia esistente.')
param runtimeIdentityResourceId string

@description('Nome DNS del registry ACR Basic.')
param registryServer string

@description('Immagine OCI gia pubblicata, preferibilmente identificata da SHA immutabile.')
param image string

@description('URL versionless del secret Key Vault contenente la connection string SQL runtime. Il valore non viene letto dal template.')
param sqlConnectionSecretUrl string

@description('URL versionless del secret Key Vault contenente la chiave HMAC del limiter login. Il valore non viene letto dal template.')
param loginHmacSecretUrl string

@description('URI HTTPS dell account Blob gia verificato.')
param journalBlobServiceUri string

@description('Container Blob privato delle immagini Journal.')
param journalBlobContainerName string

@description('Conferma esplicita della verifica della configurazione effettiva dell account Blob.')
param journalBlobAccountConfigurationVerified bool

@description('CPU in vCPU. Richiesto: il profilo di compute non e stato ancora deciso.')
param cpuCores string

@description('Memoria ACA, nel formato ammesso dalla piattaforma. Richiesta: il profilo non e stato ancora deciso.')
param memory string

@minValue(1)
@description('Numero massimo di repliche. Richiesto per esplicitare il limite di costo/capacita.')
param maxReplicas int

@minValue(0)
@description('Attesa prima del probe liveness.')
param livenessInitialDelaySeconds int

@minValue(1)
@description('Intervallo del probe liveness in secondi.')
param livenessPeriodSeconds int

@minValue(1)
@description('Timeout del probe liveness in secondi.')
param livenessTimeoutSeconds int

@minValue(1)
@description('Numero di fallimenti del probe liveness prima dell esito negativo.')
param livenessFailureThreshold int

@minValue(0)
@description('Attesa prima del probe readiness, da dimensionare rispetto al cold-start SQL.')
param readinessInitialDelaySeconds int

@minValue(1)
@description('Intervallo del probe readiness in secondi.')
param readinessPeriodSeconds int

@minValue(1)
@description('Timeout del probe readiness in secondi.')
param readinessTimeoutSeconds int

@minValue(1)
@description('Numero di fallimenti del probe readiness prima dell esito negativo.')
param readinessFailureThreshold int

var tags = {
  application: 'roamly'
  environment: 'production'
  managedBy: 'bicep'
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${runtimeIdentityResourceId}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironmentResourceId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: registryServer
          identity: runtimeIdentityResourceId
        }
      ]
      secrets: [
        {
          name: 'roamly-sql-connection'
          keyVaultUrl: sqlConnectionSecretUrl
          identity: runtimeIdentityResourceId
        }
        {
          name: 'roamly-login-hmac'
          keyVaultUrl: loginHmacSecretUrl
          identity: runtimeIdentityResourceId
        }
      ]
    }
    template: {
      scale: {
        minReplicas: 0
        maxReplicas: maxReplicas
      }
      containers: [
        {
          name: 'roamly-api'
          image: image
          resources: {
            cpu: json(cpuCores)
            memory: memory
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ConnectionStrings__Roamly'
              secretRef: 'roamly-sql-connection'
            }
            {
              name: 'RateLimiting__Login__HmacSecret'
              secretRef: 'roamly-login-hmac'
            }
            {
              name: 'JournalImages__Provider'
              value: 'AzureBlob'
            }
            {
              name: 'JournalImages__Azure__ServiceUri'
              value: journalBlobServiceUri
            }
            {
              name: 'JournalImages__Azure__ContainerName'
              value: journalBlobContainerName
            }
            {
              name: 'JournalImages__Azure__AccountConfigurationVerified'
              value: string(journalBlobAccountConfigurationVerified)
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: livenessInitialDelaySeconds
              periodSeconds: livenessPeriodSeconds
              timeoutSeconds: livenessTimeoutSeconds
              failureThreshold: livenessFailureThreshold
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: readinessInitialDelaySeconds
              periodSeconds: readinessPeriodSeconds
              timeoutSeconds: readinessTimeoutSeconds
              failureThreshold: readinessFailureThreshold
            }
          ]
        }
      ]
    }
  }
}

output containerAppName string = containerApp.name
output containerAppResourceId string = containerApp.id
