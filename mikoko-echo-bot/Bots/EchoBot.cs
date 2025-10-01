// Generated with EchoBot .NET Template version v4.22.0

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using EchoBot.Services;
using Microsoft.Extensions.Logging;

namespace EchoBot.Bots
{
    public class EchoBot : ActivityHandler
    {
        private readonly IAIFoundryAgentService _agentService;
        private readonly ILogger<EchoBot> _logger;

        public EchoBot(IAIFoundryAgentService agentService, ILogger<EchoBot> logger)
        {
            _agentService = agentService;
            _logger = logger;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var userMessage = turnContext.Activity.Text;
            var conversationId = turnContext.Activity.Conversation.Id;

            _logger.LogInformation("Processing message from conversation {ConversationId}: {Message}", conversationId, userMessage);

            try
            {
                // Use AI Foundry agent to process the message
                var response = await _agentService.ProcessMessageAsync(userMessage, conversationId);
                await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from conversation {ConversationId}", conversationId);
                var errorResponse = "I'm sorry, I encountered an error processing your request. Please try again.";
                await turnContext.SendActivityAsync(MessageFactory.Text(errorResponse), cancellationToken);
            }
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and welcome! I'm an AI assistant powered by Azure AI Foundry. How can I help you today?";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }

        protected override async Task OnEndOfConversationActivityAsync(ITurnContext<IEndOfConversationActivity> turnContext, CancellationToken cancellationToken)
        {
            var conversationId = turnContext.Activity.Conversation.Id;
            _logger.LogInformation("End of conversation for {ConversationId}, cleaning up resources", conversationId);
            
            // Clean up the conversation thread
            await _agentService.CleanupConversationAsync(conversationId);
            
            await base.OnEndOfConversationActivityAsync(turnContext, cancellationToken);
        }
    }
}
