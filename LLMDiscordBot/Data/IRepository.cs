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
    Task AddTokenUsageAsync(ulong userId, int tokens, DateTime date);

    // Chat history operations
    Task AddChatHistoryAsync(ChatHistory history);
    Task<List<ChatHistory>> GetRecentChatHistoryAsync(ulong userId, ulong channelId, int count);
    Task ClearChatHistoryAsync(ulong userId, ulong channelId);
    Task<List<ChatHistory>> GetUserChatHistoryAsync(ulong userId, int count);

    // Bot settings operations
    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value, string? updatedBy = null);
    Task<Dictionary<string, string>> GetAllSettingsAsync();

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
}

