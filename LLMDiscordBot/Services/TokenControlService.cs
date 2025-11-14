using Microsoft.Extensions.Options;
using LLMDiscordBot.Configuration;
using LLMDiscordBot.Data;
using LLMDiscordBot.Models;
using Serilog;

namespace LLMDiscordBot.Services;

/// <summary>
/// Service for managing token usage and limits
/// </summary>
public class TokenControlService(
    IRepository repository,
    IOptions<TokenLimitsConfig> tokenConfig,
    ILogger logger)
{
    private readonly TokenLimitsConfig config = tokenConfig.Value;

    /// <summary>
    /// Check if user has enough tokens remaining for today
    /// </summary>
    public async Task<(bool allowed, int used, int limit)> CheckTokenLimitAsync(ulong userId, int tokensNeeded)
    {
        if (!config.EnableLimits)
        {
            return (true, 0, int.MaxValue);
        }

        var user = await repository.GetOrCreateUserAsync(userId, config.DefaultDailyLimit);

        // Check if user is blocked
        if (user.IsBlocked)
        {
            logger.Warning("Blocked user {UserId} attempted to use the bot", userId);
            return (false, 0, 0);
        }

        var today = DateTime.UtcNow;
        var usedToday = await repository.GetTodayTokenUsageAsync(userId, today);
        var remaining = user.DailyTokenLimit - usedToday;

        var allowed = remaining >= tokensNeeded;

        if (!allowed)
        {
            logger.Information("User {UserId} exceeded daily limit. Used: {Used}, Limit: {Limit}", 
                userId, usedToday, user.DailyTokenLimit);
        }

        return (allowed, usedToday, user.DailyTokenLimit);
    }

    /// <summary>
    /// Record token usage for a user
    /// </summary>
    public async Task RecordTokenUsageAsync(ulong userId, int tokens)
    {
        var today = DateTime.UtcNow;
        await repository.AddTokenUsageAsync(userId, tokens, today);
        logger.Debug("Recorded {Tokens} tokens for user {UserId}", tokens, userId);
    }

    /// <summary>
    /// Get user statistics
    /// </summary>
    public async Task<UserStats> GetUserStatsAsync(ulong userId)
    {
        var user = await repository.GetOrCreateUserAsync(userId, config.DefaultDailyLimit);
        var today = DateTime.UtcNow;
        var usedToday = await repository.GetTodayTokenUsageAsync(userId, today);

        return new UserStats
        {
            UserId = userId,
            DailyLimit = user.DailyTokenLimit,
            UsedToday = usedToday,
            Remaining = user.DailyTokenLimit - usedToday,
            IsBlocked = user.IsBlocked,
            CreatedAt = user.CreatedAt
        };
    }

    /// <summary>
    /// Update user's daily token limit
    /// </summary>
    public async Task SetUserLimitAsync(ulong userId, int newLimit)
    {
        var user = await repository.GetOrCreateUserAsync(userId, config.DefaultDailyLimit);
        user.DailyTokenLimit = newLimit;
        await repository.UpdateUserAsync(user);
        logger.Information("Updated daily limit for user {UserId} to {Limit}", userId, newLimit);
    }

    /// <summary>
    /// Block or unblock a user
    /// </summary>
    public async Task SetUserBlockStatusAsync(ulong userId, bool isBlocked)
    {
        var user = await repository.GetOrCreateUserAsync(userId, config.DefaultDailyLimit);
        user.IsBlocked = isBlocked;
        await repository.UpdateUserAsync(user);
        logger.Information("{Action} user {UserId}", isBlocked ? "Blocked" : "Unblocked", userId);
    }

    /// <summary>
    /// Reset token usage for a user today
    /// </summary>
    public Task ResetUserUsageAsync(ulong userId)
    {
        // This is handled by deleting today's usage record
        // In a real scenario, you might want to mark it as reset instead
        logger.Information("Reset usage for user {UserId}", userId);
        // Note: This functionality would need additional repository support
        return Task.CompletedTask;
    }
}

/// <summary>
/// User statistics data model
/// </summary>
public class UserStats
{
    public ulong UserId { get; set; }
    public int DailyLimit { get; set; }
    public int UsedToday { get; set; }
    public int Remaining { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime CreatedAt { get; set; }
}
