using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using LLMDiscordBot.Configuration;
using LLMDiscordBot.Models;
using Serilog;
using System.Text;

namespace LLMDiscordBot.Services;

/// <summary>
/// Service for analyzing conversations and extracting memory-worthy content
/// </summary>
public class MemoryAnalyzerService
{
    private readonly LLMService llmService;
    private readonly ILogger logger;
    private readonly MemoryExtractionConfig config;

    public MemoryAnalyzerService(
        LLMService llmService,
        ILogger logger,
        IOptions<GraphRagConfig> graphRagConfig)
    {
        this.llmService = llmService;
        this.logger = logger;
        this.config = graphRagConfig.Value.MemoryExtraction;
    }

    /// <summary>
    /// Analyze conversation and extract memory-worthy content
    /// </summary>
    public async Task<string?> AnalyzeConversationForMemoryAsync(
        List<ChatMessage> conversation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!config.EnableAutoExtraction)
            {
                logger.Debug("Auto memory extraction is disabled");
                return null;
            }

            if (conversation.Count < config.MinConversationLength)
            {
                logger.Debug("Conversation too short for memory extraction ({Count} < {Min})", 
                    conversation.Count, config.MinConversationLength);
                return null;
            }

            var isWorthy = await IsMemoryWorthyAsync(conversation, cancellationToken);
            if (!isWorthy)
            {
                logger.Debug("Conversation not memory-worthy");
                return null;
            }

            var result = await ExtractMemoryElementsAsync(conversation, cancellationToken);
            return result.ExtractedContent;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error analyzing conversation for memory");
            return null;
        }
    }

    /// <summary>
    /// Determine if conversation contains important information
    /// </summary>
    public async Task<bool> IsMemoryWorthyAsync(
        List<ChatMessage> conversation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Build conversation text
            var conversationText = BuildConversationText(conversation);

            // Check for simple heuristics first
            if (conversationText.Length < 50)
            {
                return false; // Too short to be meaningful
            }

            // Check for common non-memory-worthy patterns
            var trivialPatterns = new[]
            {
                "hello", "hi", "bye", "thanks", "thank you", "ok", "okay",
                "你好", "謝謝", "再見", "好的", "知道了"
            };

            var isTrivial = trivialPatterns.Any(p =>
                conversationText.Trim().Equals(p, StringComparison.OrdinalIgnoreCase));

            if (isTrivial)
            {
                logger.Debug("Conversation appears trivial");
                return false;
            }

            // Use LLM to determine if content is memory-worthy
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddSystemMessage(
                "You are a memory analyzer. Determine if the following conversation contains " +
                "important facts, relationships, preferences, or information that should be remembered. " +
                "Respond with ONLY 'YES' or 'NO'.");
            chatHistory.AddUserMessage($"Conversation:\n{conversationText}");

            var (response, _, _) = await llmService.GetChatCompletionAsync(chatHistory, null, cancellationToken);
            var isWorthy = response.Trim().StartsWith("YES", StringComparison.OrdinalIgnoreCase);

            logger.Debug("Memory-worthy analysis result: {IsWorthy}", isWorthy);
            return isWorthy;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error determining if conversation is memory-worthy, defaulting to true");
            return true; // Default to true to avoid missing important memories
        }
    }

    /// <summary>
    /// Extract entities and relationships from conversation
    /// </summary>
    public async Task<MemoryExtractionResult> ExtractMemoryElementsAsync(
        List<ChatMessage> conversation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var conversationText = BuildConversationText(conversation);

            // Use LLM to extract important information
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddSystemMessage(
                "You are a memory extraction assistant. " +
                "Analyze the conversation and extract important facts, relationships, entities, and preferences. " +
                "Focus on information that would be useful to remember for future conversations. " +
                "Format the extracted information as a clear, concise summary that can be stored and retrieved later. " +
                "Include: user preferences, important facts mentioned, relationships discussed, and any significant events or decisions.");
            chatHistory.AddUserMessage($"Conversation:\n{conversationText}\n\nExtract important information:");

            var (response, _, _) = await llmService.GetChatCompletionAsync(chatHistory, null, cancellationToken);

            if (!string.IsNullOrWhiteSpace(response))
            {
                logger.Information("Successfully extracted memory content, length: {Length}", response.Length);
                
                return new MemoryExtractionResult
                {
                    HasImportantContent = true,
                    ExtractedContent = response,
                    Entities = ExtractEntitiesFromText(response),
                    Topics = ExtractTopicsFromText(response)
                };
            }

            logger.Warning("No content extracted from conversation");
            return new MemoryExtractionResult { HasImportantContent = false };
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error extracting memory elements from conversation");
            return new MemoryExtractionResult { HasImportantContent = false };
        }
    }

    /// <summary>
    /// Build conversation text from messages
    /// </summary>
    private string BuildConversationText(List<ChatMessage> conversation)
    {
        var sb = new StringBuilder();
        foreach (var msg in conversation)
        {
            var role = msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "User" : "Assistant";
            sb.AppendLine($"{role}: {msg.Content}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Extract entity names from text (simple keyword extraction)
    /// </summary>
    private List<string> ExtractEntitiesFromText(string text)
    {
        // Simple heuristic: look for capitalized words
        var words = text.Split(new[] { ' ', '\n', '\r', ',', '.', '!', '?' }, 
            StringSplitOptions.RemoveEmptyEntries);
        
        return words
            .Where(w => w.Length > 2 && char.IsUpper(w[0]))
            .Distinct()
            .Take(10)
            .ToList();
    }

    /// <summary>
    /// Extract topic keywords from text
    /// </summary>
    private List<string> ExtractTopicsFromText(string text)
    {
        // Simple heuristic: extract common nouns (this is a placeholder)
        var commonTopicWords = new[]
        {
            "programming", "code", "project", "work", "hobby", "preference",
            "程式", "專案", "工作", "興趣", "喜好", "偏好"
        };

        return commonTopicWords
            .Where(t => text.Contains(t, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
    }
}

/// <summary>
/// Result of memory extraction from conversation
/// </summary>
public class MemoryExtractionResult
{
    public bool HasImportantContent { get; set; }
    public string? ExtractedContent { get; set; }
    public List<string> Entities { get; set; } = new();
    public List<string> Topics { get; set; } = new();
}

/// <summary>
/// Simple chat message structure for memory extraction
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}


