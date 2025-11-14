using Discord;
using Discord.Interactions;
using LLMDiscordBot.Services;
using LLMDiscordBot.Data;
using LLMDiscordBot.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using Serilog;

namespace LLMDiscordBot.Commands;

/// <summary>
/// Chat commands for interacting with the LLM
/// </summary>
public class ChatCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly LLMService llmService;
    private readonly TokenControlService tokenControl;
    private readonly IRepository repository;
    private readonly ILogger logger;

    public ChatCommands(
        LLMService llmService,
        TokenControlService tokenControl,
        IRepository repository,
        ILogger logger)
    {
        this.llmService = llmService;
        this.tokenControl = tokenControl;
        this.repository = repository;
        this.logger = logger;
    }

    [SlashCommand("chat", "與 LLM 聊天")]
    public async Task ChatAsync(
        [Summary("message", "您想說的話")]
        string message)
    {
        try
        {
            // Defer response as LLM might take time
            await DeferAsync();

            var userId = Context.User.Id;
            var channelId = Context.Channel.Id;

            logger.Information("User {Username} ({UserId}) sent chat message in channel {ChannelId}",
                Context.User.Username, userId, channelId);

            // Estimate tokens needed for the message
            var estimatedTokens = llmService.EstimateTokenCount(message) + 500; // Add buffer for response

            // Check token limit
            var (allowed, used, limit) = await tokenControl.CheckTokenLimitAsync(userId, estimatedTokens);
            if (!allowed)
            {
                await FollowupAsync(
                    embed: new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("❌ Token 額度不足")
                        .WithDescription($"您今天的 Token 額度已用完。\n\n" +
                                       $"已使用: {used:N0} / {limit:N0}")
                        .WithFooter("額度將在每日 00:00 UTC 重置")
                        .Build(),
                    ephemeral: true);
                return;
            }

            // Build chat history
            var chatHistory = await llmService.BuildChatHistoryAsync(userId, channelId, message);

            // Show typing indicator (via followup)
            var thinkingMessage = await FollowupAsync("🤔 正在思考中...");

            // Get LLM response
            var (response, promptTokens, completionTokens) = await llmService.GetChatCompletionAsync(chatHistory);
            var totalTokens = promptTokens + completionTokens;

            // Record token usage
            await tokenControl.RecordTokenUsageAsync(userId, totalTokens);

            // Save chat history
            await repository.AddChatHistoryAsync(new Models.ChatHistory
            {
                UserId = userId,
                ChannelId = channelId,
                Role = "user",
                Content = message,
                TokenCount = promptTokens,
                Timestamp = DateTime.UtcNow
            });

            await repository.AddChatHistoryAsync(new Models.ChatHistory
            {
                UserId = userId,
                ChannelId = channelId,
                Role = "assistant",
                Content = response,
                TokenCount = completionTokens,
                Timestamp = DateTime.UtcNow
            });

            // Delete thinking message
            await thinkingMessage.DeleteAsync();

            // Split response if too long (Discord limit is 2000 characters per message)
            if (response.Length <= 1900)
            {
                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithDescription(response)
                    .WithFooter($"使用 {totalTokens:N0} tokens | 今日已使用 {used + totalTokens:N0} / {limit:N0}")
                    .Build();

                await FollowupAsync(embed: embed);
            }
            else
            {
                // Split into multiple messages
                var chunks = SplitMessage(response, 1900);
                for (int i = 0; i < chunks.Count; i++)
                {
                    var embedBuilder = new EmbedBuilder()
                        .WithColor(Color.Blue)
                        .WithDescription(chunks[i]);

                    if (i == chunks.Count - 1)
                    {
                        embedBuilder.WithFooter($"使用 {totalTokens:N0} tokens | 今日已使用 {used + totalTokens:N0} / {limit:N0}");
                    }

                    await FollowupAsync(embed: embedBuilder.Build());
                }
            }

            logger.Information("Chat response sent to user {UserId}. Tokens: {Tokens}", userId, totalTokens);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error in chat command");
            await FollowupAsync(
                embed: new EmbedBuilder()
                    .WithColor(Color.Red)
                    .WithTitle("❌ 錯誤")
                    .WithDescription("處理您的請求時發生錯誤，請稍後再試。")
                    .Build(),
                ephemeral: true);
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

    private List<string> SplitMessage(string message, int maxLength)
    {
        var chunks = new List<string>();
        var lines = message.Split('\n');
        var currentChunk = "";

        foreach (var line in lines)
        {
            if (currentChunk.Length + line.Length + 1 > maxLength)
            {
                if (!string.IsNullOrEmpty(currentChunk))
                {
                    chunks.Add(currentChunk);
                    currentChunk = "";
                }

                // If a single line is too long, split it
                if (line.Length > maxLength)
                {
                    for (int i = 0; i < line.Length; i += maxLength)
                    {
                        chunks.Add(line.Substring(i, Math.Min(maxLength, line.Length - i)));
                    }
                }
                else
                {
                    currentChunk = line + "\n";
                }
            }
            else
            {
                currentChunk += line + "\n";
            }
        }

        if (!string.IsNullOrEmpty(currentChunk))
        {
            chunks.Add(currentChunk);
        }

        return chunks;
    }
}

