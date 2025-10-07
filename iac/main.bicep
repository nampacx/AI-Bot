@description('The name of the project.')
param projectName string

@description('The location for all resources.')
param location string

@description('The SKU of the Azure Bot resource.')
param sku string

@description('The name of the AI Foundry agent.')
param agentName string

@description('The name of the model deployment.')
param modelDeploymentName string

@description('The name of the model.')
param modelName string

@description('The format of the model.')
param modelFormat string

@description('The SKU name of the model deployment.')
param modelSkuName string

@description('The SKU capacity of the model deployment.')
param modelSkuCapacity int

var botName = '${projectName}-bot'
var userAssignedIdentityName = '${projectName}-id'
var webAppName = '${projectName}-app'
var appServicePlanName = '${projectName}-plan'
var appInsightsName = '${projectName}-appi'
var aiFoundryName = '${projectName}-aif'

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
          name: 'MicrosoftAppId'
          value: userAssignedIdentity.properties.clientId
        }
        {
          name: 'MicrosoftAppTenantId'
          value: subscription().tenantId
        }
        {
          name: 'MicrosoftAppType'
          value: 'UserAssignedMSI'
        }
        {
          name: 'AIFoundry__ProjectEndpoint'
          value: 'https://${aiFoundryName}.services.ai.azure.com/api/projects/${projectName}'
        }
        {
          name: 'AIFoundry__ModelDeploymentName'
          value: modelDeployment.name
        }
        {
          name: 'AIFoundry__ManagedIdentityClientId'
          value: userAssignedIdentity.properties.clientId
        }
        {
          name: 'AIFoundry__AgentName'
          value: agentName
        }
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
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
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

resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: aiFoundryName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: 'S0'
  }
  kind: 'AIServices'
  properties: {
    publicNetworkAccess: 'Enabled'
    allowProjectManagement: true
    customSubDomainName: aiFoundryName
    disableLocalAuth: true
  }
}

resource aiProject 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' = {
  name: projectName
  parent: aiFoundry
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: aiFoundry
  name: modelDeploymentName
  sku: {
    capacity: modelSkuCapacity
    name: modelSkuName
  }
  properties: {
    model: {
      name: modelName
      format: modelFormat
    }
  }
}

resource userAssignedIdentityAiFoundryRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiFoundry.id, userAssignedIdentity.id, 'AzureAIUserRole')
  scope: aiFoundry
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '53ca6127-db72-4b80-b1b0-d745d6d5456d'
    ) // Azure AI User
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
