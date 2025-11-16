using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using System.Reflection;
using System.Text.RegularExpressions;
using Serilog;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDiscordBot.Services;

/// <summary>
/// Service for handling Discord slash commands and message mentions
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
        client.MessageReceived += HandleMessageReceivedAsync;
        interactionService.SlashCommandExecuted += SlashCommandExecutedAsync;

        logger.Information("Command handler service initialized");
    }

    /// <summary>
    /// Register slash commands. This should be called after the client is ready.
    /// </summary>
    public async Task RegisterCommandsAsync()
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

    private async Task HandleMessageReceivedAsync(SocketMessage socketMessage)
    {
        // Ignore system messages
        if (socketMessage is not SocketUserMessage message)
            return;

        // Ignore bot messages
        if (message.Author.IsBot)
            return;

        // Check if bot is mentioned
        if (!message.MentionedUsers.Any(u => u.Id == client?.CurrentUser?.Id))
            return;

        try
        {
            logger.Information("Bot mentioned by {Username} ({UserId}) in message: {Content}",
                message.Author.Username, message.Author.Id, message.Content);

            // Parse the message to extract reasoning effort and actual content
            var (cleanedMessage, reasoningEffort) = ParseMessageContent(message.Content);

            // Validate cleaned message
            if (string.IsNullOrWhiteSpace(cleanedMessage))
            {
                await message.ReplyAsync("請提供訊息內容。");
                return;
            }

            // Process the chat request using ChatProcessorService
            using var scope = services.CreateScope();
            var chatProcessor = scope.ServiceProvider.GetRequiredService<ChatProcessorService>();

            var userId = message.Author.Id;
            var channelId = message.Channel.Id;
            var guildId = (message.Channel as SocketGuildChannel)?.Guild?.Id;
            var username = message.Author.Username;
            var avatarUrl = message.Author.GetAvatarUrl();

            IUserMessage? currentMessage = null;
            var channelName = (message.Channel as SocketGuildChannel)?.Name ?? message.Channel.Name;
            var guildName = (message.Channel as SocketGuildChannel)?.Guild?.Name;
            var startTime = DateTime.UtcNow;

            await chatProcessor.ProcessChatRequestAsync(
                userId,
                channelId,
                guildId,
                username,
                avatarUrl,
                cleanedMessage,
                reasoningEffort,
                channelName,
                guildName,
                isSlashCommand: false,
                startTime,
                // sendInitialResponse
                async (content, embed) =>
                {
                    if (currentMessage == null)
                    {
                        currentMessage = await message.ReplyAsync(text: content, embed: embed);
                    }
                    return currentMessage;
                },
                // updateResponse
                async (modifyAction) =>
                {
                    if (currentMessage != null)
                    {
                        await currentMessage.ModifyAsync(modifyAction);
                    }
                },
                // sendFollowup
                async (embed) =>
                {
                    return await message.Channel.SendMessageAsync(embed: embed);
                }
            );
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling message mention");
            try
            {
                await message.ReplyAsync("處理您的請求時發生錯誤，請稍後再試。");
            }
            catch (Exception replyEx)
            {
                logger.Error(replyEx, "Failed to send error reply");
            }
        }
    }

    /// <summary>
    /// Parse message content to extract reasoning effort parameter and clean message
    /// Format: @Bot [low|medium|high] your message here
    /// </summary>
    private (string cleanedMessage, string reasoningEffort) ParseMessageContent(string content)
    {
        // Remove bot mentions
        var cleanedContent = Regex.Replace(content, @"<@!?\d+>", "").Trim();

        // Extract reasoning effort if present
        var reasoningMatch = Regex.Match(cleanedContent, @"\[(low|medium|high)\]", RegexOptions.IgnoreCase);
        var reasoningEffort = "medium"; // default

        if (reasoningMatch.Success)
        {
            reasoningEffort = reasoningMatch.Groups[1].Value.ToLower();
            // Remove only the first matched reasoning effort tag from the message
            cleanedContent = cleanedContent.Remove(reasoningMatch.Index, reasoningMatch.Length).Trim();
        }

        return (cleanedContent, reasoningEffort);
    }
}

