using Discord;
using Discord.WebSocket;
using LLMDiscordBot.Data;
using LLMDiscordBot.Models;
using LLMDiscordBot.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text;

namespace LLMDiscordBot.Services;

/// <summary>
/// Service for processing chat requests from both slash commands and message mentions
/// </summary>
public class ChatProcessorService(
    LLMService llmService,
    TokenControlService tokenControl,
    IRepository repository,
    UserRequestQueueService requestQueue,
    HabitLearningService habitLearning,
    GraphMemoryService graphMemoryService,
    MemoryExtractionBackgroundService memoryExtractionService,
    IOptions<GraphRagConfig> graphRagConfig,
    ILogger logger)
{
    private readonly MemoryExtractionConfig memoryConfig = graphRagConfig.Value.MemoryExtraction;

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
        string? channelName,
        string? guildName,
        bool isSlashCommand,
        DateTime startTime,
        Func<string, Embed?, Task<IUserMessage>> sendInitialResponse,
        Func<Action<MessageProperties>, Task> updateResponse,
        Func<Embed, Task<IUserMessage>> sendFollowup)
    {
        // Acquire user-specific lock to serialize requests and prevent race conditions
        using var userLock = await requestQueue.AcquireUserLockAsync(userId);
        
        // Track interaction timeout for slash commands (15 min limit)
        const int interactionTimeoutMinutes = 14; // Use 14 min to be safe
        var interactionTimedOut = false;

        try
        {
            logger.Information("User {Username} ({UserId}) sent chat message in channel {ChannelId}, guild {GuildId}",
                username, userId, channelId, guildId);

            // Detect topic category for habit learning
            var topicCategory = habitLearning.DetectTopicCategory(message);

            // Check if memory retrieval is needed and get relevant memories
            string? memoryContext = null;
            try
            {
                var shouldRetrieve = await graphMemoryService.ShouldRetrieveMemoryAsync(message, userId, guildId);
                if (shouldRetrieve)
                {
                    logger.Debug("Retrieving relevant memories for user {UserId}", userId);
                    memoryContext = await graphMemoryService.SearchRelevantMemoriesAsync(userId, guildId, message);
                    
                    if (!string.IsNullOrWhiteSpace(memoryContext))
                    {
                        logger.Information("Retrieved memory context for user {UserId}, length: {Length}", 
                            userId, memoryContext.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error retrieving memories, continuing without memory context");
            }

            // Get personalized prompt addition based on user preferences
            var globalSystemPrompt = await repository.GetSettingAsync("GlobalSystemPrompt") ?? "You are a helpful AI assistant.";
            var personalizedPrompt = await habitLearning.BuildPersonalizedPromptAsync(userId, globalSystemPrompt);
            var promptAddition = personalizedPrompt.StartsWith(globalSystemPrompt) 
                ? personalizedPrompt[globalSystemPrompt.Length..].TrimStart() 
                : personalizedPrompt;

            // Combine memory context with personalized prompt
            var combinedPromptAddition = promptAddition;
            if (!string.IsNullOrWhiteSpace(memoryContext))
            {
                var memorySection = $"\n\n=== Relevant Memories ===\n{memoryContext}\n" +
                    "Use these memories to provide more personalized and context-aware responses.";
                combinedPromptAddition = string.IsNullOrEmpty(combinedPromptAddition) 
                    ? memorySection 
                    : combinedPromptAddition + memorySection;
            }
            
            // Build chat history (with guild context for SystemPrompt, user personalization, and memory context)
            var chatHistory = await llmService.BuildChatHistoryAsync(
                userId, channelId, message, guildId, 10, 
                string.IsNullOrEmpty(combinedPromptAddition) ? null : combinedPromptAddition,
                username, guildName, channelName);

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
                // Check if we're approaching interaction timeout (slash commands only)
                if (isSlashCommand && (DateTime.UtcNow - startTime).TotalMinutes >= interactionTimeoutMinutes)
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
                            else if (!interactionTimedOut)
                            {
                                // Only update if not timed out
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
                            else if (!interactionTimedOut)
                            {
                                // Only update if not timed out
                                await updateResponse(msg =>
                                {
                                    msg.Content = null;
                                    msg.Embed = streamingEmbed;
                                });
                            }
                            else
                            {
                                logger.Debug("Skipping streaming update due to interaction timeout");
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

            // Queue memory extraction for background processing BEFORE saving current chat
            // This ensures we get previous history without the current conversation
            try
            {
                // Get recent chat history from database (before adding current conversation)
                var historyCount = memoryConfig.HistoryMessagesCount;
                var recentHistory = await repository.GetRecentChatHistoryAsync(userId, channelId, historyCount);
                
                // Convert ChatHistory to ChatMessage list
                var conversationForMemory = recentHistory
                    .OrderBy(h => h.Timestamp)
                    .Select(h => new ChatMessage 
                    { 
                        Role = h.Role, 
                        Content = h.Content 
                    })
                    .ToList();
                
                // Add current conversation to the history
                conversationForMemory.Add(new ChatMessage { Role = "user", Content = message });
                conversationForMemory.Add(new ChatMessage { Role = "assistant", Content = response });

                logger.Debug("Preparing memory extraction with {Count} messages for user {UserId} (history: {HistoryCount}, current: 2)", 
                    conversationForMemory.Count, userId, recentHistory.Count);

                memoryExtractionService.QueueMemoryExtraction(new MemoryExtractionTask
                {
                    UserId = userId,
                    GuildId = guildId,
                    RecentConversation = conversationForMemory
                });

                logger.Debug("Queued memory extraction for user {UserId} in guild {GuildId}", userId, guildId);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error queueing memory extraction, continuing");
            }

            // Save chat history (after queuing memory extraction)
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
                interactionTimedOut,
                sendInitialResponse,
                updateResponse,
                sendFollowup);

            logger.Information("Chat response sent to user {UserId}. Tokens: {Tokens}", userId, totalTokens);

            // Learn from this interaction and get suggestions
            var interactionTime = DateTime.UtcNow - startTime;
            try
            {
                await habitLearning.LearnFromInteractionAsync(
                    userId,
                    guildId,
                    "chat",
                    message,
                    response,
                    interactionTime,
                    topicCategory);
                    
                // Get and display smart suggestions (if any) - based on updated habits
                var suggestions = await habitLearning.GetSmartSuggestionsAsync(userId);
                if (suggestions.Count > 0)
                {
                    var suggestionText = string.Join("\n", suggestions);
                    await sendFollowup(new EmbedBuilder()
                        .WithColor(Color.Gold)
                        .WithTitle("💡 智慧建議")
                        .WithDescription(suggestionText)
                        .Build());
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to learn from interaction or get suggestions");
            }
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

    private static async Task SendFinalResponseAsync(
        string response,
        string originalMessage,
        string username,
        string? avatarUrl,
        int totalTokens,
        int usedTokens,
        int limitTokens,
        IUserMessage? existingMessage,
        bool interactionTimedOut,
        Func<string, Embed?, Task<IUserMessage>> sendInitialResponse,
        Func<Action<MessageProperties>, Task> updateResponse,
        Func<Embed, Task<IUserMessage>> sendFollowup)
    {
        // Split response if too long
        if (response.Length <= 1900)
        {
            var footerText = interactionTimedOut
                ? $"使用 {totalTokens:N0} tokens | 今日已使用 {usedTokens + totalTokens:N0} / {limitTokens:N0} | 因處理時間較長，以新消息回覆"
                : $"使用 {totalTokens:N0} tokens | 今日已使用 {usedTokens + totalTokens:N0} / {limitTokens:N0}";

            var embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithAuthor(username, avatarUrl)
                .WithTitle($"💬 {SafeTruncate(originalMessage, 100)}")
                .WithDescription(response)
                .WithFooter(footerText)
                .Build();

            if (interactionTimedOut)
            {
                // Use followup if timed out
                await sendFollowup(embed);
            }
            else if (existingMessage != null)
            {
                // Use modify if not timed out
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
                    var footerText = interactionTimedOut
                        ? $"使用 {totalTokens:N0} tokens | 今日已使用 {usedTokens + totalTokens:N0} / {limitTokens:N0} | 因處理時間較長，以新消息回覆"
                        : $"使用 {totalTokens:N0} tokens | 今日已使用 {usedTokens + totalTokens:N0} / {limitTokens:N0}";
                    embedBuilder.WithFooter(footerText);
                }

                // First chunk updates the existing message (unless timed out), subsequent chunks use followup
                if (i == 0 && !interactionTimedOut && existingMessage != null)
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

    private static List<string> SplitMessage(string message, int maxLength)
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

    private static string SafeTruncate(string text, int maxLength, string ellipsis = "...")
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

        return text[..targetLength] + ellipsis;
    }

    private static string SafeSubstring(string text, int startIndex, int length)
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

        return text[startIndex..endIndex];
    }
}

