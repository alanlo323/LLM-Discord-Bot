using Discord;
using Discord.Interactions;
using LLMDiscordBot.Services;
using LLMDiscordBot.Data;
using Serilog;

namespace LLMDiscordBot.Commands;

/// <summary>
/// Chat commands for interacting with the LLM
/// </summary>
public class ChatCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ChatProcessorService chatProcessor;
    private readonly IRepository repository;
    private readonly ILogger logger;

    public ChatCommands(
        ChatProcessorService chatProcessor,
        IRepository repository,
        ILogger logger)
    {
        this.chatProcessor = chatProcessor;
        this.repository = repository;
        this.logger = logger;
    }

    [SlashCommand("chat", "與 LLM 聊天")]
    public async Task ChatAsync(
        [Summary("message", "您想說的話")]
        string message,
        [Summary("reasoning-effort", "推理深度（預設：medium）")]
        [Choice("low", "low")]
        [Choice("medium", "medium")]
        [Choice("high", "high")]
        string reasoningEffort = "medium")
    {
        // Defer response as LLM might take time
        await DeferAsync();

        var userId = Context.User.Id;
        var channelId = Context.Channel.Id;
        var guildId = Context.Guild?.Id;
        var username = Context.User.Username;
        var avatarUrl = Context.User.GetAvatarUrl();
        var channelName = Context.Channel.Name;
        var guildName = Context.Guild?.Name;
        var startTime = DateTime.UtcNow;

        try
        {
            await chatProcessor.ProcessChatRequestAsync(
                userId,
                channelId,
                guildId,
                username,
                avatarUrl,
                message,
                reasoningEffort,
                channelName,
                guildName,
                isSlashCommand: true,
                startTime,
                // sendInitialResponse - not used for slash commands (already deferred)
                async (content, embed) =>
                {
                    // This shouldn't be called for slash commands, but implement anyway
                    return await FollowupAsync(text: content, embed: embed);
                },
                // updateResponse
                async (modifyAction) =>
                {
                    await ModifyOriginalResponseAsync(modifyAction);
                },
                // sendFollowup
                async (embed) =>
                {
                    return await FollowupAsync(embed: embed);
                });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error in chat command");
            try
            {
                await FollowupAsync(
                    embed: new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("❌ 錯誤")
                        .WithDescription("處理您的請求時發生錯誤，請稍後再試。")
                        .Build(),
                    ephemeral: true);
            }
            catch (Exception followupEx)
            {
                logger.Error(followupEx, "Failed to send error followup");
            }
        }
    }

    [SlashCommand("clearchat", "清除您在此頻道的聊天記錄")]
    public async Task ClearChatAsync()
    {
        try
        {
            var userId = Context.User.Id;
            var channelId = Context.Channel.Id;

            await repository.ClearChatHistoryAsync(userId, channelId);

            await RespondAsync(
                embed: new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 聊天記錄已清除")
                    .WithDescription("您在此頻道的聊天記錄已被清除。")
                    .Build(),
                ephemeral: true);

            logger.Information("User {UserId} cleared chat history in channel {ChannelId}", userId, channelId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error clearing chat history");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }
}

