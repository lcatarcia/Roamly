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

@description('Client ID della managed identity runtime, usato da Azure.Identity per selezionarla.')
param runtimeIdentityClientId string

@description('Abilita il rendering Maps personale solo dopo migration, grants e consenso. Disabilitato per default.')
param mapsEnabled bool = false

@description('Client ID pubblico dell account Maps (ARM properties.uniqueId), NON della managed identity.')
param mapsAccountClientId string = ''

@description('Immagine OCI gia pubblicata, preferibilmente identificata da SHA immutabile.')
param image string

@description('URL della versione esatta del secret Key Vault SQL runtime, conservata dal preflight. Il valore non viene letto dal template.')
param sqlConnectionSecretUrl string

@description('URL versionless del secret Key Vault contenente la chiave HMAC del limiter login. Il valore non viene letto dal template.')
param loginHmacSecretUrl string

@description('URI HTTPS dell account Blob gia verificato.')
param journalBlobServiceUri string

@description('Container Blob privato delle immagini Journal.')
param journalBlobContainerName string

@description('Conferma esplicita della verifica della configurazione effettiva dell account Blob.')
param journalBlobAccountConfigurationVerified bool

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
      registries: []
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
        maxReplicas: 1
      }
      containers: [
        {
          name: 'roamly-api'
          image: image
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
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
            {
              name: 'AZURE_CLIENT_ID'
              value: runtimeIdentityClientId
            }
            {
              name: 'Maps__Enabled'
              value: string(mapsEnabled)
            }
            {
              name: 'Maps__AccountClientId'
              value: mapsAccountClientId
            }
            {
              name: 'Maps__ManagedIdentityClientId'
              value: runtimeIdentityClientId
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 5
              timeoutSeconds: 2
              failureThreshold: 24
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 10
              timeoutSeconds: 3
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 5
              timeoutSeconds: 5
              failureThreshold: 3
            }
          ]
        }
      ]
    }
  }
}

output containerAppName string = containerApp.name
output containerAppResourceId string = containerApp.id
