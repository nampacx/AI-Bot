param (
    [Parameter(Mandatory = $true)]
    [string]$ProjectName
)

$ErrorActionPreference = "Stop"

# Construct resource names
$baseName = "$($ProjectName)"
$resourceGroupName = "rg-$($baseName)"
$webAppName = "$($baseName)-app"

# Define project path and publish output path
$projectPath = "../src/FoundryAgentBot/FoundryAgentBot.csproj"
$publishOutputPath = "../.output/publish"

# Build and publish the project
Write-Host "Building and publishing project: $($projectPath)"
dotnet publish $projectPath --configuration Release --output $publishOutputPath

# Create a new zip file for deployment, overwriting any existing one
$zipFilePath = Join-Path -Path $publishOutputPath -ChildPath "deploy.zip"
if (Test-Path $zipFilePath) {
    Write-Host "Removing existing zip file: $($zipFilePath)"
    Remove-Item -Path $zipFilePath
}
Write-Host "Creating new deployment package: $($zipFilePath)"
Compress-Archive -Path "$($publishOutputPath)/*" -DestinationPath $zipFilePath -Force
$zipFile = Get-Item -Path $zipFilePath

Write-Host "Deploying $($zipFile.FullName) to App Service: $($webAppName) in resource group: $($resourceGroupName)"

# Deploy to Azure App Service
az webapp deploy --resource-group $resourceGroupName --name $webAppName --src-path $zipFile.FullName --type zip

Write-Host "Deployment complete."
