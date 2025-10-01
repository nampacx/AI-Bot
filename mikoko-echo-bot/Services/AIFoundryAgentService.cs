using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EchoBot.Services
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

            // Get configuration values
            _modelDeploymentName = configuration["AIFoundry:ModelDeploymentName"] ?? throw new InvalidOperationException("AIFoundry:ModelDeploymentName is required");
            _agentName = configuration["AIFoundry:AgentName"] ?? throw new InvalidOperationException("AIFoundry:AgentName is required");


            // Create the client
            var projectEndpoint = configuration["AIFoundry:ProjectEndpoint"] ?? throw new InvalidOperationException("AIFoundry:ProjectEndpoint is required");
            var tenantId = configuration["MicrosoftAppTenantId"] ?? throw new InvalidOperationException("MicrosoftAppTenantId is required");
            var opts = new DefaultAzureCredentialOptions { TenantId = tenantId };
            _client = new PersistentAgentsClient(projectEndpoint, new DefaultAzureCredential(opts));

            // Check if agent already exists or create a new one
            _agent = GetOrCreateAgent();

            _logger.LogInformation("AI Foundry Agent Service initialized with agent ID: {AgentId}", _agent.Id);
        }

        private PersistentAgent GetOrCreateAgent()
        {
            try
            {
                // Get all existing agents
                var existingAgents = _client.Administration.GetAgents();
                
                // Look for an agent with the same name
                var existingAgent = existingAgents.FirstOrDefault(a => a.Name == _agentName);
                
                if (existingAgent != null)
                {
                    _logger.LogInformation("Found existing agent with name '{AgentName}' and ID: {AgentId}", _agentName, existingAgent.Id);
                    return existingAgent;
                }
                
                // No existing agent found, create a new one
                _logger.LogInformation("No existing agent found with name '{AgentName}', creating new agent", _agentName);
                var newAgentResponse = _client.Administration.CreateAgent(
                    model: _modelDeploymentName,
                    name: _agentName,
                    instructions: "You are a helpful assistant integrated into a chatbot. Provide helpful, accurate, and conversational responses to user questions. Keep responses concise but informative.",
                    tools: new List<ToolDefinition> { new CodeInterpreterToolDefinition() });
                
                var newAgent = newAgentResponse.Value;
                _logger.LogInformation("Created new agent with ID: {AgentId}", newAgent.Id);
                return newAgent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for existing agent or creating new agent");
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