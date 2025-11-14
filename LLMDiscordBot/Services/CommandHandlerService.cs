using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using System.Reflection;
using Serilog;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDiscordBot.Services;

/// <summary>
/// Service for handling Discord slash commands
/// </summary>
public class CommandHandlerService
{
    private readonly IServiceProvider services;
    private readonly InteractionService interactionService;
    private readonly ILogger logger;
    private DiscordSocketClient? client;

    public CommandHandlerService(IServiceProvider services, ILogger logger)
    {
        this.services = services;
        this.logger = logger;
        this.interactionService = new InteractionService(services.GetRequiredService<DiscordSocketClient>());
    }

    public async Task InitializeAsync(DiscordSocketClient client)
    {
        this.client = client;
        
        // Add modules
        await interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), services);

        // Hook events
        client.InteractionCreated += HandleInteractionAsync;
        interactionService.SlashCommandExecuted += SlashCommandExecutedAsync;
        client.Ready += RegisterCommandsAsync;

        logger.Information("Command handler service initialized");
    }

    private async Task RegisterCommandsAsync()
    {
        try
        {
            // Register commands globally (takes up to 1 hour to update)
            // For testing, you can use RegisterCommandsToGuildAsync with a guild ID
            await interactionService.RegisterCommandsGloballyAsync();
            logger.Information("Slash commands registered globally");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to register slash commands");
        }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(client!, interaction);
            await interactionService.ExecuteCommandAsync(context, services);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling interaction");

            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                var message = "發生錯誤，請稍後再試。";
                if (interaction.HasResponded)
                {
                    await interaction.FollowupAsync(text: message, ephemeral: true);
                }
                else
                {
                    await interaction.RespondAsync(text: message, ephemeral: true);
                }
            }
        }
    }

    private Task SlashCommandExecutedAsync(SlashCommandInfo commandInfo, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            logger.Warning("Command {CommandName} failed: {Error}", 
                commandInfo.Name, result.ErrorReason);
        }
        else
        {
            logger.Information("User {Username} executed command {CommandName}", 
                context.User.Username, commandInfo.Name);
        }

        return Task.CompletedTask;
    }
}

