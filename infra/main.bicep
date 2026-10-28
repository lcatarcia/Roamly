targetScope = 'resourceGroup'

@allowed([
  'italynorth'
])
@description('Regione approvata per le risorse Roamly.')
param location string = 'italynorth'

@description('Login SQL amministrativo usato per il bootstrap e le operazioni di manutenzione.')
param sqlAdminLogin string = 'roamly_bootstrap_admin'

@secure()
@description('Password SQL amministrativa. Viene conservata nel Key Vault e non va usata dal runtime.')
param sqlAdminPassword string

var suffix = uniqueString(subscription().id, resourceGroup().id)
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var tags = {
  application: 'roamly'
  environment: 'production'
  managedBy: 'bicep'
}
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'log-roamly-prod-${suffix}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource runtimeIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-roamly-runtime-prod'
  location: location
  tags: tags
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: 'acrroamly${suffix}'
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

resource runtimeAcrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, runtimeIdentity.id, acrPullRoleId)
  scope: containerRegistry
  properties: {
    principalId: runtimeIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      acrPullRoleId)
  }
}

module journalStorage 'journal-storage.bicep' = {
  name: 'journal-storage'
  params: {
    location: location
    runtimePrincipalId: runtimeIdentity.properties.principalId
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-roamly${suffix}'
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enablePurgeProtection: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'None'
      defaultAction: 'Allow'
    }
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: 'sql-roamly-prod-${suffix}'
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlAdminPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'sql-admin-password'
  properties: {
    value: sqlAdminPassword
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: 'roamly'
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    autoPauseDelay: 15
    requestedBackupStorageRedundancy: 'Local'
    licenseType: 'LicenseIncluded'
    maxSizeBytes: 1073741824
    minCapacity: json('0.5')
    zoneRedundant: false
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-roamly-live'
  location: location
  tags: tags
  properties: {
    peerAuthentication: {
      mtls: {
        enabled: false
      }
    }
    peerTrafficConfiguration: {
      encryption: {
        enabled: false
      }
    }
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource sqlAzureServicesFirewallRule 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output sqlFullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName
output containerAppsEnvironmentName string = containerAppsEnvironment.name
output keyVaultName string = keyVault.name
output runtimeIdentityClientId string = runtimeIdentity.properties.clientId
output runtimeIdentityPrincipalId string = runtimeIdentity.properties.principalId
output journalImageStorageAccountName string = journalStorage.outputs.storageAccountName
output journalImageStorageServiceUri string = journalStorage.outputs.blobServiceUri
output journalImageContainerName string = journalStorage.outputs.containerName
