using Discord;
using Discord.WebSocket;
using LLMDiscordBot.Data;
using LLMDiscordBot.Models;
using Serilog;
using System.Text;

namespace LLMDiscordBot.Services;

/// <summary>
/// Service for processing chat requests from both slash commands and message mentions
/// </summary>
public class ChatProcessorService
{
    private readonly LLMService llmService;
    private readonly TokenControlService tokenControl;
    private readonly IRepository repository;
    private readonly UserRequestQueueService requestQueue;
    private readonly ILogger logger;

    public ChatProcessorService(
        LLMService llmService,
        TokenControlService tokenControl,
        IRepository repository,
        UserRequestQueueService requestQueue,
        ILogger logger)
    {
        this.llmService = llmService;
        this.tokenControl = tokenControl;
        this.repository = repository;
        this.requestQueue = requestQueue;
        this.logger = logger;
    }

    /// <summary>
    /// Process a chat request and stream the response
    /// </summary>
    public async Task ProcessChatRequestAsync(
        ulong userId,
        ulong channelId,
        ulong? guildId,
        string username,
        string? avatarUrl,
        string message,
        string reasoningEffort,
        Func<string, Embed?, Task<IUserMessage>> sendInitialResponse,
        Func<Action<MessageProperties>, Task> updateResponse,
        Func<Embed, Task<IUserMessage>> sendFollowup)
    {
        // Acquire user-specific lock to serialize requests and prevent race conditions
        using var userLock = await requestQueue.AcquireUserLockAsync(userId);

        try
        {
            logger.Information("User {Username} ({UserId}) sent chat message in channel {ChannelId}, guild {GuildId}",
                username, userId, channelId, guildId);

            // Note: ChatProcessorService already receives username, guildName would need to be passed from caller
            // For now, we pass null for guildName and channelName as they're not available in the current method signature
            
            // Build chat history first (with guild context for SystemPrompt)
            var chatHistory = await llmService.BuildChatHistoryAsync(userId, channelId, message, guildId, 10, null, username, null, null);

            // Calculate accurate prompt tokens using SharpToken
            int estimatedPromptTokens = 0;
            foreach (var historyMessage in chatHistory)
            {
                estimatedPromptTokens += llmService.CalculateTokenCount(historyMessage.Content ?? "");
            }

            // Get MaxTokens setting to estimate response size
            var maxTokensSetting = await repository.GetSettingAsync("GlobalMaxTokens");
            var maxTokens = int.TryParse(maxTokensSetting, out var max) ? max : 2000;
            
            if (guildId.HasValue)
            {
                var guildSettings = await repository.GetGuildSettingsAsync(guildId.Value);
                if (guildSettings?.MaxTokens.HasValue == true)
                {
                    maxTokens = Math.Min(maxTokens, guildSettings.MaxTokens.Value);
                }
            }

            // Estimate total tokens (prompt + expected response)
            var estimatedTotalTokens = estimatedPromptTokens + maxTokens;

            // Check token limit (with guild context)
            var (allowed, used, limit) = await tokenControl.CheckTokenLimitAsync(userId, estimatedTotalTokens, guildId);
            if (!allowed)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithColor(Color.Red)
                    .WithTitle("❌ Token 額度不足")
                    .WithDescription($"您今天的 Token 額度已用完。\n\n" +
                                   $"已使用: {used:N0} / {limit:N0}\n" +
                                   $"本次請求預估: {estimatedTotalTokens:N0} tokens")
                    .WithFooter("額度將在每日 00:00 UTC 重置")
                    .Build();
                
                await sendFollowup(errorEmbed);
                return;
            }

            // Stream LLM response with real-time updates
            var responseBuilder = new StringBuilder();
            var reasoningBuilder = new StringBuilder();
            var lastUpdateTime = DateTime.UtcNow;
            var lastReasoningUpdateTime = DateTime.UtcNow;
            int? promptTokens = null;
            int? completionTokens = null;
            var hasContent = false;
            IUserMessage? reasoningMessage = null;
            IUserMessage? mainMessage = null;

            await foreach (var (content, reasoning, pTokens, cTokens) in llmService.GetChatCompletionStreamingAsync(chatHistory, guildId, reasoningEffort))
            {
                // Handle reasoning content
                if (!string.IsNullOrEmpty(reasoning) && reasoning != reasoningBuilder.ToString())
                {
                    reasoningBuilder.Clear();
                    reasoningBuilder.Append(reasoning);

                    var now = DateTime.UtcNow;
                    // Update reasoning message (rate limit: every 2 seconds)
                    if ((now - lastReasoningUpdateTime).TotalSeconds >= 2.0 || reasoningMessage == null)
                    {
                        lastReasoningUpdateTime = now;
                        
                        var displayReasoning = SafeTruncate(reasoning, 4090);

                        var reasoningEmbed = new EmbedBuilder()
                            .WithColor(Color.Purple)
                            .WithTitle("🧠 推理過程")
                            .WithDescription(displayReasoning)
                            .WithFooter("正在推理...")
                            .Build();

                        try
                        {
                            if (reasoningMessage == null)
                            {
                                reasoningMessage = await sendFollowup(reasoningEmbed);
                            }
                            else
                            {
                                await reasoningMessage.ModifyAsync(msg =>
                                {
                                    msg.Content = null;
                                    msg.Embed = reasoningEmbed;
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Warning(ex, "Failed to update reasoning message");
                        }
                    }
                }

                if (!string.IsNullOrEmpty(content))
                {
                    responseBuilder.Append(content);
                    hasContent = true;

                    // Update token counts if available
                    if (pTokens.HasValue) promptTokens = pTokens.Value;
                    if (cTokens.HasValue) completionTokens = cTokens.Value;

                    // Update Discord message every 2 seconds
                    var now = DateTime.UtcNow;
                    if ((now - lastUpdateTime).TotalSeconds >= 2.0)
                    {
                        lastUpdateTime = now;
                        var currentContent = responseBuilder.ToString();

                        var displayContent = SafeTruncate(currentContent, 1900);

                        var streamingEmbed = new EmbedBuilder()
                            .WithColor(Color.Blue)
                            .WithAuthor(username, avatarUrl)
                            .WithTitle($"💬 {SafeTruncate(message, 100)}")
                            .WithDescription(displayContent)
                            .WithFooter("正在生成回應...")
                            .Build();

                        try
                        {
                            if (mainMessage == null)
                            {
                                mainMessage = await sendInitialResponse(null!, streamingEmbed);
                            }
                            else
                            {
                                await updateResponse(msg =>
                                {
                                    msg.Content = null;
                                    msg.Embed = streamingEmbed;
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Warning(ex, "Failed to update streaming message");
                        }
                    }
                }
            }

            var response = responseBuilder.ToString();

            // If no content was received, handle error
            if (!hasContent || string.IsNullOrEmpty(response))
            {
                logger.Warning("No content received from LLM streaming");
                response = "抱歉，無法生成回應。請稍後再試。";
                promptTokens = llmService.CalculateTokenCount(message);
                completionTokens = 0;
            }
            else
            {
                // Calculate tokens accurately if API didn't return usage info
                if (!promptTokens.HasValue)
                {
                    promptTokens = llmService.CalculateTokenCount(message);
                    logger.Information("Calculated prompt tokens (API didn't provide): {Tokens}", promptTokens.Value);
                }

                if (!completionTokens.HasValue)
                {
                    completionTokens = llmService.CalculateTokenCount(response);
                    logger.Information("Calculated completion tokens (API didn't provide): {Tokens}", completionTokens.Value);
                }
            }

            var totalTokens = (promptTokens ?? 0) + (completionTokens ?? 0);

            // Update final reasoning message if it exists
            if (reasoningMessage != null && reasoningBuilder.Length > 0)
            {
                try
                {
                    var finalReasoning = reasoningBuilder.ToString();
                    var displayReasoning = SafeTruncate(finalReasoning, 4090);

                    var finalReasoningEmbed = new EmbedBuilder()
                        .WithColor(Color.Purple)
                        .WithTitle("🧠 推理過程")
                        .WithDescription(displayReasoning)
                        .WithFooter($"推理完成 | 使用 reasoning_effort: {reasoningEffort}")
                        .Build();

                    await reasoningMessage.ModifyAsync(msg =>
                    {
                        msg.Content = null;
                        msg.Embed = finalReasoningEmbed;
                    });
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to update final reasoning message");
                }
            }

            // Record token usage
            await tokenControl.RecordTokenUsageAsync(userId, totalTokens, guildId);

            // Save chat history
            await repository.AddChatHistoryAsync(new ChatHistory
            {
                UserId = userId,
                GuildId = guildId,
                ChannelId = channelId,
                Role = "user",
                Content = message,
                TokenCount = promptTokens ?? 0,
                Timestamp = DateTime.UtcNow
            });

            await repository.AddChatHistoryAsync(new ChatHistory
            {
                UserId = userId,
                GuildId = guildId,
                ChannelId = channelId,
                Role = "assistant",
                Content = response,
                TokenCount = completionTokens ?? 0,
                Timestamp = DateTime.UtcNow
            });

            // Send final response
            await SendFinalResponseAsync(
                response,
                message,
                username,
                avatarUrl,
                totalTokens,
                used,
                limit,
                mainMessage,
                sendInitialResponse,
                updateResponse,
                sendFollowup);

            logger.Information("Chat response sent to user {UserId}. Tokens: {Tokens}", userId, totalTokens);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error processing chat request");
            
            var errorEmbed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle("❌ 錯誤")
                .WithDescription("處理您的請求時發生錯誤，請稍後再試。")
                .Build();
            
            await sendFollowup(errorEmbed);
        }
    }

    private async Task SendFinalResponseAsync(
        string response,
        string originalMessage,
        string username,
        string? avatarUrl,
        int totalTokens,
        int usedTokens,
        int limitTokens,
        IUserMessage? existingMessage,
        Func<string, Embed?, Task<IUserMessage>> sendInitialResponse,
        Func<Action<MessageProperties>, Task> updateResponse,
        Func<Embed, Task<IUserMessage>> sendFollowup)
    {
        // Split response if too long
        if (response.Length <= 1900)
        {
            var embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithAuthor(username, avatarUrl)
                .WithTitle($"💬 {SafeTruncate(originalMessage, 100)}")
                .WithDescription(response)
                .WithFooter($"使用 {totalTokens:N0} tokens | 今日已使用 {usedTokens + totalTokens:N0} / {limitTokens:N0}")
                .Build();

            if (existingMessage != null)
            {
                await updateResponse(msg =>
                {
                    msg.Content = null;
                    msg.Embed = embed;
                });
            }
            else
            {
                await sendInitialResponse(null!, embed);
            }
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

                if (i == 0)
                {
                    embedBuilder
                        .WithAuthor(username, avatarUrl)
                        .WithTitle($"💬 {SafeTruncate(originalMessage, 100)}");
                }

                if (i == chunks.Count - 1)
                {
                    embedBuilder.WithFooter($"使用 {totalTokens:N0} tokens | 今日已使用 {usedTokens + totalTokens:N0} / {limitTokens:N0}");
                }

                if (i == 0 && existingMessage != null)
                {
                    await updateResponse(msg =>
                    {
                        msg.Content = null;
                        msg.Embed = embedBuilder.Build();
                    });
                }
                else
                {
                    await sendFollowup(embedBuilder.Build());
                }
            }
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

                if (line.Length > maxLength)
                {
                    for (int i = 0; i < line.Length; i += maxLength)
                    {
                        chunks.Add(SafeSubstring(line, i, Math.Min(maxLength, line.Length - i)));
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

    private string SafeTruncate(string text, int maxLength, string ellipsis = "...")
    {
        if (text.Length <= maxLength)
            return text;

        var targetLength = maxLength - ellipsis.Length;
        if (targetLength <= 0)
            return ellipsis;

        if (targetLength > 0 && char.IsHighSurrogate(text[targetLength - 1]))
        {
            targetLength--;
        }

        return text.Substring(0, targetLength) + ellipsis;
    }

    private string SafeSubstring(string text, int startIndex, int length)
    {
        if (startIndex >= text.Length)
            return string.Empty;

        var endIndex = Math.Min(startIndex + length, text.Length);

        if (startIndex > 0 && char.IsLowSurrogate(text[startIndex]))
        {
            startIndex--;
        }

        if (endIndex < text.Length && char.IsHighSurrogate(text[endIndex - 1]))
        {
            endIndex--;
        }

        return text.Substring(startIndex, endIndex - startIndex);
    }
}

