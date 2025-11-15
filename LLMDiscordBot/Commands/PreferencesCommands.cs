using Discord;
using Discord.Interactions;
using LLMDiscordBot.Data;
using LLMDiscordBot.Models;
using Serilog;
using System.Text.Json;

namespace LLMDiscordBot.Commands;

/// <summary>
/// User preferences commands
/// </summary>
[Group("preferences", "個人偏好設定")]
public class PreferencesCommands(
    IRepository repository,
    ILogger logger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("view", "查看您的個人偏好設定和習慣統計")]
    public async Task ViewPreferencesAsync()
    {
        try
        {
            await DeferAsync(ephemeral: true);

            var userId = Context.User.Id;
            var preferences = await repository.GetUserPreferencesAsync(userId);

            var embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle($"📋 {Context.User.Username} 的個人偏好設定")
                .WithThumbnailUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                .WithCurrentTimestamp();

            if (preferences != null)
            {
                // General preferences section
                var generalPrefs = new List<string>();
                if (!string.IsNullOrEmpty(preferences.PreferredLanguage))
                    generalPrefs.Add($"語言: **{preferences.PreferredLanguage}**");
                if (preferences.PreferredTemperature.HasValue)
                    generalPrefs.Add($"溫度: **{preferences.PreferredTemperature.Value:F2}**");
                if (preferences.PreferredMaxTokens.HasValue)
                    generalPrefs.Add($"最大 Token 數: **{preferences.PreferredMaxTokens.Value:N0}**");
                if (!string.IsNullOrEmpty(preferences.PreferredResponseStyle))
                    generalPrefs.Add($"回答風格: **{preferences.PreferredResponseStyle}**");
                if (!string.IsNullOrEmpty(preferences.PreferredTimeZone))
                    generalPrefs.Add($"時區: **{preferences.PreferredTimeZone}**");

                if (generalPrefs.Any())
                {
                    embed.AddField("⚙️ 一般偏好", string.Join("\n", generalPrefs), false);
                }
                else
                {
                    embed.AddField("⚙️ 一般偏好", "尚未設定（使用系統預設值）", false);
                }

                // Custom system prompt
                if (!string.IsNullOrEmpty(preferences.CustomSystemPrompt))
                {
                    var promptPreview = preferences.CustomSystemPrompt.Length > 100
                        ? preferences.CustomSystemPrompt.Substring(0, 100) + "..."
                        : preferences.CustomSystemPrompt;
                    embed.AddField("💬 自訂系統提示", $"> {promptPreview}", false);
                }

                // Content preferences
                var contentPrefs = new List<string>();
                contentPrefs.Add($"程式碼範例: {(preferences.PreferCodeExamples ? "✅" : "❌")}");
                contentPrefs.Add($"逐步教學: {(preferences.PreferStepByStep ? "✅" : "❌")}");
                contentPrefs.Add($"視覺內容: {(preferences.PreferVisualContent ? "✅" : "❌")}");
                contentPrefs.Add($"智慧建議: {(preferences.EnableSmartSuggestions ? "✅" : "❌")}");
                contentPrefs.Add($"記憶對話上下文: {(preferences.RememberConversationContext ? "✅" : "❌")}");
                embed.AddField("📝 內容偏好", string.Join("\n", contentPrefs), false);

                // Usage statistics
                var usageStats = new List<string>();
                usageStats.Add($"總互動次數: **{preferences.TotalInteractions:N0}**");
                usageStats.Add($"連續天數: **{preferences.ConsecutiveDays}** 天");
                usageStats.Add($"平均訊息長度: **{preferences.AverageMessageLength:F0}** 字元");
                if (preferences.LastInteractionAt.HasValue)
                    usageStats.Add($"最後互動: {preferences.LastInteractionAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
                embed.AddField("📊 使用統計", string.Join("\n", usageStats), false);

                // Favorite commands
                if (!string.IsNullOrEmpty(preferences.FavoriteCommands))
                {
                    try
                    {
                        var commandFreq = JsonSerializer.Deserialize<Dictionary<string, int>>(preferences.FavoriteCommands);
                        if (commandFreq != null && commandFreq.Any())
                        {
                            var topCommands = string.Join("\n", commandFreq.Take(5).Select(x => $"`/{x.Key}`: **{x.Value}** 次"));
                            embed.AddField("⭐ 常用命令", topCommands, true);
                        }
                    }
                    catch { }
                }

                // Top topics
                if (!string.IsNullOrEmpty(preferences.MostUsedTopics))
                {
                    try
                    {
                        var topics = JsonSerializer.Deserialize<List<string>>(preferences.MostUsedTopics);
                        if (topics != null && topics.Any())
                        {
                            var topTopics = string.Join(", ", topics.Select(t => $"**{t}**"));
                            embed.AddField("🏷️ 常用主題", topTopics, true);
                        }
                    }
                    catch { }
                }

                embed.WithFooter($"偏好設定建立於 {preferences.CreatedAt:yyyy-MM-dd}");
            }
            else
            {
                embed.WithDescription("您還沒有設定任何個人偏好，系統將使用預設值。\n\n使用 `/preferences set` 命令開始自訂您的體驗！");
            }

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
            logger.Information("User {UserId} viewed their preferences", userId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error viewing user preferences");
            await FollowupAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("set-language", "設定您偏好的語言")]
    public async Task SetLanguageAsync(
        [Summary("language", "語言代碼（例如：zh-TW, en-US, ja-JP）")]
        [MaxLength(10)]
        string language)
    {
        try
        {
            var userId = Context.User.Id;
            var preferences = await repository.GetOrCreateUserPreferencesAsync(userId);
            preferences.PreferredLanguage = language;
            await repository.UpdateUserPreferencesAsync(preferences);

            await RespondAsync(
                embed: new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 偏好語言已更新")
                    .WithDescription($"您的偏好語言已設定為 **{language}**。")
                    .Build(),
                ephemeral: true);

            logger.Information("User {UserId} set language to {Language}", userId, language);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting language preference");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("set-temperature", "設定您偏好的生成溫度")]
    public async Task SetTemperatureAsync(
        [Summary("temperature", "溫度值 (0.0 - 2.0)")]
        [MinValue(0)]
        [MaxValue(2)]
        double temperature)
    {
        try
        {
            var userId = Context.User.Id;
            var preferences = await repository.GetOrCreateUserPreferencesAsync(userId);
            preferences.PreferredTemperature = temperature;
            await repository.UpdateUserPreferencesAsync(preferences);

            await RespondAsync(
                embed: new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 偏好溫度已更新")
                    .WithDescription($"您的偏好溫度已設定為 **{temperature:F2}**。\n\n" +
                                   "較低的溫度會產生更一致和確定的回答，較高的溫度會產生更多樣和創意的回答。")
                    .Build(),
                ephemeral: true);

            logger.Information("User {UserId} set temperature to {Temperature}", userId, temperature);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting temperature preference");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("set-max-tokens", "設定您偏好的最大回應 Token 數")]
    public async Task SetMaxTokensAsync(
        [Summary("max-tokens", "最大 Token 數")]
        [MinValue(100)]
        [MaxValue(32000)]
        int maxTokens)
    {
        try
        {
            var userId = Context.User.Id;
            var preferences = await repository.GetOrCreateUserPreferencesAsync(userId);
            preferences.PreferredMaxTokens = maxTokens;
            await repository.UpdateUserPreferencesAsync(preferences);

            await RespondAsync(
                embed: new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 偏好最大 Token 數已更新")
                    .WithDescription($"您的偏好最大 Token 數已設定為 **{maxTokens:N0}**。")
                    .Build(),
                ephemeral: true);

            logger.Information("User {UserId} set max tokens to {MaxTokens}", userId, maxTokens);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting max tokens preference");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("set-style", "設定您偏好的回答風格")]
    public async Task SetStyleAsync(
        [Summary("style", "回答風格")]
        [Choice("簡潔", "concise")]
        [Choice("詳細", "detailed")]
        [Choice("輕鬆", "casual")]
        [Choice("正式", "formal")]
        [Choice("技術性", "technical")]
        [Choice("創意性", "creative")]
        string style)
    {
        try
        {
            var userId = Context.User.Id;
            var preferences = await repository.GetOrCreateUserPreferencesAsync(userId);
            preferences.PreferredResponseStyle = style;
            await repository.UpdateUserPreferencesAsync(preferences);

            var styleNames = new Dictionary<string, string>
            {
                ["concise"] = "簡潔",
                ["detailed"] = "詳細",
                ["casual"] = "輕鬆",
                ["formal"] = "正式",
                ["technical"] = "技術性",
                ["creative"] = "創意性"
            };

            await RespondAsync(
                embed: new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 偏好風格已更新")
                    .WithDescription($"您的偏好回答風格已設定為 **{styleNames.GetValueOrDefault(style, style)}**。")
                    .Build(),
                ephemeral: true);

            logger.Information("User {UserId} set style to {Style}", userId, style);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting style preference");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("set-custom-prompt", "設定您的自訂系統提示（會附加在標準提示之後）")]
    public async Task SetCustomPromptAsync(
        [Summary("prompt", "自訂系統提示內容")]
        [MaxLength(1000)]
        string prompt)
    {
        try
        {
            var userId = Context.User.Id;
            var preferences = await repository.GetOrCreateUserPreferencesAsync(userId);
            preferences.CustomSystemPrompt = prompt;
            await repository.UpdateUserPreferencesAsync(preferences);

            await RespondAsync(
                embed: new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 自訂提示已更新")
                    .WithDescription($"您的自訂系統提示已更新。\n\n**新的提示：**\n> {(prompt.Length > 200 ? prompt.Substring(0, 200) + "..." : prompt)}")
                    .Build(),
                ephemeral: true);

            logger.Information("User {UserId} set custom prompt", userId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting custom prompt");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("toggle-code-examples", "切換是否偏好在回答中包含程式碼範例")]
    public async Task ToggleCodeExamplesAsync()
    {
        try
        {
            var userId = Context.User.Id;
            var preferences = await repository.GetOrCreateUserPreferencesAsync(userId);
            preferences.PreferCodeExamples = !preferences.PreferCodeExamples;
            await repository.UpdateUserPreferencesAsync(preferences);

            await RespondAsync(
                embed: new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 偏好已更新")
                    .WithDescription($"程式碼範例偏好已設定為 **{(preferences.PreferCodeExamples ? "啟用" : "停用")}**。")
                    .Build(),
                ephemeral: true);

            logger.Information("User {UserId} toggled code examples to {Enabled}", userId, preferences.PreferCodeExamples);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error toggling code examples preference");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("toggle-step-by-step", "切換是否偏好逐步教學式的回答")]
    public async Task ToggleStepByStepAsync()
    {
        try
        {
            var userId = Context.User.Id;
            var preferences = await repository.GetOrCreateUserPreferencesAsync(userId);
            preferences.PreferStepByStep = !preferences.PreferStepByStep;
            await repository.UpdateUserPreferencesAsync(preferences);

            await RespondAsync(
                embed: new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 偏好已更新")
                    .WithDescription($"逐步教學偏好已設定為 **{(preferences.PreferStepByStep ? "啟用" : "停用")}**。")
                    .Build(),
                ephemeral: true);

            logger.Information("User {UserId} toggled step-by-step to {Enabled}", userId, preferences.PreferStepByStep);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error toggling step-by-step preference");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("reset", "重置所有個人偏好設定為預設值")]
    public async Task ResetPreferencesAsync()
    {
        try
        {
            var userId = Context.User.Id;
            var preferences = await repository.GetUserPreferencesAsync(userId);

            if (preferences == null)
            {
                await RespondAsync("您還沒有設定任何個人偏好。", ephemeral: true);
                return;
            }

            // Reset all preferences to defaults
            preferences.PreferredLanguage = null;
            preferences.PreferredTemperature = null;
            preferences.PreferredMaxTokens = null;
            preferences.PreferredResponseStyle = null;
            preferences.CustomSystemPrompt = null;
            preferences.PreferredTimeZone = null;
            preferences.EnableSmartSuggestions = true;
            preferences.RememberConversationContext = true;
            preferences.PreferCodeExamples = false;
            preferences.PreferStepByStep = false;
            preferences.PreferVisualContent = false;

            await repository.UpdateUserPreferencesAsync(preferences);

            await RespondAsync(
                embed: new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 偏好已重置")
                    .WithDescription("您的所有個人偏好設定已重置為預設值。\n\n（您的使用統計和習慣數據不受影響）")
                    .Build(),
                ephemeral: true);

            logger.Information("User {UserId} reset their preferences", userId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error resetting preferences");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("stats", "查看您的使用統計和習慣分析")]
    public async Task ViewStatsAsync()
    {
        try
        {
            await DeferAsync(ephemeral: true);

            var userId = Context.User.Id;
            var preferences = await repository.GetUserPreferencesAsync(userId);
            var recentInteractions = await repository.GetUserInteractionHistoryAsync(userId, 100);

            var embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle($"📊 {Context.User.Username} 的使用統計")
                .WithThumbnailUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                .WithCurrentTimestamp();

            if (preferences != null && preferences.TotalInteractions > 0)
            {
                // Basic stats
                embed.AddField("📈 基本統計",
                    $"總互動次數: **{preferences.TotalInteractions:N0}**\n" +
                    $"連續使用天數: **{preferences.ConsecutiveDays}** 天\n" +
                    $"平均訊息長度: **{preferences.AverageMessageLength:F0}** 字元\n" +
                    $"最後互動: {(preferences.LastInteractionAt.HasValue ? $"{preferences.LastInteractionAt.Value:yyyy-MM-dd HH:mm}" : "無")}",
                    false);

                // Activity analysis
                if (recentInteractions.Any())
                {
                    var avgResponseTime = recentInteractions.Average(i => i.ResponseTime.TotalSeconds);
                    var totalResponseLength = recentInteractions.Sum(i => i.ResponseLength);
                    var avgResponseLength = recentInteractions.Average(i => i.ResponseLength);

                    embed.AddField("⚡ 活動分析",
                        $"近期互動: **{recentInteractions.Count}** 次\n" +
                        $"平均回應時間: **{avgResponseTime:F1}** 秒\n" +
                        $"平均回應長度: **{avgResponseLength:F0}** 字元\n" +
                        $"總回應長度: **{totalResponseLength:N0}** 字元",
                        false);

                    // Daily activity
                    var last7Days = recentInteractions.Where(i => i.Timestamp >= DateTime.UtcNow.AddDays(-7)).ToList();
                    var dailyActivity = last7Days.GroupBy(i => i.Timestamp.Date)
                        .OrderByDescending(g => g.Key)
                        .Take(7)
                        .Select(g => $"`{g.Key:MM/dd}`: {g.Count()} 次")
                        .ToList();

                    if (dailyActivity.Any())
                    {
                        embed.AddField("📅 近 7 天活動", string.Join("\n", dailyActivity), true);
                    }

                    // Command usage
                    var commandUsage = recentInteractions
                        .GroupBy(i => i.CommandType)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .Select(g => $"`/{g.Key}`: {g.Count()} 次")
                        .ToList();

                    if (commandUsage.Any())
                    {
                        embed.AddField("⭐ 常用命令", string.Join("\n", commandUsage), true);
                    }

                    // Topic analysis
                    var topicUsage = recentInteractions
                        .Where(i => !string.IsNullOrEmpty(i.TopicCategory))
                        .GroupBy(i => i.TopicCategory)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .Select(g => $"**{g.Key}**: {g.Count()} 次")
                        .ToList();

                    if (topicUsage.Any())
                    {
                        embed.AddField("🏷️ 主題分布", string.Join("\n", topicUsage), false);
                    }
                }

                embed.WithFooter($"統計數據從 {preferences.CreatedAt:yyyy-MM-dd} 開始追蹤");
            }
            else
            {
                embed.WithDescription("尚無使用統計數據。開始使用 Bot 後，系統會自動追蹤您的習慣。");
            }

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
            logger.Information("User {UserId} viewed their usage stats", userId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error viewing user stats");
            await FollowupAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }
}

