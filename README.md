# AI-Bot Deployment

This document describes how to deploy the AI-Bot infrastructure using the provided Bicep templates and deployment script.

## Prerequisites

1.  **Azure CLI**: Ensure you have the [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed.
2.  **Azure Login**: You must be logged into your Azure account. Run the following command to log in:
    ```bash
    az login
    ```
3.  **Azure Subscription**: If you have multiple subscriptions, make sure to select the one you want to deploy to:
    ```bash
    az account set --subscription "<Your Subscription Name or ID>"
    ```

## Infrastructure Deployment

The infrastructure deployment is handled by a PowerShell script that uses Azure CLI to provision the resources defined in the Bicep file (`iac/main.bicep`).

1.  Open a PowerShell terminal.
2.  Navigate to the `deployment` directory:
    ```powershell
    cd deployment
    ```
3.  Run the `deploy-infra.ps1` script with the required parameters.

### Script Parameters

*   `ProjectName`: A unique name for your project.
*   `Location`: The full name of the Azure location (e.g., `swedencentral`).

### Example

```powershell
.\deploy-infra.ps1 -ProjectName "mychatbot" -Location "swedencentral"
```

This command will:
1.  Create a resource group named `mychatbot-dev-rg` if it doesn't already exist.
2.  Deploy the resources defined in `../iac/main.bicep` into that resource group.

## Application Code Deployment

After deploying the infrastructure, you can deploy the application code to the created App Service.

1.  Open a PowerShell terminal.
2.  Navigate to the `deployment` directory:
    ```powershell
    cd deployment
    ```
3.  Run the `deploy-app.ps1` script with the same parameters you used for the infrastructure deployment.

### Script Parameters

*   `ProjectName`: The unique name for your project.

### Example

```powershell
.\deploy-app.ps1 -ProjectName "mychatbot"
```

This command will:
1.  Build and publish the `FoundryAgentBot` project.
2.  Create a zip file of the published application.
3.  Deploy the zip file to the corresponding App Service.
