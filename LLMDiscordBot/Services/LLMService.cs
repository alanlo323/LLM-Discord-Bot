using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Options;
using LLMDiscordBot.Configuration;
using LLMDiscordBot.Data;
using LLMDiscordBot.Plugins;
using Serilog;
using SharpToken;

namespace LLMDiscordBot.Services;

/// <summary>
/// Service for interacting with LLM through Semantic Kernel
/// </summary>
public class LLMService
{
    private readonly Kernel kernel;
    private readonly IChatCompletionService chatService;
    private readonly ILogger logger;
    private readonly LLMConfig config;
    private readonly IRepository repository;
    private readonly GptEncoding? tokenEncoding;

    public LLMService(
        IOptions<LLMConfig> config,
        ILogger logger,
        IRepository repository,
        TavilySearchPlugin tavilySearchPlugin)
    {
        this.config = config.Value;
        this.logger = logger;
        this.repository = repository;

        // Build kernel with OpenAI-compatible endpoint
        var builder = Kernel.CreateBuilder();
        
        builder.AddOpenAIChatCompletion(
            modelId: this.config.Model,
            apiKey: "not-needed", // LM Studio doesn't require an API key
            endpoint: new Uri(this.config.ApiEndpoint)
        );

        this.kernel = builder.Build();
        
        // Register Tavily search plugin
        this.kernel.Plugins.AddFromObject(tavilySearchPlugin, "TavilySearch");
        this.logger.Information("Tavily search plugin registered to kernel");
        
        this.chatService = this.kernel.GetRequiredService<IChatCompletionService>();

        // Initialize SharpToken encoding for accurate token counting
        try
        {
            // Use cl100k_base encoding (GPT-4, GPT-3.5-turbo, and most modern models)
            this.tokenEncoding = GptEncoding.GetEncoding("cl100k_base");
            this.logger.Information("SharpToken encoding initialized successfully");
        }
        catch (Exception ex)
        {
            this.logger.Warning(ex, "Failed to initialize SharpToken encoding, will use estimation fallback");
            this.tokenEncoding = null;
        }

        this.logger.Information("LLM Service initialized with endpoint: {Endpoint}", this.config.ApiEndpoint);
    }

