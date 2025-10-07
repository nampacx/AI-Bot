#nullable enable

using Azure.AI.Agents.Persistent;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FoundryAgentBot.Services
{
    public interface IAIFoundryAgentService
    {
        Task<string> ProcessMessageAsync(string userMessage, string conversationId);
        Task CleanupConversationAsync(string conversationId);
    }

    public class AIFoundryAgentService : IAIFoundryAgentService, IDisposable
    {
        private readonly PersistentAgentsClient _client;
        private readonly PersistentAgent _agent;
        private readonly ConcurrentDictionary<string, PersistentAgentThread> _threads;
        private readonly ILogger<AIFoundryAgentService> _logger;
        private readonly string _modelDeploymentName;
        private readonly string _agentName;

        public AIFoundryAgentService(IConfiguration configuration, ILogger<AIFoundryAgentService> logger)
        {
            _logger = logger;
            _threads = new ConcurrentDictionary<string, PersistentAgentThread>();

            _logger.LogInformation("Initializing AI Foundry Agent Service");

            // Get configuration values
            _modelDeploymentName = configuration["AIFoundry:ModelDeploymentName"] ?? throw new InvalidOperationException("AIFoundry:ModelDeploymentName is required");
            _logger.LogInformation("Model Deployment Name: {ModelDeploymentName}", _modelDeploymentName);

            _agentName = configuration["AIFoundry:AgentName"] ?? throw new InvalidOperationException("AIFoundry:AgentName is required");
            _logger.LogInformation("Agent Name: {AgentName}", _agentName);

            // Create the client
            var projectEndpoint = configuration["AIFoundry:ProjectEndpoint"] ?? throw new InvalidOperationException("AIFoundry:ProjectEndpoint is required");
            _logger.LogInformation("Project Endpoint: {ProjectEndpoint}", projectEndpoint);

            var tenantId = configuration["MicrosoftAppTenantId"] ?? throw new InvalidOperationException("MicrosoftAppTenantId is required");
            _logger.LogInformation("Tenant ID: {TenantId}", tenantId);

            var managedIdentityClientId = configuration["AIFoundry:ManagedIdentityClientId"];
            _logger.LogInformation("Managed Identity Client ID: {ManagedIdentityClientId}", managedIdentityClientId ?? "Not specified");

            var opts = new DefaultAzureCredentialOptions { TenantId = tenantId };
            
            // Add managed identity client ID if specified
            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                opts.ManagedIdentityClientId = managedIdentityClientId;
                _logger.LogInformation("Using user-assigned managed identity with client ID: {ClientId}", managedIdentityClientId);
            }

            var credential = new DefaultAzureCredential(opts);
            _logger.LogInformation("Using default Azure credential");

            try
            {
                _client = new PersistentAgentsClient(projectEndpoint, credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create PersistentAgentsClient. If using managed identity, ensure it has proper permissions to access AI Foundry resources.");
                throw;
            }

            // Check if agent already exists or create a new one
            _agent = GetAgent() ?? CreateAgent();

            _logger.LogInformation("AI Foundry Agent Service initialized with agent ID: {AgentId}", _agent.Id);
        }

        private PersistentAgent? GetAgent()
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve agent with name: {AgentName}", _agentName);
                // Get all existing agents
                var existingAgents = _client.Administration.GetAgents();

                // Look for an agent with the same name
                var existingAgent = existingAgents.FirstOrDefault(a => a.Name == _agentName);

                if (existingAgent != null)
                {
                    _logger.LogInformation("Found existing agent with name '{AgentName}' and ID: {AgentId}", _agentName, existingAgent.Id);
                    return existingAgent;
                }

                _logger.LogInformation("No existing agent found with name '{AgentName}'", _agentName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get agent.");
                // It's better to let the caller handle the exception or decide on fallback logic.
                // In this case, we'll let it bubble up or return null if we want to attempt creation.
                return null;
            }
        }

        private PersistentAgent CreateAgent()
        {
            try
            {
                // No existing agent found, create a new one
                _logger.LogInformation("Creating new agent with name '{AgentName}'", _agentName);

                // Read instructions from prompty file
                string instructions = "";
                try
                {
                    var promptyContent = File.ReadAllText("instructions.prompty");
                    var lines = promptyContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    var systemLines = lines
                        .SkipWhile(line => !line.Trim().StartsWith("system:"))
                        .Skip(1) // Skip the "system:" line itself
                        .TakeWhile(line => !line.Trim().StartsWith("user:"))
                        .ToList();

                    if (systemLines.Any())
                    {
                        instructions = string.Join(Environment.NewLine, systemLines).Trim();
                        _logger.LogInformation("Successfully parsed and extracted system instructions from instructions.prompty");
                    }
                    else
                    {
                        _logger.LogWarning("Could not find 'system:' block in instructions.prompty. Creating agent without instructions.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read or parse instructions.prompty. Creating agent without instructions.");
                }

                var newAgentResponse = _client.Administration.CreateAgent(
                    model: _modelDeploymentName,
                    name: _agentName,
                    instructions: instructions);

                _logger.LogInformation("Successfully created new agent with ID: {AgentId}", newAgentResponse.Value.Id);
                return newAgentResponse.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create agent.");
                throw;
            }
        }

        public async Task<string> ProcessMessageAsync(string userMessage, string conversationId)
        {
            try
            {
                // Get or create thread for this conversation
                var thread = _threads.GetOrAdd(conversationId, _ => _client.Threads.CreateThread());

                // Create message in thread
                var messageResponse = _client.Messages.CreateMessage(
                    thread.Id,
                    MessageRole.User,
                    userMessage);

                // Run the agent
                var runResponse = _client.Runs.CreateRun(thread, _agent);
                var run = runResponse.Value;

                // Wait for completion
                do
                {
                    await Task.Delay(500);
                    var runStatusResponse = _client.Runs.GetRun(thread.Id, run.Id);
                    run = runStatusResponse.Value;
                }
                while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

                if (run.Status == RunStatus.Failed)
                {
                    _logger.LogError("Agent run failed for conversation {ConversationId}: {Error}", conversationId, run.LastError?.Message);
                    return "I'm sorry, I encountered an error processing your request. Please try again.";
                }

                // Get all messages and find the assistant's response
                var messages = _client.Messages.GetMessages(
                    threadId: thread.Id,
                    order: ListSortOrder.Descending);

                // Get the most recent non-user message (which should be the assistant's response)
                foreach (var message in messages)
                {
                    if (message.Role != MessageRole.User)
                    {
                        var response = string.Empty;
                        foreach (var contentItem in message.ContentItems)
                        {
                            if (contentItem is MessageTextContent textItem)
                            {
                                response += textItem.Text;
                            }
                        }
                        if (!string.IsNullOrEmpty(response))
                        {
                            return response;
                        }
                    }
                }

                return "I'm sorry, I couldn't generate a response. Please try again.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message for conversation {ConversationId}", conversationId);
                return "I'm sorry, I encountered an error processing your request. Please try again.";
            }
        }

        public async Task CleanupConversationAsync(string conversationId)
        {
            if (_threads.TryRemove(conversationId, out var thread))
            {
                try
                {
                    _client.Threads.DeleteThread(thread.Id);
                    _logger.LogInformation("Cleaned up thread for conversation {ConversationId}", conversationId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up thread for conversation {ConversationId}", conversationId);
                }
            }
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            // Cleanup all threads
            foreach (var kvp in _threads)
            {
                try
                {
                    _client.Threads.DeleteThread(kvp.Value.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up thread {ThreadId} during disposal", kvp.Value.Id);
                }
            }

            // Delete the agent
            try
            {
                _client.Administration.DeleteAgent(_agent.Id);
                _logger.LogInformation("Disposed AI Foundry Agent Service and cleaned up agent {AgentId}", _agent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up agent {AgentId} during disposal", _agent.Id);
            }
        }
    }
}