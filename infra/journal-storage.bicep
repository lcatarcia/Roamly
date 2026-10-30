targetScope = 'resourceGroup'

@allowed([
  'italynorth'
])
@description('Regione approvata per lo storage Roamly.')
param location string = 'italynorth'

@description('Principal ID della managed identity runtime.')
param runtimePrincipalId string

var suffix = uniqueString(subscription().id, resourceGroup().id)
var storageAccountName = 'storoamly${suffix}'
var storageBlobDelegatorRoleId = 'db58b8e5-c6ad-4a2a-8342-4190687cbf4a'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var tags = {
  application: 'roamly'
  environment: 'production'
  managedBy: 'bicep'
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowCrossTenantReplication: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'None'
      defaultAction: 'Allow'
    }
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    changeFeed: {
      enabled: false
    }
    containerDeleteRetentionPolicy: {
      enabled: false
    }
    deleteRetentionPolicy: {
      enabled: false
    }
    isVersioningEnabled: false
  }
}

resource journalContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'roamly-journal-images'
  properties: {
    publicAccess: 'None'
  }
}

resource storageBlobDelegatorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, runtimePrincipalId, storageBlobDelegatorRoleId)
  scope: storageAccount
  properties: {
    principalId: runtimePrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageBlobDelegatorRoleId)
  }
}

resource storageBlobDataContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(journalContainer.id, runtimePrincipalId, storageBlobDataContributorRoleId)
  scope: journalContainer
  properties: {
    principalId: runtimePrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageBlobDataContributorRoleId)
  }
}

output storageAccountName string = storageAccount.name
output blobServiceUri string = storageAccount.properties.primaryEndpoints.blob
output containerName string = journalContainer.name
