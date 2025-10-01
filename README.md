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

## Deployment Steps

The deployment is handled by a PowerShell script that uses Azure CLI to provision the resources defined in the Bicep file (`iac/main.bicep`).

1.  Open a PowerShell terminal.
2.  Navigate to the `iac` directory:
    ```powershell
    cd iac
    ```
3.  Run the `deploy.ps1` script with the required parameters.

### Script Parameters

*   `ProjectName`: A unique name for your project.
*   `RegionShort`: A short name for the Azure region (e.g., `weu` for West Europe).
*   `Stage`: The deployment stage (e.g., `dev`, `tst`, `prd`).
*   `Location`: The full name of the Azure location (e.g., `westeurope`).

### Example

```powershell
.\deploy.ps1 -ProjectName "mychatbot" -RegionShort "weu" -Stage "dev" -Location "westeurope"
```

This command will:
1.  Create a resource group named `rg-mychatbot-weu-dev` if it doesn't already exist.
2.  Deploy the resources defined in `main.bicep` into that resource group.
