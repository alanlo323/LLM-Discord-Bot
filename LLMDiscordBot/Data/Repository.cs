using Microsoft.EntityFrameworkCore;
using LLMDiscordBot.Models;
using Serilog;

namespace LLMDiscordBot.Data;

/// <summary>
/// Repository implementation for database operations
/// </summary>
public class Repository : IRepository
{
    private readonly BotDbContext context;
    private readonly ILogger logger;

    public Repository(BotDbContext context, ILogger logger)
    {
        this.context = context;
        this.logger = logger;
    }

    #region User Operations

    public async Task<User?> GetUserAsync(ulong userId)
    {
        return await context.Users.FindAsync(userId);
    }

    public async Task<User> GetOrCreateUserAsync(ulong userId, int defaultDailyLimit)
    {
        var user = await GetUserAsync(userId);
        if (user == null)
        {
            user = new User
            {
                UserId = userId,
                DailyTokenLimit = defaultDailyLimit,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            logger.Information("Created new user {UserId} with daily limit {Limit}", userId, defaultDailyLimit);
        }

        user.LastAccessAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return user;
    }

    public async Task UpdateUserAsync(User user)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync();
        logger.Information("Updated user {UserId}", user.UserId);
    }

    #endregion

    #region Token Usage Operations

    public async Task<int> GetTodayTokenUsageAsync(ulong userId, DateTime today)
    {
        var dateOnly = today.Date;
        var usage = await context.TokenUsages
            .Where(t => t.UserId == userId && t.Date == dateOnly)
            .SumAsync(t => (int?)t.TokensUsed) ?? 0;
        return usage;
    }

    public async Task AddTokenUsageAsync(ulong userId, int tokens, DateTime date)
    {
        var dateOnly = date.Date;
        var existing = await context.TokenUsages
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Date == dateOnly);

        if (existing != null)
        {
            existing.TokensUsed += tokens;
            existing.MessageCount++;
            context.TokenUsages.Update(existing);
        }
        else
        {
            var usage = new TokenUsage
            {
                UserId = userId,
                Date = dateOnly,
                TokensUsed = tokens,
                MessageCount = 1,
                CreatedAt = DateTime.UtcNow
            };
            context.TokenUsages.Add(usage);
        }

        await context.SaveChangesAsync();
        logger.Debug("Added {Tokens} tokens for user {UserId} on {Date}", tokens, userId, dateOnly);
    }

    #endregion

    #region Chat History Operations

    public async Task AddChatHistoryAsync(ChatHistory history)
    {
        context.ChatHistories.Add(history);
        await context.SaveChangesAsync();
        logger.Debug("Added chat history for user {UserId} in channel {ChannelId}", 
            history.UserId, history.ChannelId);
    }

    public async Task<List<ChatHistory>> GetRecentChatHistoryAsync(ulong userId, ulong channelId, int count)
    {
        return await context.ChatHistories
            .Where(h => h.UserId == userId && h.ChannelId == channelId)
            .OrderByDescending(h => h.Timestamp)
            .Take(count)
            .OrderBy(h => h.Timestamp)
            .ToListAsync();
    }

    public async Task ClearChatHistoryAsync(ulong userId, ulong channelId)
    {
        var histories = await context.ChatHistories
            .Where(h => h.UserId == userId && h.ChannelId == channelId)
            .ToListAsync();

        context.ChatHistories.RemoveRange(histories);
        await context.SaveChangesAsync();
        logger.Information("Cleared chat history for user {UserId} in channel {ChannelId}", userId, channelId);
    }

    public async Task<List<ChatHistory>> GetUserChatHistoryAsync(ulong userId, int count)
    {
        return await context.ChatHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.Timestamp)
            .Take(count)
            .OrderBy(h => h.Timestamp)
            .ToListAsync();
    }

    #endregion

    #region Bot Settings Operations

    public async Task<string?> GetSettingAsync(string key)
    {
        var setting = await context.BotSettings.FindAsync(key);
        return setting?.Value;
    }

    public async Task SetSettingAsync(string key, string value, string? updatedBy = null)
    {
        var setting = await context.BotSettings.FindAsync(key);
        if (setting != null)
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedBy = updatedBy;
            context.BotSettings.Update(setting);
        }
        else
        {
            setting = new BotSettings
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = updatedBy
            };
            context.BotSettings.Add(setting);
        }

        await context.SaveChangesAsync();
        logger.Information("Updated setting {Key} = {Value} by {UpdatedBy}", key, value, updatedBy ?? "System");
    }

    public async Task<Dictionary<string, string>> GetAllSettingsAsync()
    {
        return await context.BotSettings
            .ToDictionaryAsync(s => s.Key, s => s.Value);
    }

    #endregion
}

