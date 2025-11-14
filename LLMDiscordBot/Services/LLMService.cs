using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Options;
using LLMDiscordBot.Configuration;
using LLMDiscordBot.Data;
using Serilog;

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

    public LLMService(
        IOptions<LLMConfig> config,
        ILogger logger,
        IRepository repository)
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
        this.chatService = this.kernel.GetRequiredService<IChatCompletionService>();

        this.logger.Information("LLM Service initialized with endpoint: {Endpoint}", this.config.ApiEndpoint);
    }

    /// <summary>
    /// Get chat completion from LLM
    /// </summary>
    public async Task<(string response, int promptTokens, int completionTokens)> GetChatCompletionAsync(
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory chatHistory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get settings from database if available
            var modelSetting = await repository.GetSettingAsync("Model");
            var temperatureSetting = await repository.GetSettingAsync("Temperature");
            var maxTokensSetting = await repository.GetSettingAsync("MaxTokens");

            var temperature = double.TryParse(temperatureSetting, out var temp) ? temp : config.Temperature;
            var maxTokens = int.TryParse(maxTokensSetting, out var max) ? max : config.MaxTokens;

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = temperature,
                MaxTokens = maxTokens
            };

            logger.Debug("Sending request to LLM with {MessageCount} messages", chatHistory.Count);

            var result = await chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                kernel,
                cancellationToken);

            // Extract token usage from metadata
            var promptTokens = 0;
            var completionTokens = 0;

            if (result != null && result.Count > 0)
            {
                var lastMessage = result[result.Count - 1];
                if (lastMessage.Metadata != null &&
                    lastMessage.Metadata.TryGetValue("Usage", out var usageObj))
                {
                    // Try to parse usage information
                    var usageDict = usageObj as IDictionary<string, object>;
                    if (usageDict != null)
                    {
                        if (usageDict.TryGetValue("PromptTokens", out var pt))
                            promptTokens = Convert.ToInt32(pt);
                        if (usageDict.TryGetValue("CompletionTokens", out var ct))
                            completionTokens = Convert.ToInt32(ct);
                    }
                }

                var responseContent = string.Join("", result.Select(r => r.Content));
                logger.Information("LLM response received. Prompt tokens: {PromptTokens}, Completion tokens: {CompletionTokens}",
                    promptTokens, completionTokens);

                return (responseContent ?? "", promptTokens, completionTokens);
            }

            return ("", 0, 0);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting chat completion from LLM");
            throw;
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
        int historyCount = 10)
    {
        var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();

        // Add system message
        var systemPrompt = await repository.GetSettingAsync("SystemPrompt") ?? config.SystemPrompt;
        chatHistory.AddSystemMessage(systemPrompt);

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
}

