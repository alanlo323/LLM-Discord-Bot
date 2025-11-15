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
public class ChatCommands(
    LLMService llmService,
    TokenControlService tokenControl,
    IRepository repository,
    UserRequestQueueService requestQueue,
    ILogger logger) : InteractionModuleBase<SocketInteractionContext>
{

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
        
        // Track start time for interaction timeout detection (15 min limit)
        var startTime = DateTime.UtcNow;
        const int interactionTimeoutMinutes = 14; // Use 14 min to be safe

        // Acquire user-specific lock to serialize requests and prevent race conditions
        using var userLock = await requestQueue.AcquireUserLockAsync(userId);

        try
        {
            logger.Information("User {Username} ({UserId}) sent chat message in channel {ChannelId}, guild {GuildId}",
                Context.User.Username, userId, channelId, guildId);

            // Build chat history first (with guild context for SystemPrompt)
            var chatHistory = await llmService.BuildChatHistoryAsync(userId, channelId, message, guildId);

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
                await FollowupAsync(
                    embed: new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("❌ Token 額度不足")
                        .WithDescription($"您今天的 Token 額度已用完。\n\n" +
                                       $"已使用: {used:N0} / {limit:N0}\n" +
                                       $"本次請求預估: {estimatedTotalTokens:N0} tokens")
                        .WithFooter("額度將在每日 00:00 UTC 重置")
                        .Build(),
                    ephemeral: true);
                return;
            }

            // Stream LLM response with real-time updates (with guild context for MaxTokens)
            var responseBuilder = new System.Text.StringBuilder();
            var reasoningBuilder = new System.Text.StringBuilder();
            var lastUpdateTime = DateTime.UtcNow;
            var lastReasoningUpdateTime = DateTime.UtcNow;
            int? promptTokens = null;
            int? completionTokens = null;
            var hasContent = false;
            Discord.IUserMessage? reasoningMessage = null;
            var interactionTimedOut = false;

            await foreach (var (content, reasoning, pTokens, cTokens) in llmService.GetChatCompletionStreamingAsync(chatHistory, guildId, reasoningEffort))
            {
                // Check if we're approaching interaction timeout
                if ((DateTime.UtcNow - startTime).TotalMinutes >= interactionTimeoutMinutes)
                {
                    interactionTimedOut = true;
                    logger.Warning("Interaction approaching timeout for user {UserId}, switching to followup", userId);
                }

                // Handle reasoning content
                if (!string.IsNullOrEmpty(reasoning) && reasoning != reasoningBuilder.ToString())
                {
                    reasoningBuilder.Clear();
                    reasoningBuilder.Append(reasoning);

                    var now = DateTime.UtcNow;
                    // Update reasoning message (rate limit: every 1 second)
                    if ((now - lastReasoningUpdateTime).TotalSeconds >= 1.0 || reasoningMessage == null)
                    {
                        lastReasoningUpdateTime = now;
                        
                        // Truncate if too long (Discord embed description limit is 4096)
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
                                // Send initial reasoning message
                                reasoningMessage = await FollowupAsync(embed: reasoningEmbed);
                            }
                            else if (!interactionTimedOut)
                            {
                                // Update existing reasoning message only if not timed out
                                await reasoningMessage.ModifyAsync(msg =>
                                {
                                    msg.Content = null;
                                    msg.Embed = reasoningEmbed;
                                });
                            }
                            else
                            {
                                logger.Debug("Skipping reasoning message update due to interaction timeout");
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log but don't stop streaming if update fails
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

                    // Update Discord message every 1 second
                    var now = DateTime.UtcNow;
                    if ((now - lastUpdateTime).TotalSeconds >= 1.0)
                    {
                        lastUpdateTime = now;
                        var currentContent = responseBuilder.ToString();

                        // Truncate if too long for streaming display (Discord embed description limit 4096)
                        var displayContent = SafeTruncate(currentContent, 1900);

                        var streamingEmbed = new EmbedBuilder()
                            .WithColor(Color.Blue)
                            .WithAuthor(Context.User.Username, Context.User.GetAvatarUrl())
                            .WithTitle($"💬 {SafeTruncate(message, 100)}")
                            .WithDescription(displayContent)
                            .WithFooter("正在生成回應...")
                            .Build();

                        try
                        {
                            // Skip updating original response if we've timed out
                            if (interactionTimedOut)
                            {
                                logger.Debug("Skipping streaming update due to interaction timeout");
                            }
                            else
                            {
                                await ModifyOriginalResponseAsync(msg =>
                                {
                                    msg.Content = null;
                                    msg.Embed = streamingEmbed;
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log but don't stop streaming if update fails
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
                // Use accurate calculation if no token data available
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
            if (reasoningMessage != null && reasoningBuilder.Length > 0 && !interactionTimedOut)
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
            else if (interactionTimedOut && reasoningMessage != null && reasoningBuilder.Length > 0)
            {
                logger.Debug("Skipping final reasoning message update due to interaction timeout");
            }

            // Record token usage
            await tokenControl.RecordTokenUsageAsync(userId, totalTokens, guildId);

            // Save chat history
            await repository.AddChatHistoryAsync(new Models.ChatHistory
            {
                UserId = userId,
                GuildId = guildId,
                ChannelId = channelId,
                Role = "user",
                Content = message,
                TokenCount = promptTokens ?? 0,
                Timestamp = DateTime.UtcNow
            });

            await repository.AddChatHistoryAsync(new Models.ChatHistory
            {
                UserId = userId,
                GuildId = guildId,
                ChannelId = channelId,
                Role = "assistant",
                Content = response,
                TokenCount = completionTokens ?? 0,
                Timestamp = DateTime.UtcNow
            });

            // Update final message with complete response
            // Split response if too long (Discord limit is 2000 characters per message)
            if (response.Length <= 1900)
            {
                var footerText = interactionTimedOut
                    ? $"使用 {totalTokens:N0} tokens | 今日已使用 {used + totalTokens:N0} / {limit:N0} | 因處理時間較長，以新消息回覆"
                    : $"使用 {totalTokens:N0} tokens | 今日已使用 {used + totalTokens:N0} / {limit:N0}";

                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithAuthor(Context.User.Username, Context.User.GetAvatarUrl())
                    .WithTitle($"💬 {SafeTruncate(message, 100)}")
                    .WithDescription(response)
                    .WithFooter(footerText)
                    .Build();

                if (interactionTimedOut)
                {
                    // Use followup if timed out
                    await FollowupAsync(embed: embed);
                }
                else
                {
                    // Use modify if not timed out
                    await ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Content = null; // Clear the "thinking" text
                        msg.Embed = embed;
                    });
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

                    // Add user info and question only to first chunk
                    if (i == 0)
                    {
                        embedBuilder
                            .WithAuthor(Context.User.Username, Context.User.GetAvatarUrl())
                            .WithTitle($"💬 {SafeTruncate(message, 100)}");
                    }

                    if (i == chunks.Count - 1)
                    {
                        var footerText = interactionTimedOut
                            ? $"使用 {totalTokens:N0} tokens | 今日已使用 {used + totalTokens:N0} / {limit:N0} | 因處理時間較長，以新消息回覆"
                            : $"使用 {totalTokens:N0} tokens | 今日已使用 {used + totalTokens:N0} / {limit:N0}";
                        embedBuilder.WithFooter(footerText);
                    }

                    // First chunk updates the original deferred message (unless timed out), subsequent chunks use followup
                    if (i == 0 && !interactionTimedOut)
                    {
                        await ModifyOriginalResponseAsync(msg =>
                        {
                            msg.Content = null; // Clear the "thinking" text
                            msg.Embed = embedBuilder.Build();
                        });
                    }
                    else
                    {
                        await FollowupAsync(embed: embedBuilder.Build());
                    }
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

                // If a single line is too long, split it safely
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

    /// <summary>
    /// Safely truncate string without breaking UTF-16 surrogate pairs
    /// </summary>
    private string SafeTruncate(string text, int maxLength, string ellipsis = "...")
    {
        if (text.Length <= maxLength)
            return text;

        // Account for ellipsis
        var targetLength = maxLength - ellipsis.Length;
        if (targetLength <= 0)
            return ellipsis;

        // Check if we're cutting in the middle of a surrogate pair
        if (targetLength > 0 && char.IsHighSurrogate(text[targetLength - 1]))
        {
            targetLength--;
        }

        return text.Substring(0, targetLength) + ellipsis;
    }

    /// <summary>
    /// Safely extract substring without breaking UTF-16 surrogate pairs
    /// </summary>
    private string SafeSubstring(string text, int startIndex, int length)
    {
        if (startIndex >= text.Length)
            return string.Empty;

        var endIndex = Math.Min(startIndex + length, text.Length);

        // Adjust start if it's in the middle of a surrogate pair
        if (startIndex > 0 && char.IsLowSurrogate(text[startIndex]))
        {
            startIndex--;
        }

        // Adjust end if it's in the middle of a surrogate pair
        if (endIndex < text.Length && char.IsHighSurrogate(text[endIndex - 1]))
        {
            endIndex--;
        }

        return text.Substring(startIndex, endIndex - startIndex);
    }
}

