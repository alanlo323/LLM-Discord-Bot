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

        // Acquire user-specific lock to serialize requests and prevent race conditions
        using var userLock = await requestQueue.AcquireUserLockAsync(userId);

        try
        {
            logger.Information("User {Username} ({UserId}) sent chat message in channel {ChannelId}, guild {GuildId}",
                Context.User.Username, userId, channelId, guildId);

            // Estimate tokens needed for the message
            var estimatedTokens = llmService.EstimateTokenCount(message) + 500; // Add buffer for response

            // Check token limit (with guild context)
            var (allowed, used, limit) = await tokenControl.CheckTokenLimitAsync(userId, estimatedTokens, guildId);
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

            // Build chat history (with guild context for SystemPrompt)
            var chatHistory = await llmService.BuildChatHistoryAsync(userId, channelId, message, guildId);

            // Stream LLM response with real-time updates (with guild context for MaxTokens)
            var responseBuilder = new System.Text.StringBuilder();
            var reasoningBuilder = new System.Text.StringBuilder();
            var lastUpdateTime = DateTime.UtcNow;
            var lastReasoningUpdateTime = DateTime.UtcNow;
            int? promptTokens = null;
            int? completionTokens = null;
            var hasContent = false;
            Discord.IUserMessage? reasoningMessage = null;

            await foreach (var (content, reasoning, pTokens, cTokens) in llmService.GetChatCompletionStreamingAsync(chatHistory, guildId, reasoningEffort))
            {
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
                        
                        // Truncate if too long (Discord embed limit is 4096)
                        var displayReasoning = reasoning.Length > 4000 
                            ? reasoning.Substring(0, 4000) + "..." 
                            : reasoning;

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
                            else
                            {
                                // Update existing reasoning message
                                await reasoningMessage.ModifyAsync(msg =>
                                {
                                    msg.Content = null;
                                    msg.Embed = reasoningEmbed;
                                });
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

                        // Truncate if too long for streaming display (Discord embed limit)
                        var displayContent = currentContent.Length > 1900 
                            ? currentContent.Substring(0, 1900) + "..." 
                            : currentContent;

                        var streamingEmbed = new EmbedBuilder()
                            .WithColor(Color.Blue)
                            .WithAuthor(Context.User.Username, Context.User.GetAvatarUrl())
                            .WithTitle($"💬 {(message.Length > 100 ? message.Substring(0, 100) + "..." : message)}")
                            .WithDescription(displayContent)
                            .WithFooter("正在生成回應...")
                            .Build();

                        try
                        {
                            await ModifyOriginalResponseAsync(msg =>
                            {
                                msg.Content = null;
                                msg.Embed = streamingEmbed;
                            });
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
            if (reasoningMessage != null && reasoningBuilder.Length > 0)
            {
                try
                {
                    var finalReasoning = reasoningBuilder.ToString();
                    var displayReasoning = finalReasoning.Length > 4000 
                        ? finalReasoning.Substring(0, 4000) + "..." 
                        : finalReasoning;

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
                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithAuthor(Context.User.Username, Context.User.GetAvatarUrl())
                    .WithTitle($"💬 {(message.Length > 100 ? string.Concat(message.AsSpan(0, 100), "...") : message)}")
                    .WithDescription(response)
                    .WithFooter($"使用 {totalTokens:N0} tokens | 今日已使用 {used + totalTokens:N0} / {limit:N0}")
                    .Build();

                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = null; // Clear the "thinking" text
                    msg.Embed = embed;
                });
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
                            .WithTitle($"💬 {(message.Length > 100 ? message.Substring(0, 100) + "..." : message)}");
                    }

                    if (i == chunks.Count - 1)
                    {
                        embedBuilder.WithFooter($"使用 {totalTokens:N0} tokens | 今日已使用 {used + totalTokens:N0} / {limit:N0}");
                    }

                    // First chunk updates the original deferred message, subsequent chunks use followup
                    if (i == 0)
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

