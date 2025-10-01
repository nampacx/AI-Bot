@description('The name of the Azure Bot resource.')
param botName string

@description('The location of the Azure Bot resource.')
param location string = resourceGroup().location

@description('The resource ID of the user-assigned managed identity.')
param userAssignedIdentityResourceId string

@description('The client ID of the user-assigned managed identity.')
param userAssignedIdentityClientId string

@description('The SKU of the Azure Bot resource.')
param sku string = 'F0'

resource bot 'Microsoft.BotService/botServices@2022-09-15' = {
  name: botName
  location: location
  kind: 'azurebot'
  sku: {
    name: sku
  }
  properties: {
    displayName: botName
    endpoint: ''
    msaAppType: 'UserAssignedMSI'
    msaAppId: userAssignedIdentityClientId
    msaAppMSIResourceId: userAssignedIdentityResourceId
  }
}
