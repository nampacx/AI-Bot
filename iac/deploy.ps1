# deploy.ps1

param (
    [Parameter(Mandatory=$true)]
    [string]$ProjectName,

    [Parameter(Mandatory=$true)]
    [string]$RegionShort,

    [Parameter(Mandatory=$true)]
    [string]$Stage,

    [Parameter(Mandatory=$true)]
    [string]$Location
)

$resourceGroupName = "rg-${ProjectName}-${RegionShort}-${Stage}"
$deploymentName = "bicep-deployment-${ProjectName}-${Stage}-$(Get-Date -Format 'yyyyMMddHHmmss')"

# Ensure you are logged into Azure with 'az login'

# Select the subscription if you have multiple
# az account set --subscription "Your Subscription Name or ID"

# Check if the resource group exists, and if not, create it.
Write-Host "Checking for resource group '$resourceGroupName'..."
az group show --name $resourceGroupName --output none
if ($LASTEXITCODE -ne 0) {
    Write-Host "Creating resource group '$resourceGroupName' in location '$Location'..."
    az group create --name $resourceGroupName --location $Location
} else {
    Write-Host "Resource group '$resourceGroupName' already exists."
}

# Deploy the Bicep template
Write-Host "Starting Bicep deployment '$deploymentName'..."
az deployment group create `
    --name $deploymentName `
    --resource-group $resourceGroupName `
    --template-file "./main.bicep" `
    --parameters projectName=$ProjectName `
                 regionShort=$RegionShort `
                 stage=$Stage `
                 location=$Location

Write-Host "Deployment script finished."
