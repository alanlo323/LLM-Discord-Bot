using Microsoft.EntityFrameworkCore;
using LLMDiscordBot.Models;
using Serilog;

namespace LLMDiscordBot.Data;

/// <summary>
/// Repository implementation for database operations
/// </summary>
public class Repository(BotDbContext context, ILogger logger) : IRepository
{

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

    public async Task<int> GetUserTodayTokenUsageInGuildAsync(ulong userId, ulong guildId, DateTime today)
    {
        var dateOnly = today.Date;
        var usage = await context.TokenUsages
            .Where(t => t.UserId == userId && t.GuildId == guildId && t.Date == dateOnly)
            .SumAsync(t => (int?)t.TokensUsed) ?? 0;
        return usage;
    }

    public async Task AddTokenUsageAsync(ulong userId, int tokens, DateTime date, ulong? guildId = null)
    {
        var dateOnly = date.Date;

        // Use transaction to ensure atomic operation and prevent race conditions
        await using var transaction = await context.Database.BeginTransactionAsync();
        
        try
        {
            // Attempt atomic UPDATE first - this prevents read-modify-write race conditions
            // Handle both nullable and non-nullable GuildId comparison
            int rowsAffected;
            if (guildId.HasValue)
            {
                rowsAffected = await context.Database.ExecuteSqlRawAsync(
                    @"UPDATE TokenUsages 
                      SET TokensUsed = TokensUsed + {0}, MessageCount = MessageCount + 1 
                      WHERE UserId = {1} AND Date = {2} AND GuildId = {3}",
                    tokens, userId, dateOnly, guildId.Value);
            }
            else
            {
                rowsAffected = await context.Database.ExecuteSqlRawAsync(
                    @"UPDATE TokenUsages 
                      SET TokensUsed = TokensUsed + {0}, MessageCount = MessageCount + 1 
                      WHERE UserId = {1} AND Date = {2} AND GuildId IS NULL",
                    tokens, userId, dateOnly);
            }

            // If no rows were affected, the record doesn't exist - INSERT it
            if (rowsAffected == 0)
            {
                var usage = new TokenUsage
                {
                    UserId = userId,
                    GuildId = guildId,
                    Date = dateOnly,
                    TokensUsed = tokens,
                    MessageCount = 1,
                    CreatedAt = DateTime.UtcNow
                };
                context.TokenUsages.Add(usage);
                await context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            logger.Debug("Added {Tokens} tokens for user {UserId} on {Date} in guild {GuildId}", 
                tokens, userId, dateOnly, guildId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.Error(ex, "Error adding token usage for user {UserId}", userId);
            throw;
        }
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

    #region Guild Settings Operations

    public async Task<GuildSettings?> GetGuildSettingsAsync(ulong guildId)
    {
        return await context.GuildSettings.FindAsync(guildId);
    }

    public async Task<GuildSettings> GetOrCreateGuildSettingsAsync(ulong guildId)
    {
        var settings = await GetGuildSettingsAsync(guildId);
        if (settings == null)
        {
            settings = new GuildSettings
            {
                GuildId = guildId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                EnableLimits = true
            };
            context.GuildSettings.Add(settings);
            await context.SaveChangesAsync();
            logger.Information("Created default settings for guild {GuildId}", guildId);
        }
        return settings;
    }

    public async Task UpdateGuildSettingsAsync(GuildSettings settings)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        context.GuildSettings.Update(settings);
        await context.SaveChangesAsync();
        logger.Information("Updated settings for guild {GuildId}", settings.GuildId);
    }

    public async Task<bool> ValidateGuildLimitsAsync(ulong guildId, int? dailyLimit, int? maxTokens)
    {
        var globalDailyLimitStr = await GetSettingAsync("GlobalDailyLimit");
        var globalMaxTokensStr = await GetSettingAsync("GlobalMaxTokens");

        var globalDailyLimit = int.TryParse(globalDailyLimitStr, out var gDL) ? gDL : int.MaxValue;
        var globalMaxTokens = int.TryParse(globalMaxTokensStr, out var gMT) ? gMT : int.MaxValue;

        if (dailyLimit.HasValue && dailyLimit.Value > globalDailyLimit)
        {
            logger.Warning("Guild {GuildId} attempted to set DailyLimit {DailyLimit} above global limit {GlobalLimit}", 
                guildId, dailyLimit.Value, globalDailyLimit);
            return false;
        }

        if (maxTokens.HasValue && maxTokens.Value > globalMaxTokens)
        {
            logger.Warning("Guild {GuildId} attempted to set MaxTokens {MaxTokens} above global limit {GlobalLimit}", 
                guildId, maxTokens.Value, globalMaxTokens);
            return false;
        }

        return true;
    }

    public async Task<List<(GuildSettings guild, List<string> adjustments)>> AdjustGuildSettingsToGlobalLimitsAsync(int globalDailyLimit, int globalMaxTokens)
    {
        var adjustedGuilds = new List<(GuildSettings, List<string>)>();

        var guildsToAdjust = await context.GuildSettings
            .Where(g => (g.DailyLimit.HasValue && g.DailyLimit.Value > globalDailyLimit) ||
                       (g.MaxTokens.HasValue && g.MaxTokens.Value > globalMaxTokens))
            .ToListAsync();

        foreach (var guild in guildsToAdjust)
        {
            var adjustments = new List<string>();

            if (guild.DailyLimit.HasValue && guild.DailyLimit.Value > globalDailyLimit)
            {
                adjustments.Add($"DailyLimit: {guild.DailyLimit} -> {globalDailyLimit}");
                guild.DailyLimit = globalDailyLimit;
            }

            if (guild.MaxTokens.HasValue && guild.MaxTokens.Value > globalMaxTokens)
            {
                adjustments.Add($"MaxTokens: {guild.MaxTokens} -> {globalMaxTokens}");
                guild.MaxTokens = globalMaxTokens;
            }

            if (adjustments.Any())
            {
                guild.UpdatedAt = DateTime.UtcNow;
                guild.UpdatedBy = "System (Global Limit Adjustment)";
                adjustedGuilds.Add((guild, adjustments));
                logger.Information("Adjusted guild {GuildId} settings: {Adjustments}", 
                    guild.GuildId, string.Join(", ", adjustments));
            }
        }

        if (guildsToAdjust.Any())
        {
            await context.SaveChangesAsync();
        }

        return adjustedGuilds;
    }

    #endregion

    #region Guild Admin Operations

    public async Task<bool> IsGuildAdminAsync(ulong guildId, ulong userId)
    {
        return await context.GuildAdmins
            .AnyAsync(ga => ga.GuildId == guildId && ga.UserId == userId);
    }

    public async Task<List<GuildAdmin>> GetGuildAdminsAsync(ulong guildId)
    {
        return await context.GuildAdmins
            .Where(ga => ga.GuildId == guildId)
            .OrderBy(ga => ga.CreatedAt)
            .ToListAsync();
    }

    public async Task AddGuildAdminAsync(ulong guildId, ulong userId, string? createdBy = null)
    {
        // Check if already exists
        var exists = await IsGuildAdminAsync(guildId, userId);
        if (exists)
        {
            logger.Warning("User {UserId} is already an admin of guild {GuildId}", userId, guildId);
            return;
        }

        var guildAdmin = new GuildAdmin
        {
            GuildId = guildId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        context.GuildAdmins.Add(guildAdmin);
        await context.SaveChangesAsync();
        logger.Information("Added user {UserId} as admin of guild {GuildId} by {CreatedBy}", 
            userId, guildId, createdBy ?? "System");
    }

    public async Task RemoveGuildAdminAsync(ulong guildId, ulong userId)
    {
        var guildAdmin = await context.GuildAdmins
            .FirstOrDefaultAsync(ga => ga.GuildId == guildId && ga.UserId == userId);

        if (guildAdmin != null)
        {
            context.GuildAdmins.Remove(guildAdmin);
            await context.SaveChangesAsync();
            logger.Information("Removed user {UserId} as admin of guild {GuildId}", userId, guildId);
        }
    }

    #endregion

    #region Global Statistics Operations

    public async Task<int> GetTotalUsersCountAsync()
    {
        return await context.Users.CountAsync();
    }

    public async Task<int> GetBlockedUsersCountAsync()
    {
        return await context.Users.CountAsync(u => u.IsBlocked);
    }

    public async Task<int> GetActiveUsersTodayCountAsync(DateTime today)
    {
        var dateOnly = today.Date;
        return await context.TokenUsages
            .Where(t => t.Date == dateOnly)
            .Select(t => t.UserId)
            .Distinct()
            .CountAsync();
    }

    public async Task<long> GetTotalTokenUsageAsync()
    {
        return await context.TokenUsages.SumAsync(t => (long)t.TokensUsed);
    }

    public async Task<long> GetTotalMessageCountAsync()
    {
        return await context.TokenUsages.SumAsync(t => (long)t.MessageCount);
    }

    public async Task<int> GetTodayTokenUsageAsync(DateTime today)
    {
        var dateOnly = today.Date;
        return await context.TokenUsages
            .Where(t => t.Date == dateOnly)
            .SumAsync(t => (int?)t.TokensUsed) ?? 0;
    }

    public async Task<int> GetTodayMessageCountAsync(DateTime today)
    {
        var dateOnly = today.Date;
        return await context.TokenUsages
            .Where(t => t.Date == dateOnly)
            .SumAsync(t => (int?)t.MessageCount) ?? 0;
    }

    public async Task<List<TopUser>> GetTopUsersByTokenUsageAsync(DateTime date, int count)
    {
        var dateOnly = date.Date;
        var topUsers = await context.TokenUsages
            .Where(t => t.Date == dateOnly)
            .GroupBy(t => t.UserId)
            .Select(g => new TopUser
            {
                UserId = g.Key,
                TokensUsed = g.Sum(t => t.TokensUsed),
                MessageCount = g.Sum(t => t.MessageCount)
            })
            .OrderByDescending(u => u.TokensUsed)
            .Take(count)
            .ToListAsync();

        // Assign ranks
        for (int i = 0; i < topUsers.Count; i++)
        {
            topUsers[i].Rank = i + 1;
        }

        return topUsers;
    }

    public async Task<List<DailyTrend>> GetDailyTokenUsageTrendAsync(DateTime startDate, DateTime endDate)
    {
        var startDateOnly = startDate.Date;
        var endDateOnly = endDate.Date;

        var trendsFromDb = await context.TokenUsages
            .Where(t => t.Date >= startDateOnly && t.Date <= endDateOnly)
            .GroupBy(t => t.Date)
            .Select(g => new DailyTrend
            {
                Date = g.Key,
                TokensUsed = g.Sum(t => t.TokensUsed),
                MessageCount = g.Sum(t => t.MessageCount),
                ActiveUsers = g.Select(t => t.UserId).Distinct().Count()
            })
            .OrderBy(t => t.Date)
            .ToListAsync();

        // Fill in missing dates with zero values
        var allDates = new List<DailyTrend>();
        for (var date = startDateOnly; date <= endDateOnly; date = date.AddDays(1))
        {
            var existingTrend = trendsFromDb.FirstOrDefault(t => t.Date == date);
            if (existingTrend != null)
            {
                allDates.Add(existingTrend);
            }
            else
            {
                allDates.Add(new DailyTrend
                {
                    Date = date,
                    TokensUsed = 0,
                    MessageCount = 0,
                    ActiveUsers = 0
                });
            }
        }

        return allDates;
    }

    #endregion

    #region Guild Statistics Operations

    public async Task<int> GetGuildTodayTokenUsageAsync(ulong guildId, DateTime today)
    {
        var dateOnly = today.Date;
        return await context.TokenUsages
            .Where(t => t.GuildId == guildId && t.Date == dateOnly)
            .SumAsync(t => (int?)t.TokensUsed) ?? 0;
    }

    public async Task<int> GetGuildTodayMessageCountAsync(ulong guildId, DateTime today)
    {
        var dateOnly = today.Date;
        return await context.TokenUsages
            .Where(t => t.GuildId == guildId && t.Date == dateOnly)
            .SumAsync(t => (int?)t.MessageCount) ?? 0;
    }

    public async Task<int> GetGuildActiveUsersTodayCountAsync(ulong guildId, DateTime today)
    {
        var dateOnly = today.Date;
        return await context.TokenUsages
            .Where(t => t.GuildId == guildId && t.Date == dateOnly)
            .Select(t => t.UserId)
            .Distinct()
            .CountAsync();
    }

    public async Task<List<TopUser>> GetGuildTopUsersByTokenUsageAsync(ulong guildId, DateTime date, int count)
    {
        var dateOnly = date.Date;
        var topUsers = await context.TokenUsages
            .Where(t => t.GuildId == guildId && t.Date == dateOnly)
            .GroupBy(t => t.UserId)
            .Select(g => new TopUser
            {
                UserId = g.Key,
                TokensUsed = g.Sum(t => t.TokensUsed),
                MessageCount = g.Sum(t => t.MessageCount)
            })
            .OrderByDescending(u => u.TokensUsed)
            .Take(count)
            .ToListAsync();

        // Assign ranks
        for (int i = 0; i < topUsers.Count; i++)
        {
            topUsers[i].Rank = i + 1;
        }

        return topUsers;
    }

    public async Task<List<DailyTrend>> GetGuildDailyTokenUsageTrendAsync(ulong guildId, DateTime startDate, DateTime endDate)
    {
        var startDateOnly = startDate.Date;
        var endDateOnly = endDate.Date;

        var trendsFromDb = await context.TokenUsages
            .Where(t => t.GuildId == guildId && t.Date >= startDateOnly && t.Date <= endDateOnly)
            .GroupBy(t => t.Date)
            .Select(g => new DailyTrend
            {
                Date = g.Key,
                TokensUsed = g.Sum(t => t.TokensUsed),
                MessageCount = g.Sum(t => t.MessageCount),
                ActiveUsers = g.Select(t => t.UserId).Distinct().Count()
            })
            .OrderBy(t => t.Date)
            .ToListAsync();

        // Fill in missing dates with zero values
        var allDates = new List<DailyTrend>();
        for (var date = startDateOnly; date <= endDateOnly; date = date.AddDays(1))
        {
            var existingTrend = trendsFromDb.FirstOrDefault(t => t.Date == date);
            if (existingTrend != null)
            {
                allDates.Add(existingTrend);
            }
            else
            {
                allDates.Add(new DailyTrend
                {
                    Date = date,
                    TokensUsed = 0,
                    MessageCount = 0,
                    ActiveUsers = 0
                });
            }
        }

        return allDates;
    }

    public async Task<long> GetGuildTotalTokenUsageAsync(ulong guildId)
    {
        return await context.TokenUsages
            .Where(t => t.GuildId == guildId)
            .SumAsync(t => (long)t.TokensUsed);
    }

    public async Task<long> GetGuildTotalMessageCountAsync(ulong guildId)
    {
        return await context.TokenUsages
            .Where(t => t.GuildId == guildId)
            .SumAsync(t => (long)t.MessageCount);
    }

    #endregion
}

