using ConsoleAgentClient.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ConsoleAgentClient
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Build service provider
            var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .AddLogging(builder =>
                {
                    builder.AddConfiguration(configuration.GetSection("Logging"));
                    builder.AddConsole();
                })
                .AddSingleton<IAIFoundryAgentService, AIFoundryAgentService>()
                .BuildServiceProvider();

            var logger = services.GetRequiredService<ILogger<Program>>();
            var agentService = services.GetRequiredService<IAIFoundryAgentService>();

            try
            {
                logger.LogInformation("Starting Console Agent Client...");
                
                // Welcome message
                Console.WriteLine("=== AI Foundry Console Agent Client ===");
                Console.WriteLine("Type your messages and press Enter to send them to the agent.");
                Console.WriteLine("Type 'exit' or 'quit' to end the conversation.");
                Console.WriteLine("Type 'clear' to start a new conversation.");
                Console.WriteLine("Type 'help' for more commands.");
                Console.WriteLine();

                string conversationId = Guid.NewGuid().ToString();
                logger.LogInformation("Started new conversation with ID: {ConversationId}", conversationId);

                while (true)
                {
                    // Get user input
                    Console.Write("You: ");
                    var input = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    // Handle special commands
                    switch (input.ToLower().Trim())
                    {
                        case "exit":
                        case "quit":
                            Console.WriteLine("Goodbye!");
                            await agentService.CleanupConversationAsync(conversationId);
                            return;

                        case "clear":
                            await agentService.CleanupConversationAsync(conversationId);
                            conversationId = Guid.NewGuid().ToString();
                            Console.WriteLine("Started new conversation.");
                            logger.LogInformation("Started new conversation with ID: {ConversationId}", conversationId);
                            continue;

                        case "help":
                            ShowHelp();
                            continue;
                    }

                    // Process the message
                    try
                    {
                        Console.WriteLine("Agent: Thinking...");
                        
                        var response = await agentService.ProcessMessageAsync(input, conversationId);
                        
                        // Clear the "Thinking..." line and show the response
                        Console.SetCursorPosition(0, Console.CursorTop - 1);
                        Console.WriteLine($"Agent: {response}");
                        Console.WriteLine();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing user message");
                        Console.WriteLine($"Agent: I'm sorry, I encountered an error: {ex.Message}");
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fatal error occurred");
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            finally
            {
                // Cleanup
                if (agentService is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                services.Dispose();
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            Console.WriteLine("  exit, quit - Exit the application");
            Console.WriteLine("  clear      - Start a new conversation");
            Console.WriteLine("  help       - Show this help message");
            Console.WriteLine();
            Console.WriteLine("Configuration:");
            Console.WriteLine("  Edit appsettings.json to configure your AI Foundry connection:");
            Console.WriteLine("  - MicrosoftAppTenantId: Your Azure tenant ID");
            Console.WriteLine("  - AIFoundry:ProjectEndpoint: Your AI Foundry project endpoint");
            Console.WriteLine("  - AIFoundry:ModelDeploymentName: Your model deployment name");
            Console.WriteLine("  - AIFoundry:AgentName: Name for your agent instance");
            Console.WriteLine();
        }
    }
}