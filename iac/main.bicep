@description('The name of the project.')
param projectName string

@description('The short name of the region (e.g., sec for Sweden Central).')
param regionShort string

@description('The deployment stage (e.g., dev, tst, prd).')
param stage string

@description('The location for all resources.')
param location string = resourceGroup().location

@description('The SKU of the Azure Bot resource.')
param sku string = 'S1'

@description('The name of the AI Foundry agent.')
param agentName string = 'my-agent'

var baseName = '${projectName}-${regionShort}-${stage}'
var botName = '${baseName}-bot'
var userAssignedIdentityName = '${baseName}-id'
var webAppName = '${baseName}-app'
var appServicePlanName = '${baseName}-plan'
var appInsightsName = '${baseName}-appi'
var aiFoundryName = '${baseName}-aif'

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
          value: 'https://${aiFoundryName}.services.ai.azure.com/api/projects/${aiProjectName}'
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
  name: 'gpt-4o'
  sku: {
    capacity: 1
    name: 'GlobalStandard'
  }
  properties: {
    model: {
      name: 'gpt-4o'
      format: 'OpenAI'
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