    /// <summary>
    /// Get chat completion from LLM
    /// </summary>
    public async Task<(string response, int promptTokens, int completionTokens)> GetChatCompletionAsync(
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory chatHistory,
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        // Apply default timeout of 5 minutes if no cancellation token provided
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        try
        {
            // Get settings from database if available
            var modelSetting = await repository.GetSettingAsync("Model");
            var temperatureSetting = await repository.GetSettingAsync("Temperature");
            var globalMaxTokensSetting = await repository.GetSettingAsync("GlobalMaxTokens");

            var temperature = double.TryParse(temperatureSetting, out var temp) ? temp : config.Temperature;
            var maxTokens = int.TryParse(globalMaxTokensSetting, out var max) ? max : config.MaxTokens;

            // Check for guild-specific MaxTokens override
            if (guildId.HasValue)
            {
                var guildSettings = await repository.GetGuildSettingsAsync(guildId.Value);
                if (guildSettings?.MaxTokens.HasValue == true)
                {
                    maxTokens = Math.Min(maxTokens, guildSettings.MaxTokens.Value);
                }
            }

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = temperature,
                MaxTokens = maxTokens,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            logger.Debug("Sending request to LLM with {MessageCount} messages (Auto function calling enabled)", chatHistory.Count);

            var result = await chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                kernel,
                linkedCts.Token);

            // Extract token usage from metadata
            var promptTokens = 0;
            var completionTokens = 0;

            if (result != null && result.Count > 0)
            {
                var lastMessage = result[result.Count - 1];
                if (lastMessage.Metadata != null &&
                    lastMessage.Metadata.TryGetValue("Usage", out var usageObj))
                {
                    logger.Debug("Usage object type: {Type}", usageObj?.GetType().FullName ?? "null");
                    
                    // Try to parse usage information
                    var usageDict = usageObj as IDictionary<string, object>;
                    if (usageDict != null)
                    {
                        // Dictionary approach (for some API implementations)
                        if (usageDict.TryGetValue("prompt_tokens", out var pt))
                            promptTokens = Convert.ToInt32(pt);
                        else if (usageDict.TryGetValue("PromptTokens", out var pt2))
                            promptTokens = Convert.ToInt32(pt2);

                        if (usageDict.TryGetValue("completion_tokens", out var ct))
                            completionTokens = Convert.ToInt32(ct);
                        else if (usageDict.TryGetValue("CompletionTokens", out var ct2))
                            completionTokens = Convert.ToInt32(ct2);
                    }
                    else if (usageObj != null)
                    {
                        // Use reflection to get properties (for OpenAI.Chat.ChatTokenUsage and similar types)
                        var usageType = usageObj.GetType();
                        
                        // Try to get InputTokenCount or PromptTokens
                        var inputTokenProp = usageType.GetProperty("InputTokenCount") 
                            ?? usageType.GetProperty("PromptTokens")
                            ?? usageType.GetProperty("prompt_tokens");
                        if (inputTokenProp != null)
                        {
                            var value = inputTokenProp.GetValue(usageObj);
                            if (value != null)
                                promptTokens = Convert.ToInt32(value);
                        }
                        
                        // Try to get OutputTokenCount or CompletionTokens
                        var outputTokenProp = usageType.GetProperty("OutputTokenCount")
                            ?? usageType.GetProperty("CompletionTokens")
                            ?? usageType.GetProperty("completion_tokens");
                        if (outputTokenProp != null)
                        {
                            var value = outputTokenProp.GetValue(usageObj);
                            if (value != null)
                                completionTokens = Convert.ToInt32(value);
                        }
                        
                        logger.Debug("Token extraction via reflection - Prompt: {PromptTokens}, Completion: {CompletionTokens}", 
                            promptTokens, completionTokens);
                    }
                    else
                    {
                        logger.Warning("Usage metadata is null");
                    }
                }
                else
                {
                    logger.Warning("No Usage metadata found in LLM response");
                }

                var responseContent = string.Join("", result.Select(r => r.Content));
                logger.Information("LLM response received. Prompt tokens: {PromptTokens}, Completion tokens: {CompletionTokens}",
                    promptTokens, completionTokens);

                return (responseContent ?? "", promptTokens, completionTokens);
            }

            return ("", 0, 0);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            logger.Error("LLM request timed out after 5 minutes");
            throw new TimeoutException("LLM request timed out after 5 minutes");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting chat completion from LLM");
            throw;
        }
    }

    /// <summary>
    /// Get chat completion from LLM with streaming support
    /// </summary>
    public async IAsyncEnumerable<(string content, string? reasoning, int? promptTokens, int? completionTokens)> GetChatCompletionStreamingAsync(
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory chatHistory,
        ulong? guildId = null,
        string? reasoningEffort = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Apply default timeout of 10 minutes for streaming (longer than non-streaming)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        IAsyncEnumerator<(string content, string? reasoning, int? promptTokens, int? completionTokens)>? enumerator = null;
        
        try
        {
            enumerator = GetChatCompletionStreamingInternalAsync(chatHistory, guildId, reasoningEffort, linkedCts.Token).GetAsyncEnumerator(linkedCts.Token);
            
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    logger.Error("LLM streaming request timed out after 10 minutes");
                    throw new TimeoutException("LLM streaming request timed out after 10 minutes");
                }
                
                if (!hasNext)
                    break;
                    
                yield return enumerator.Current;
            }
        }
        finally
        {
            if (enumerator != null)
            {
                await enumerator.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Internal method for streaming chat completion
    /// </summary>
    private async IAsyncEnumerable<(string content, string? reasoning, int? promptTokens, int? completionTokens)> GetChatCompletionStreamingInternalAsync(
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory chatHistory,
        ulong? guildId,
        string? reasoningEffort,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int? promptTokens = null;
        int? completionTokens = null;
        string? reasoning = null;

        // Get settings from database if available
        var modelSetting = await repository.GetSettingAsync("Model");
        var temperatureSetting = await repository.GetSettingAsync("Temperature");
        var globalMaxTokensSetting = await repository.GetSettingAsync("GlobalMaxTokens");

        var temperature = double.TryParse(temperatureSetting, out var temp) ? temp : config.Temperature;
        var maxTokens = int.TryParse(globalMaxTokensSetting, out var max) ? max : config.MaxTokens;

        // Check for guild-specific MaxTokens override
        if (guildId.HasValue)
        {
            var guildSettings = await repository.GetGuildSettingsAsync(guildId.Value);
            if (guildSettings?.MaxTokens.HasValue == true)
            {
                maxTokens = Math.Min(maxTokens, guildSettings.MaxTokens.Value);
            }
        }

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = temperature,
            MaxTokens = maxTokens,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        // Add reasoning_effort parameter if specified
        if (!string.IsNullOrEmpty(reasoningEffort))
        {
            executionSettings.ExtensionData = new Dictionary<string, object>
            {
                ["reasoning_effort"] = reasoningEffort
            };
            logger.Debug("Added reasoning_effort parameter: {ReasoningEffort}", reasoningEffort);
        }

        logger.Debug("Sending streaming request to LLM with {MessageCount} messages (Auto function calling enabled)", chatHistory.Count);

        await foreach (var message in chatService.GetStreamingChatMessageContentsAsync(
            chatHistory,
            executionSettings,
            kernel,
            cancellationToken))
        {
            // Try to extract metadata if available
            if (message.Metadata != null)
            {
                // Extract token usage
                if (message.Metadata.TryGetValue("Usage", out var usageObj))
                {
                    var usageDict = usageObj as IDictionary<string, object>;
                    if (usageDict != null)
                    {
                        if (usageDict.TryGetValue("prompt_tokens", out var pt))
                            promptTokens = Convert.ToInt32(pt);
                        else if (usageDict.TryGetValue("PromptTokens", out var pt2))
                            promptTokens = Convert.ToInt32(pt2);

                        if (usageDict.TryGetValue("completion_tokens", out var ct))
                            completionTokens = Convert.ToInt32(ct);
                        else if (usageDict.TryGetValue("CompletionTokens", out var ct2))
                            completionTokens = Convert.ToInt32(ct2);
                    }
                    else if (usageObj != null)
                    {
                        var usageType = usageObj.GetType();
                        
                        var inputTokenProp = usageType.GetProperty("InputTokenCount") 
                            ?? usageType.GetProperty("PromptTokens")
                            ?? usageType.GetProperty("prompt_tokens");
                        if (inputTokenProp != null)
                        {
                            var value = inputTokenProp.GetValue(usageObj);
                            if (value != null)
                                promptTokens = Convert.ToInt32(value);
                        }
                        
                        var outputTokenProp = usageType.GetProperty("OutputTokenCount")
                            ?? usageType.GetProperty("CompletionTokens")
                            ?? usageType.GetProperty("completion_tokens");
                        if (outputTokenProp != null)
                        {
                            var value = outputTokenProp.GetValue(usageObj);
                            if (value != null)
                                completionTokens = Convert.ToInt32(value);
                        }
                    }
                }

                // Extract reasoning content if available
                if (message.Metadata.TryGetValue("Reasoning", out var reasoningObj))
                {
                    reasoning = reasoningObj?.ToString();
                    if (!string.IsNullOrEmpty(reasoning))
                    {
                        logger.Debug("Received reasoning content: {Length} characters", reasoning.Length);
                    }
                }
                else if (message.Metadata.TryGetValue("reasoning", out var reasoningObj2))
                {
                    reasoning = reasoningObj2?.ToString();
                    if (!string.IsNullOrEmpty(reasoning))
                    {
                        logger.Debug("Received reasoning content: {Length} characters", reasoning.Length);
                    }
                }
            }

            yield return (message.Content ?? "", reasoning, promptTokens, completionTokens);
        }

        logger.Information("LLM streaming response completed. Prompt tokens: {PromptTokens}, Completion tokens: {CompletionTokens}",
            promptTokens ?? 0, completionTokens ?? 0);
    }

    /// <summary>
    /// Calculate accurate token count using SharpToken
    /// </summary>
    public int CalculateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        try
        {
            if (tokenEncoding != null)
            {
                // Use SharpToken for accurate counting
                var tokens = tokenEncoding.Encode(text);
                return tokens.Count;
            }
            else
            {
                // Fallback to estimation if SharpToken is not available
                logger.Debug("Using fallback estimation for token count");
                return EstimateTokenCount(text);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error calculating token count, using estimation fallback");
            return EstimateTokenCount(text);
        }
    }

    /// <summary>
    /// Estimate token count for a message (simple approximation)
    /// </summary>
    public int EstimateTokenCount(string text)
    {
        // Simple estimation: ~4 characters per token
        // This is a rough approximation; for accurate counts, use a proper tokenizer
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    /// <summary>
    /// Build chat history from database records
    /// </summary>
    public async Task<Microsoft.SemanticKernel.ChatCompletion.ChatHistory> BuildChatHistoryAsync(
        ulong userId,
        ulong channelId,
        string userMessage,
        ulong? guildId = null,
        int historyCount = 10,
        string? personalizedPromptAddition = null,
        string? username = null,
        string? guildName = null,
        string? channelName = null)
    {
        var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();

        // Add system message: GlobalSystemPrompt first, then Guild SystemPrompt, then User personalization
        var globalSystemPrompt = await repository.GetSettingAsync("GlobalSystemPrompt") ?? config.SystemPrompt;
        var systemPrompt = globalSystemPrompt;

        // Append guild-specific system prompt if available
        if (guildId.HasValue)
        {
            var guildSettings = await repository.GetGuildSettingsAsync(guildId.Value);
            if (guildSettings?.SystemPrompt != null && !string.IsNullOrWhiteSpace(guildSettings.SystemPrompt))
            {
                systemPrompt = $"{globalSystemPrompt}\n\n{guildSettings.SystemPrompt}";
            }
        }

        // Append user-specific personalized prompt if available
        if (!string.IsNullOrEmpty(personalizedPromptAddition))
        {
            systemPrompt = $"{systemPrompt}\n\n{personalizedPromptAddition}";
        }

        chatHistory.AddSystemMessage(systemPrompt);

        // Add context information as a separate system message
        var contextInfo = await BuildContextInformationAsync(userId, channelId, guildId, username, guildName, channelName);
        if (!string.IsNullOrEmpty(contextInfo))
        {
            chatHistory.AddSystemMessage(contextInfo);
        }

        // Add recent conversation history
        var history = await repository.GetRecentChatHistoryAsync(userId, channelId, historyCount);
        foreach (var item in history)
        {
            if (item.Role.ToLower() == "user")
                chatHistory.AddUserMessage(item.Content);
            else
                chatHistory.AddAssistantMessage(item.Content);
        }

        // Add current user message
        chatHistory.AddUserMessage(userMessage);

        return chatHistory;
    }

    /// <summary>
    /// Build context information string for the LLM
    /// </summary>
    private async Task<string> BuildContextInformationAsync(
        ulong userId,
        ulong channelId,
        ulong? guildId,
        string? username,
        string? guildName,
        string? channelName)
    {
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("=== Context Information ===");
        
        // Current time
        var currentTime = DateTime.UtcNow;
        contextBuilder.AppendLine($"Current Time: {currentTime:yyyy-MM-dd HH:mm:ss} UTC");
        
        // User information
        if (!string.IsNullOrEmpty(username))
        {
            contextBuilder.AppendLine($"Current User: {username} (ID: {userId})");
        }
        else
        {
            contextBuilder.AppendLine($"Current User ID: {userId}");
        }
        
        // Server information
        if (guildId.HasValue)
        {
            if (!string.IsNullOrEmpty(guildName))
            {
                contextBuilder.AppendLine($"Server: {guildName} (ID: {guildId.Value})");
            }
            else
            {
                contextBuilder.AppendLine($"Server ID: {guildId.Value}");
            }
        }
        else
        {
            contextBuilder.AppendLine("Server: Direct Message");
        }
        
        // Channel information
        if (!string.IsNullOrEmpty(channelName))
        {
            contextBuilder.AppendLine($"Channel: {channelName} (ID: {channelId})");
        }
        else
        {
            contextBuilder.AppendLine($"Channel ID: {channelId}");
        }
        
        // Get recent channel messages (all users)
        var channelHistory = await repository.GetChannelRecentChatHistoryAsync(channelId, 10);
        if (channelHistory.Any())
        {
            contextBuilder.AppendLine();
            contextBuilder.AppendLine($"Recent Channel Messages (last {channelHistory.Count}):");
            
            foreach (var msg in channelHistory)
            {
                var timestamp = msg.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                var role = msg.Role.ToLower() == "user" ? "User" : "Bot";
                var userIdStr = msg.UserId.ToString();
                
                // Truncate long messages for context
                var content = msg.Content.Length > 100 
                    ? msg.Content.Substring(0, 97) + "..." 
                    : msg.Content;
                
                // Replace newlines in content to keep it compact
                content = content.Replace("\n", " ").Replace("\r", "");
                
                contextBuilder.AppendLine($"[{timestamp}] {role} {userIdStr}: {content}");
            }
        }
        
        contextBuilder.AppendLine("=========================");
        
        return contextBuilder.ToString();
    }

    /// <summary>
    /// Apply user preferences to execution settings
    /// </summary>
    public async Task<OpenAIPromptExecutionSettings> ApplyUserPreferencesToSettingsAsync(
        OpenAIPromptExecutionSettings baseSettings,
        ulong userId,
        ulong? guildId = null)
    {
        var preferences = await repository.GetUserPreferencesAsync(userId);
        if (preferences == null)
            return baseSettings;

        // Clone settings
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = preferences.PreferredTemperature ?? baseSettings.Temperature,
            MaxTokens = preferences.PreferredMaxTokens ?? baseSettings.MaxTokens,
            ToolCallBehavior = baseSettings.ToolCallBehavior,
            ExtensionData = baseSettings.ExtensionData
        };

        // Guild settings take precedence over user preferences for MaxTokens
        if (guildId.HasValue)
        {
            var guildSettings = await repository.GetGuildSettingsAsync(guildId.Value);
            if (guildSettings?.MaxTokens.HasValue == true && settings.MaxTokens.HasValue)
            {
                settings.MaxTokens = Math.Min(settings.MaxTokens.Value, guildSettings.MaxTokens.Value);
            }
        }

        return settings;
    }
}

