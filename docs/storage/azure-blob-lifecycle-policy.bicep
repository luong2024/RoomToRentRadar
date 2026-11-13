@description('Name of the existing Azure Storage Account')
param storageAccountName string

@description('Configured KYC blob container name matching KycStorage:ContainerName in appsettings')
param kycContainerName string = 'kyc'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

resource managementPolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          enabled: true
          name: 'cleanup-unpromoted-kyc-staging-documents'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                delete: {
                  daysAfterCreationGreaterThan: 1
                }
              }
            }
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                '${kycContainerName}/staging/'
              ]
            }
          }
        }
      ]
    }
  }
}
