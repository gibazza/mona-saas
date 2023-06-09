param deploymentName                string
param location                      string = resourceGroup().location
param aadClientId                   string // Used to authenticate webhook callbacks to the Marketplace API
param aadTenantId                   string // Used to authenticate webhook callbacks to the Marketplace API


@secure()
param aadClientSecret       string

var packName                      = 'default'
var cleanDeploymentName           = toLower(deploymentName)
var eventGridConnectionNamestring = 'mona-events-connection-${cleanDeploymentName}' // For subscribing to the event grid topic...
var eventGridTopicName            = 'mona-events-${cleanDeploymentName}' // For subscribing to the event grid topic...
var eventGridConnectionName       = 'mona-events-connection-${cleanDeploymentName}' // For subscribing to the event grid topic...
var storageAccountName            =  take('monalastor${uniqueString(resourceGroup().id, cleanDeploymentName)}', 24) //Standard Logic App components
var logicAppName                  = 'mona-logicapps-${cleanDeploymentName}' //Standard Logic App components
var appInsightsName               = 'mona-logicapps-insights-${cleanDeploymentName}' //Standard Logic App components
var appServicePlanName            = 'mona-logicapps-plan-${cleanDeploymentName}' //Standard Logic App components

// ========== Logic app Storage ==========
//Storage for standard logic app
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
  }
  //File service created for easier integration to vnet
  resource functionFileService 'fileServices@2022-09-01' = {
    name : 'default'
  }
}
//File Share for standard logic app
resource functionFileService 'Microsoft.Storage/storageAccounts/fileServices@2022-09-01' existing = {
  parent: storageAccount
  name : 'default'
}
resource logicAppContentShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2022-09-01' = {
  parent: functionFileService
  name: toLower(logicAppName)
}
// ========== Logic app Storage ==========

// ========== Logic app web app ==========
// Create App Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: { 
    Application_Type: 'web'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
  tags: {
    // circular dependency means we can't reference logicApp directly  /subscriptions/<subscriptionId>/resourceGroups/<rg-name>/providers/Microsoft.Web/sites/<appName>"
     'hidden-link:/subscriptions/${subscription().id}/resourceGroups/${resourceGroup().name}/providers/Microsoft.Web/sites/${logicAppName}': 'Resource'
  }
}
// Create Service Plan
resource laAppServicePlan 'Microsoft.Web/serverfarms@2020-10-01' = {
  name: appServicePlanName
  location: location
  kind: 'Windows'
  sku: {
    name: 'WS1'
    tier: 'WorkflowStandard'
  }
  properties: {
    maximumElasticWorkerCount: 1
  }
  dependsOn: [
    appInsights
    storageAccount
  ]
}
// Create Standard Logic App
resource logicApp 'Microsoft.Web/sites@2022-09-01' = {
  name: logicAppName
  location: location
  kind: 'workflowapp,functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: laAppServicePlan.id
    clientAffinityEnabled: false
    siteConfig: {
      netFrameworkVersion: 'v4.6'
      appSettings: [
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'APP_KIND'
          value: 'workflowApp'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'node'
        }
        {
          name: 'WEBSITE_NODE_DEFAULT_VERSION'
          value: '~14'
        }
        {
          name: 'WORKFLOWS_SUBSCRIPTION_ID'
          value: subscription().subscriptionId
        }
        {
          name: 'WORKFLOWS_LOCATION_NAME'
          value: location
        }
        {
          name: 'RESOURCE_GROUP_NAME'
          value: resourceGroup().id
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys('${resourceGroup().id}/providers/Microsoft.Storage/storageAccounts/${storageAccount.name}', '2019-06-01').keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys('${resourceGroup().id}/providers/Microsoft.Storage/storageAccounts/${storageAccount.name}', '2019-06-01').keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(logicAppName)
        }
        {
          name: 'WEBSITE_START_SCM_ON_SITE_CREATION'
          value: '1'
        }
      ]
      cors: {
        allowedOrigins: [
          'https://portal.azure.com'
        ]
      }
    }
  }
}
// ========== Logic app web app ==========

module onPurchased './on_purchased_workflow.bicep' = {
  name: '${packName}-pack-deploy-on-purchased-${deploymentName}'
  params: {
    deploymentName: deploymentName
    location: location
    eventGridConnectionName: eventGridConnectionName
    eventGridTopicName: eventGridTopicName
    aadClientId: aadClientId
    aadClientSecret: aadClientSecret
    aadTenantId: aadTenantId
  }
}

module onCanceled './on_canceled_workflow.bicep' = {
  name: '${packName}-pack-deploy-on-canceled-${deploymentName}'
  params: {
    deploymentName: deploymentName
    location: location
    eventGridConnectionName: eventGridConnectionName
    eventGridTopicName: eventGridTopicName
  }
}

module onPlanChanged './on_plan_changed_workflow.bicep' = {
  name: '${packName}-pack-deploy-on-plan-changed-${deploymentName}'
  params: {
    deploymentName: deploymentName
    location: location
    eventGridConnectionName: eventGridConnectionName
    eventGridTopicName: eventGridTopicName
    aadClientId: aadClientId
    aadClientSecret: aadClientSecret
    aadTenantId: aadTenantId
  }
}

module onSeatQtyChanged './on_seat_qty_changed_workflow.bicep' = {
  name: '${packName}-pack-deploy-on-seat-qty-changed-${deploymentName}'
  params: {
    deploymentName: deploymentName
    location: location
    eventGridConnectionName: eventGridConnectionName
    eventGridTopicName: eventGridTopicName
    aadClientId: aadClientId
    aadClientSecret: aadClientSecret
    aadTenantId: aadTenantId
  }
}

module onReinstated './on_reinstated_workflow.bicep' = {
  name: '${packName}-pack-deploy-on-reinstated-${deploymentName}'
  params: {
    deploymentName: deploymentName
    location: location
    eventGridConnectionName: eventGridConnectionName
    eventGridTopicName: eventGridTopicName
    aadClientId: aadClientId
    aadClientSecret: aadClientSecret
    aadTenantId: aadTenantId
  }
}

module onSuspended './on_suspended_workflow.bicep' = {
  name: '${packName}-pack-deploy-on-suspended-${deploymentName}'
  params: {
    deploymentName: deploymentName
    location: location
    eventGridConnectionName: eventGridConnectionName
    eventGridTopicName: eventGridTopicName
  }
}

module onRenewed './on_renewed_workflow.bicep' = {
  name: '${packName}-pack-deploy-on-renewed-${deploymentName}'
  params: {
    deploymentName: deploymentName
    location: location
    eventGridConnectionName: eventGridConnectionName
    eventGridTopicName: eventGridTopicName
  }
}
