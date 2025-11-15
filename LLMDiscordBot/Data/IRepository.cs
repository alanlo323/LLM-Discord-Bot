using LLMDiscordBot.Models;

namespace LLMDiscordBot.Data;

/// <summary>
/// Repository interface for database operations
/// </summary>
public interface IRepository
{
    // User operations
    Task<User?> GetUserAsync(ulong userId);
    Task<User> GetOrCreateUserAsync(ulong userId, int defaultDailyLimit);
    Task UpdateUserAsync(User user);

    // Token usage operations
    Task<int> GetTodayTokenUsageAsync(ulong userId, DateTime today);
    Task<int> GetUserTodayTokenUsageInGuildAsync(ulong userId, ulong guildId, DateTime today);
    Task AddTokenUsageAsync(ulong userId, int tokens, DateTime date, ulong? guildId = null);

    // Chat history operations
    Task AddChatHistoryAsync(ChatHistory history);
    Task<List<ChatHistory>> GetRecentChatHistoryAsync(ulong userId, ulong channelId, int count);
    Task<List<ChatHistory>> GetChannelRecentChatHistoryAsync(ulong channelId, int count);
    Task ClearChatHistoryAsync(ulong userId, ulong channelId);
    Task<List<ChatHistory>> GetUserChatHistoryAsync(ulong userId, int count);

    // Bot settings operations
    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value, string? updatedBy = null);
    Task<Dictionary<string, string>> GetAllSettingsAsync();

    // Guild settings operations
    Task<GuildSettings?> GetGuildSettingsAsync(ulong guildId);
    Task<GuildSettings> GetOrCreateGuildSettingsAsync(ulong guildId);
    Task UpdateGuildSettingsAsync(GuildSettings settings);
    Task<bool> ValidateGuildLimitsAsync(ulong guildId, int? dailyLimit, int? maxTokens);
    Task<List<(GuildSettings guild, List<string> adjustments)>> AdjustGuildSettingsToGlobalLimitsAsync(int globalDailyLimit, int globalMaxTokens);

    // Guild admin operations
    Task<bool> IsGuildAdminAsync(ulong guildId, ulong userId);
    Task<List<GuildAdmin>> GetGuildAdminsAsync(ulong guildId);
    Task AddGuildAdminAsync(ulong guildId, ulong userId, string? createdBy = null);
    Task RemoveGuildAdminAsync(ulong guildId, ulong userId);

    // Global statistics operations
    Task<int> GetTotalUsersCountAsync();
    Task<int> GetBlockedUsersCountAsync();
    Task<int> GetActiveUsersTodayCountAsync(DateTime today);
    Task<long> GetTotalTokenUsageAsync();
    Task<long> GetTotalMessageCountAsync();
    Task<int> GetTodayTokenUsageAsync(DateTime today);
    Task<int> GetTodayMessageCountAsync(DateTime today);
    Task<List<TopUser>> GetTopUsersByTokenUsageAsync(DateTime date, int count);
    Task<List<DailyTrend>> GetDailyTokenUsageTrendAsync(DateTime startDate, DateTime endDate);

    // Guild statistics operations
    Task<int> GetGuildTodayTokenUsageAsync(ulong guildId, DateTime today);
    Task<int> GetGuildTodayMessageCountAsync(ulong guildId, DateTime today);
    Task<int> GetGuildActiveUsersTodayCountAsync(ulong guildId, DateTime today);
    Task<List<TopUser>> GetGuildTopUsersByTokenUsageAsync(ulong guildId, DateTime date, int count);
    Task<List<DailyTrend>> GetGuildDailyTokenUsageTrendAsync(ulong guildId, DateTime startDate, DateTime endDate);
    Task<long> GetGuildTotalTokenUsageAsync(ulong guildId);
    Task<long> GetGuildTotalMessageCountAsync(ulong guildId);

    // User preferences operations
    Task<UserPreferences?> GetUserPreferencesAsync(ulong userId);
    Task<UserPreferences> GetOrCreateUserPreferencesAsync(ulong userId);
    Task UpdateUserPreferencesAsync(UserPreferences preferences);
    Task UpdateUserHabitsAsync(ulong userId, string commandType, int messageLength, int responseLength, TimeSpan responseTime, string? topicCategory = null);

    // Interaction log operations
    Task AddInteractionLogAsync(InteractionLog log);
    Task<List<InteractionLog>> GetUserInteractionHistoryAsync(ulong userId, int count);
    Task<Dictionary<string, int>> GetUserCommandFrequencyAsync(ulong userId, DateTime since);
    Task<List<string>> GetUserTopTopicsAsync(ulong userId, int count);
}

