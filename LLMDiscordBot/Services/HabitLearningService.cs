using LLMDiscordBot.Data;
using LLMDiscordBot.Models;
using Serilog;

namespace LLMDiscordBot.Services;

/// <summary>
/// Service for learning and adapting to user habits
/// </summary>
public class HabitLearningService(
    IRepository repository,
    ILogger logger)
{
    /// <summary>
    /// Learn from user interaction and update habits
    /// </summary>
    public async Task LearnFromInteractionAsync(
        ulong userId,
        ulong? guildId,
        string commandType,
        string userMessage,
        string assistantResponse,
        TimeSpan responseTime,
        string? topicCategory = null)
    {
        try
        {
            // Log the interaction
            var interactionLog = new InteractionLog
            {
                UserId = userId,
                GuildId = guildId,
                CommandType = commandType,
                MessageLength = userMessage.Length,
                ResponseLength = assistantResponse.Length,
                ResponseTime = responseTime,
                TopicCategory = topicCategory,
                Timestamp = DateTime.UtcNow
            };

            await repository.AddInteractionLogAsync(interactionLog);

            // Update user habits
            await repository.UpdateUserHabitsAsync(
                userId,
                commandType,
                userMessage.Length,
                assistantResponse.Length,
                responseTime,
                topicCategory);

            logger.Debug("Learned from user {UserId} interaction: {CommandType}", userId, commandType);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error learning from user interaction");
            // Don't throw - learning failures shouldn't break user experience
        }
    }

    /// <summary>
    /// Detect topic category from user message
    /// </summary>
    public string? DetectTopicCategory(string message)
    {
        var lowerMessage = message.ToLower();

        // Programming/Tech
        if (ContainsKeywords(lowerMessage, "code", "程式", "python", "javascript", "java", "c#", "c++", "sql",
            "function", "class", "method", "bug", "error", "debug", "api", "database", "git", "github"))
            return "programming";

        // Math/Science
        if (ContainsKeywords(lowerMessage, "math", "數學", "calculate", "計算", "equation", "方程", "formula", "公式",
            "physics", "物理", "chemistry", "化學", "science", "科學"))
            return "math_science";

        // Writing/Language
        if (ContainsKeywords(lowerMessage, "write", "寫", "essay", "文章", "translate", "翻譯", "grammar", "文法",
            "language", "語言", "文字", "作文", "論文"))
            return "writing_language";

        // Business/Finance
        if (ContainsKeywords(lowerMessage, "business", "商業", "finance", "財務", "investment", "投資", "market", "市場",
            "strategy", "策略", "management", "管理", "經營"))
            return "business_finance";

        // Creative
        if (ContainsKeywords(lowerMessage, "creative", "創意", "idea", "點子", "brainstorm", "腦力激盪", "design", "設計",
            "art", "藝術", "story", "故事", "imagine", "想像"))
            return "creative";

        // Learning/Education
        if (ContainsKeywords(lowerMessage, "learn", "學習", "teach", "教", "explain", "解釋", "understand", "理解",
            "lesson", "課程", "study", "讀書", "homework", "作業"))
            return "education";

        // General conversation
        return "general";
    }

    /// <summary>
    /// Get smart suggestions based on user habits
    /// </summary>
    public async Task<List<string>> GetSmartSuggestionsAsync(ulong userId)
    {
        try
        {
            var suggestions = new List<string>();
            var preferences = await repository.GetUserPreferencesAsync(userId);

            if (preferences == null || !preferences.EnableSmartSuggestions)
                return suggestions;

            // Suggest based on interaction count
            if (preferences.TotalInteractions >= 10 && preferences.PreferredResponseStyle == null)
            {
                suggestions.Add("💡 您已使用 Bot 多次，要不要設定您偏好的回答風格？使用 `/preferences set-style`");
            }

            // Suggest consecutive days milestone
            if (preferences.ConsecutiveDays >= 7 && preferences.ConsecutiveDays % 7 == 0)
            {
                suggestions.Add($"🔥 太棒了！您已經連續使用 {preferences.ConsecutiveDays} 天了！");
            }

            // Suggest based on message length
            if (preferences.AverageMessageLength > 500 && preferences.PreferStepByStep == false)
            {
                suggestions.Add("💡 您似乎喜歡詳細的問題。要啟用逐步教學模式嗎？使用 `/preferences toggle-step-by-step`");
            }

            // Suggest based on favorite topics
            if (!string.IsNullOrEmpty(preferences.MostUsedTopics))
            {
                try
                {
                    var topics = System.Text.Json.JsonSerializer.Deserialize<List<string>>(preferences.MostUsedTopics);
                    if (topics != null && topics.Contains("programming") && !preferences.PreferCodeExamples)
                    {
                        suggestions.Add("💡 看起來您經常問程式問題。要啟用程式碼範例嗎？使用 `/preferences toggle-code-examples`");
                    }
                }
                catch { }
            }

            return suggestions.Take(2).ToList(); // Limit to 2 suggestions
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error getting smart suggestions");
            return new List<string>();
        }
    }

    /// <summary>
    /// Build personalized system prompt based on user preferences
    /// </summary>
    public async Task<string> BuildPersonalizedPromptAsync(ulong userId, string basePrompt)
    {
        try
        {
            var preferences = await repository.GetUserPreferencesAsync(userId);
            if (preferences == null)
                return basePrompt;

            var personalizedPrompt = basePrompt;

            // Add style preference
            if (!string.IsNullOrEmpty(preferences.PreferredResponseStyle))
            {
                var styleInstructions = preferences.PreferredResponseStyle switch
                {
                    "concise" => "Please provide concise and to-the-point answers.",
                    "detailed" => "Please provide detailed and comprehensive answers with thorough explanations.",
                    "casual" => "Please respond in a casual, friendly, and conversational tone.",
                    "formal" => "Please respond in a formal, professional tone.",
                    "technical" => "Please provide technical, precise answers with appropriate terminology.",
                    "creative" => "Please be creative and imaginative in your responses.",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(styleInstructions))
                    personalizedPrompt += $"\n\n{styleInstructions}";
            }

            // Add content preferences
            var contentInstructions = new List<string>();
            if (preferences.PreferCodeExamples)
                contentInstructions.Add("Include code examples when relevant");
            if (preferences.PreferStepByStep)
                contentInstructions.Add("Provide step-by-step instructions");
            if (preferences.PreferVisualContent)
                contentInstructions.Add("Use visual descriptions, diagrams in text, or structured formatting");

            if (contentInstructions.Any())
                personalizedPrompt += $"\n\n{string.Join(". ", contentInstructions)}.";

            // Add language preference
            if (!string.IsNullOrEmpty(preferences.PreferredLanguage))
            {
                personalizedPrompt += $"\n\nPreferred communication language: {preferences.PreferredLanguage}";
            }

            // Add custom prompt
            if (!string.IsNullOrEmpty(preferences.CustomSystemPrompt))
            {
                personalizedPrompt += $"\n\nUser's custom instructions: {preferences.CustomSystemPrompt}";
            }

            return personalizedPrompt;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error building personalized prompt");
            return basePrompt;
        }
    }

    private bool ContainsKeywords(string text, params string[] keywords)
    {
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}

