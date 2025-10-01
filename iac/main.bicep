@description('The name of the project.')
param projectName string

@description('The short name of the region (e.g., weu for West Europe).')
param regionShort string

@description('The deployment stage (e.g., dev, tst, prd).')
param stage string

@description('The location for all resources.')
param location string = resourceGroup().location

@description('The SKU of the Azure Bot resource.')
param sku string = 'S1'

var botName = '${projectName}-${regionShort}-${stage}-bot'
var userAssignedIdentityName = '${projectName}-${regionShort}-${stage}-id'
var webAppName = '${projectName}-${regionShort}-${stage}-app'
var appServicePlanName = '${projectName}-${regionShort}-${stage}-plan'
var appInsightsName = '${projectName}-${regionShort}-${stage}-ai'

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: userAssignedIdentityName
  location: location
}

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      appSettings: [
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
      ]
    }
    httpsOnly: true
  }
  tags: {
    'hidden-link: /app-insights-resource-id': appInsights.id
  }
}

resource bot 'Microsoft.BotService/botServices@2022-09-15' = {
  name: botName
  location: 'global'
  kind: 'azurebot'
  sku: {
    name: sku
  }
  properties: {
    displayName: botName
    endpoint: 'https://${webApp.properties.defaultHostName}/api/messages'
    msaAppType: 'UserAssignedMSI'
    msaAppId: userAssignedIdentity.properties.clientId
    msaAppMSIResourceId: userAssignedIdentity.id
    msaAppTenantId: subscription().tenantId
  }
}
