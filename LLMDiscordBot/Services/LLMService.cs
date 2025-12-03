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
    private readonly List<ChatClientDescriptor> chatClients = new();
    private readonly ChatClientDescriptor actionGuardClient;
    private readonly ChatClientDescriptor? taskClient;
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

        // Build primary Fara-7B client plus optional fallbacks
        var primaryEndpoint = new LLMEndpointConfig
        {
            ApiEndpoint = this.config.ApiEndpoint,
            ApiKey = this.config.ApiKey,
            Model = this.config.Model,
            Temperature = this.config.Temperature,
            MaxTokens = this.config.MaxTokens,
            ReasoningEffort = this.config.DefaultReasoningEffort,
            FriendlyName = "primary"
        };

        var primaryClient = CreateChatClient("primary", primaryEndpoint, tavilySearchPlugin);
        this.chatClients.Add(primaryClient);

        if (this.config.FallbackModels?.Count > 0)
        {
            var index = 0;
            foreach (var fallback in this.config.FallbackModels)
            {
                var descriptor = CreateChatClient($"fallback_{index}", fallback, tavilySearchPlugin);
                this.chatClients.Add(descriptor);
                index++;
            }
        }

        this.actionGuardClient = this.config.ActionGuardClient != null
            ? CreateChatClient("action_guard", this.config.ActionGuardClient, tavilySearchPlugin)
            : primaryClient;
        this.logger.Information("Action guard client resolved to {Client}", this.actionGuardClient.DisplayName);

        if (this.config.TaskClient != null)
        {
            this.taskClient = CreateChatClient("task_client", this.config.TaskClient, tavilySearchPlugin);
            this.logger.Information("Task command client resolved to {Client}", this.taskClient.DisplayName);
        }

        this.logger.Information("Initialized {PrimaryModel} with {FallbackCount} fallback client(s)", primaryClient.Config.Model, this.chatClients.Count - 1);

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
    /// Get chat completion from default LLM pipeline (gpt-oss-20b)
    /// </summary>
    public Task<(string response, int promptTokens, int completionTokens)> GetChatCompletionAsync(
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory chatHistory,
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteChatCompletionAsync(
            chatClients,
                chatHistory,
            guildId,
            config.DefaultReasoningEffort,
            TimeSpan.FromMinutes(5),
            cancellationToken);
    }

    /// <summary>
    /// Get chat completion using TaskCommands pipeline (Fara-7B first, fallback to defaults)
    /// </summary>
    public Task<(string response, int promptTokens, int completionTokens)> GetTaskChatCompletionAsync(
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory chatHistory,
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteChatCompletionAsync(
            GetTaskClientPipeline(),
            chatHistory,
            guildId,
            config.TaskClient?.ReasoningEffort ?? config.DefaultReasoningEffort,
            TimeSpan.FromMinutes(5),
            cancellationToken);
    }

    /// <summary>
    /// Stream chat completion from default pipeline
    /// </summary>
    public IAsyncEnumerable<(string content, string? reasoning, int? promptTokens, int? completionTokens)> GetChatCompletionStreamingAsync(
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory chatHistory,
        ulong? guildId = null,
        string? reasoningEffort = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteStreamingCompletionAsync(
            chatClients,
            chatHistory,
            guildId,
            reasoningEffort ?? config.DefaultReasoningEffort,
            TimeSpan.FromMinutes(10),
            cancellationToken);
    }

    /// <summary>
    /// Stream chat completion using TaskCommands (Fara-first) pipeline
    /// </summary>
    public IAsyncEnumerable<(string content, string? reasoning, int? promptTokens, int? completionTokens)> GetTaskChatCompletionStreamingAsync(
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory chatHistory,
        ulong? guildId = null,
        string? reasoningEffort = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteStreamingCompletionAsync(
            GetTaskClientPipeline(),
            chatHistory,
            guildId,
            reasoningEffort ?? config.TaskClient?.ReasoningEffort ?? config.DefaultReasoningEffort,
            TimeSpan.FromMinutes(10),
            cancellationToken);
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
    public static int EstimateTokenCount(string text)
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
        
        // Replace placeholders in system prompt with configured values
        globalSystemPrompt = globalSystemPrompt
            .Replace("{BotName}", config.BotName)
            .Replace("{RoleBackground}", config.RoleBackground)
            .Replace("{LanguagePreference}", config.LanguagePreference)
            .Replace("{StyleDescription}", config.StyleDescription);

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
            if (item.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
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
        if (channelHistory.Count > 0)
        {
            contextBuilder.AppendLine();
            contextBuilder.AppendLine($"Recent Channel Messages (last {channelHistory.Count}):");
            
            foreach (var msg in channelHistory)
            {
                var timestamp = msg.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                var role = msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "User" : "Bot";
                var userIdStr = msg.UserId.ToString();
                
                // Truncate long messages for context
                var content = msg.Content.Length > 100 
                    ? msg.Content[..97] + "..." 
                    : msg.Content;
                
                // Replace newlines in content to keep it compact
                content = content.Replace("\n", " ").Replace("\r", "");
                
                contextBuilder.AppendLine($"[{timestamp}] {role} {userIdStr}: {content}");
            }
        }
        
        contextBuilder.AppendLine("=========================");
        
        return contextBuilder.ToString();
    }

    private async Task<(string response, int promptTokens, int completionTokens)> ExecuteChatCompletionAsync(
        IEnumerable<ChatClientDescriptor> clientPipeline,
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory chatHistory,
        ulong? guildId,
        string? reasoningEffort,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var (temperature, maxTokens) = await ResolveGenerationParametersAsync(guildId);
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        Exception? lastError = null;

        foreach (var client in clientPipeline)
        {
            try
            {
                var executionSettings = CreateExecutionSettings(client, temperature, maxTokens, reasoningEffort);
                logger.Debug("Sending request via {Client} with {MessageCount} messages", client.DisplayName, chatHistory.Count);

                var result = await client.ChatService.GetChatMessageContentsAsync(
                    chatHistory,
                    executionSettings,
                    client.Kernel,
                    linkedCts.Token);

                var (promptTokens, completionTokens) = ExtractTokenUsage(result?.Count > 0 ? result[^1] : null);
                var responseContent = CombineResponseContent(result);

                logger.Information(
                    "LLM response from {Client}. Prompt tokens: {PromptTokens}, Completion tokens: {CompletionTokens}",
                    client.DisplayName,
                    promptTokens,
                    completionTokens);

                return (responseContent, promptTokens, completionTokens);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                logger.Error("LLM request timed out after {Minutes} minutes", timeout.TotalMinutes);
                throw new TimeoutException($"LLM request timed out after {timeout.TotalMinutes} minutes");
            }
            catch (Exception ex)
            {
                lastError = ex;
                logger.Warning(ex, "LLM request failed via {Client}, attempting fallback", client.DisplayName);
            }
        }

        logger.Error(lastError, "All configured LLM endpoints failed");
        throw lastError ?? new InvalidOperationException("Unable to reach any LLM endpoint");
    }

    private async IAsyncEnumerable<(string content, string? reasoning, int? promptTokens, int? completionTokens)> ExecuteStreamingCompletionAsync(
        IEnumerable<ChatClientDescriptor> clientPipeline,
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory chatHistory,
        ulong? guildId,
        string? reasoningEffort,
        TimeSpan timeout,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (temperature, maxTokens) = await ResolveGenerationParametersAsync(guildId);
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        Exception? lastError = null;

        foreach (var client in clientPipeline)
        {
            var yieldedAny = false;
            Exception? streamException = null;
            var executionSettings = CreateExecutionSettings(client, temperature, maxTokens, reasoningEffort);
            var enumerator = StreamFromClientAsync(
                client,
                chatHistory,
                executionSettings,
                linkedCts.Token).GetAsyncEnumerator(linkedCts.Token);

            try
            {
                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = await enumerator.MoveNextAsync();
                    }
                    catch (Exception ex)
                    {
                        streamException = ex;
                        break;
                    }

                    if (!hasNext)
                    {
                        streamException = null;
                        break;
                    }

                    yieldedAny = true;
                    yield return enumerator.Current;
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            if (streamException == null)
            {
                yield break;
            }

            if (streamException is OperationCanceledException && timeoutCts.IsCancellationRequested)
            {
                logger.Error("LLM streaming request timed out after {Minutes} minutes", timeout.TotalMinutes);
                throw new TimeoutException($"LLM streaming request timed out after {timeout.TotalMinutes} minutes");
            }

            if (yieldedAny)
            {
                logger.Error(streamException, "Streaming failed mid-response via {Client}", client.DisplayName);
                throw streamException;
            }

            lastError = streamException;
            logger.Warning(streamException, "Streaming failed via {Client}, attempting fallback");
        }

        logger.Error(lastError, "All configured LLM endpoints failed during streaming");
        throw lastError ?? new InvalidOperationException("Unable to stream from any LLM endpoint");
    }

    private IEnumerable<ChatClientDescriptor> GetTaskClientPipeline()
    {
        if (taskClient != null)
        {
            yield return taskClient;
        }

        foreach (var client in chatClients)
        {
            if (taskClient != null && ReferenceEquals(client, taskClient))
            {
                continue;
            }

            yield return client;
        }
    }

    private async IAsyncEnumerable<(string content, string? reasoning, int? promptTokens, int? completionTokens)> StreamFromClientAsync(
        ChatClientDescriptor client,
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory chatHistory,
        OpenAIPromptExecutionSettings executionSettings,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int? promptTokens = null;
        int? completionTokens = null;
        string? reasoning = null;

        logger.Debug("Sending streaming request to LLM with {MessageCount} messages (Auto function calling enabled)", chatHistory.Count);

        await foreach (var message in client.ChatService.GetStreamingChatMessageContentsAsync(
            chatHistory,
            executionSettings,
            client.Kernel,
            cancellationToken))
        {
            if (message.Metadata != null)
            {
                if (message.Metadata.TryGetValue("Usage", out var usageObj))
                {
                    UpdateUsageFromMetadata(usageObj, ref promptTokens, ref completionTokens);
                }

                if (message.Metadata.TryGetValue("Reasoning", out var reasoningObj) ||
                    message.Metadata.TryGetValue("reasoning", out reasoningObj))
                {
                    reasoning = reasoningObj?.ToString();
                }
            }

            yield return (message.Content ?? string.Empty, reasoning, promptTokens, completionTokens);
        }

        logger.Information("LLM streaming response via {Client} completed. Prompt tokens: {PromptTokens}, Completion tokens: {CompletionTokens}",
            client.DisplayName,
            promptTokens ?? 0,
            completionTokens ?? 0);
    }

    private async Task<(double temperature, int maxTokens)> ResolveGenerationParametersAsync(ulong? guildId)
    {
        var temperatureSetting = await repository.GetSettingAsync("Temperature");
        var globalMaxTokensSetting = await repository.GetSettingAsync("GlobalMaxTokens");

        var temperature = double.TryParse(temperatureSetting, out var temp) ? temp : config.Temperature;
        var maxTokens = int.TryParse(globalMaxTokensSetting, out var max) ? max : config.MaxTokens;

        if (guildId.HasValue)
        {
            var guildSettings = await repository.GetGuildSettingsAsync(guildId.Value);
            if (guildSettings?.MaxTokens.HasValue == true)
            {
                maxTokens = Math.Min(maxTokens, guildSettings.MaxTokens.Value);
            }
        }

        return (temperature, maxTokens);
    }

    private OpenAIPromptExecutionSettings CreateExecutionSettings(
        ChatClientDescriptor client,
        double baseTemperature,
        int baseMaxTokens,
        string? reasoningEffort)
    {
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = client.Config.Temperature ?? baseTemperature,
            MaxTokens = client.Config.MaxTokens ?? baseMaxTokens,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var effectiveReasoning = client.Config.ReasoningEffort ?? reasoningEffort;
        if (!string.IsNullOrWhiteSpace(effectiveReasoning))
        {
            executionSettings.ExtensionData = new Dictionary<string, object>
            {
                ["reasoning_effort"] = effectiveReasoning!
            };
        }

        return executionSettings;
    }

    private static string CombineResponseContent(IReadOnlyList<ChatMessageContent>? contents)
    {
        if (contents == null || contents.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(string.Empty, contents.Select(r => r.Content));
    }

    private static (int promptTokens, int completionTokens) ExtractTokenUsage(ChatMessageContent? message)
    {
        if (message?.Metadata == null || !message.Metadata.TryGetValue("Usage", out var usageObj))
        {
            return (0, 0);
        }

        int? promptTokens = 0;
        int? completionTokens = 0;
        UpdateUsageFromMetadata(usageObj, ref promptTokens, ref completionTokens);
        return (promptTokens ?? 0, completionTokens ?? 0);
    }

    private static void UpdateUsageFromMetadata(object? usageObj, ref int? promptTokens, ref int? completionTokens)
    {
        if (usageObj is IDictionary<string, object> usageDict)
        {
            if (TryGetUsageValue(usageDict, out var promptValue, "prompt_tokens", "PromptTokens"))
            {
                promptTokens = Convert.ToInt32(promptValue);
            }

            if (TryGetUsageValue(usageDict, out var completionValue, "completion_tokens", "CompletionTokens"))
            {
                completionTokens = Convert.ToInt32(completionValue);
            }

            return;
        }

        if (usageObj is null)
        {
            return;
        }

        var usageType = usageObj.GetType();
        var inputTokenProp = usageType.GetProperty("InputTokenCount")
            ?? usageType.GetProperty("PromptTokens")
            ?? usageType.GetProperty("prompt_tokens");
        if (inputTokenProp != null)
        {
            var value = inputTokenProp.GetValue(usageObj);
            if (value != null)
            {
                promptTokens = Convert.ToInt32(value);
            }
        }

        var outputTokenProp = usageType.GetProperty("OutputTokenCount")
            ?? usageType.GetProperty("CompletionTokens")
            ?? usageType.GetProperty("completion_tokens");
        if (outputTokenProp != null)
        {
            var value = outputTokenProp.GetValue(usageObj);
            if (value != null)
            {
                completionTokens = Convert.ToInt32(value);
            }
        }
    }

    private static bool TryGetUsageValue(IDictionary<string, object> metadata, out object? value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    private ChatClientDescriptor CreateChatClient(string name, LLMEndpointConfig endpointConfig, TavilySearchPlugin tavilySearchPlugin)
    {
        var builder = Kernel.CreateBuilder();

        var resolvedEndpoint = string.IsNullOrWhiteSpace(endpointConfig.ApiEndpoint)
            ? config.ApiEndpoint
            : endpointConfig.ApiEndpoint;
        var resolvedModel = string.IsNullOrWhiteSpace(endpointConfig.Model)
            ? config.Model
            : endpointConfig.Model;
        var resolvedApiKey = string.IsNullOrWhiteSpace(endpointConfig.ApiKey)
            ? (string.IsNullOrWhiteSpace(config.ApiKey) ? "not-needed" : config.ApiKey)
            : endpointConfig.ApiKey;

        builder.AddOpenAIChatCompletion(
            serviceId: name,
            modelId: resolvedModel,
            apiKey: resolvedApiKey,
            endpoint: new Uri(resolvedEndpoint));

        var kernel = builder.Build();
        kernel.Plugins.AddFromObject(tavilySearchPlugin, "TavilySearch");
        var chatClient = kernel.GetRequiredService<IChatCompletionService>();

        return new ChatClientDescriptor(
            name,
            endpointConfig,
            endpointConfig.FriendlyName ?? name,
            kernel,
            chatClient);
    }

    private sealed record ChatClientDescriptor(
        string Name,
        LLMEndpointConfig Config,
        string DisplayName,
        Kernel Kernel,
        IChatCompletionService ChatService);

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

