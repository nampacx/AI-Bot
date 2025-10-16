# AI Foundry Console Agent Client

This is a sample console application that demonstrates how to use the AI Foundry Agent Service to interact with an AI agent from a command-line interface.

## Features

- Interactive console interface for chatting with an AI agent
- Persistent conversation threads
- Simple command system for managing conversations
- Proper logging and error handling
- Configuration through appsettings.json

## Prerequisites

Before running this application, you need:

1. **Azure AI Foundry Project**: An active Azure AI Foundry project
2. **Model Deployment**: A deployed model in your AI Foundry project
3. **Authentication**: Appropriate Azure credentials (see Authentication section)

## Configuration

1. Open `appsettings.json` and configure the following settings:

```json
{
    "MicrosoftAppTenantId": "your-azure-tenant-id",
    "AIFoundry": {
        "ProjectEndpoint": "https://your-project.cognitiveservices.azure.com/",
        "ModelDeploymentName": "your-model-deployment-name",
        "AgentName": "ConsoleAgent",
        "ManagedIdentityClientId": ""
    }
}
```

### Required Configuration Values:

- **MicrosoftAppTenantId**: Your Azure tenant ID
- **AIFoundry:ProjectEndpoint**: The endpoint URL of your AI Foundry project
- **AIFoundry:ModelDeploymentName**: The name of your deployed model (e.g., "gpt-4", "gpt-35-turbo")
- **AIFoundry:AgentName**: A unique name for your agent instance

### Optional Configuration:

- **AIFoundry:ManagedIdentityClientId**: If using user-assigned managed identity, specify the client ID

## Authentication

The application uses Azure Default Credential for authentication, which supports multiple authentication methods in the following order:

1. **Environment Variables** (for development/testing)
2. **Managed Identity** (when running in Azure)
3. **Visual Studio** (when developing locally with VS signed in)
4. **Azure CLI** (when signed in via `az login`)
5. **Azure PowerShell** (when signed in via PowerShell)

### For Local Development:

The easiest method is to use Azure CLI:

```powershell
az login --tenant your-tenant-id
```

### For Production:

Use Managed Identity when deploying to Azure services.

## Running the Application

1. **Build the project:**
   ```powershell
   dotnet build
   ```

2. **Run the application:**
   ```powershell
   dotnet run
   ```

3. **Start chatting:**
   - Type your messages and press Enter
   - The agent will respond to your prompts
   - Use special commands for additional functionality

## Available Commands

While in the console application, you can use these commands:

- **exit** or **quit** - Exit the application
- **clear** - Start a new conversation (cleans up the current thread)
- **help** - Show available commands and configuration info

## How It Works

1. **Initialization**: The application creates an AI Foundry Agent using the configured model and instructions
2. **Conversation Management**: Each conversation gets a unique thread ID for maintaining context
3. **Message Processing**: User messages are sent to the agent, and responses are displayed
4. **Cleanup**: Threads and agents are properly cleaned up when conversations end or the app exits

## Agent Instructions

The agent behavior is defined in `instructions.prompty`. You can modify this file to customize how the agent responds. The application will automatically read the system instructions from this file when creating the agent.

## Troubleshooting

### Authentication Issues:
- Ensure you're logged in with Azure CLI: `az login`
- Verify your tenant ID is correct
- Check that your account has access to the AI Foundry project

### Connection Issues:
- Verify the ProjectEndpoint URL is correct
- Ensure the model deployment name matches exactly
- Check network connectivity to Azure

### Agent Creation Issues:
- Verify the model deployment is active and available
- Check that you have permissions to create agents in the AI Foundry project
- Review the logs for detailed error messages

## Logging

The application includes comprehensive logging. Check the console output for detailed information about:
- Authentication status
- Agent creation and retrieval
- Message processing
- Error details

You can adjust logging levels in the `appsettings.json` file under the `Logging` section.

## Example Usage

```
=== AI Foundry Console Agent Client ===
Type your messages and press Enter to send them to the agent.
Type 'exit' or 'quit' to end the conversation.
Type 'clear' to start a new conversation.
Type 'help' for more commands.

You: Hello, can you help me understand what Azure AI Foundry is?
Agent: Thinking...
Agent: Hello! I'd be happy to help you understand Azure AI Foundry...

You: Can you write a simple Python script for me?
Agent: Thinking...
Agent: Of course! Here's a simple Python script...

You: clear
Started new conversation.

You: exit
Goodbye!
```

## Dependencies

- Azure.AI.Agents.Persistent
- Azure.Identity
- Microsoft.Extensions.* (Configuration, DependencyInjection, Logging, Hosting)

## License

This sample code is provided as-is for demonstration purposes.