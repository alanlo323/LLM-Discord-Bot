namespace LLMDiscordBot.Models;

/// <summary>
/// Global statistics data model
/// </summary>
public class GlobalStats
{
    // Basic statistics
    public int TotalUsers { get; set; }
    public int ActiveUsersToday { get; set; }
    public int BlockedUsers { get; set; }
    
    // Today's usage
    public int TodayTokenUsage { get; set; }
    public int TodayMessageCount { get; set; }
    
    // Historical totals
    public long TotalTokenUsage { get; set; }
    public long TotalMessageCount { get; set; }
    
    // Averages
    public double AverageTokensPerUser { get; set; }
    public double AverageTokensPerMessage { get; set; }
    
    // Top users
    public List<TopUser> TopUsers { get; set; } = new();
    
    // Trends
    public List<DailyTrend> Last7DaysTrend { get; set; } = new();
    public List<DailyTrend> Last30DaysTrend { get; set; } = new();
    
    // Trend summaries
    public long Last7DaysTotal { get; set; }
    public long Last30DaysTotal { get; set; }
    public double Last7DaysAverage { get; set; }
    public double Last30DaysAverage { get; set; }
}

/// <summary>
/// Top user ranking data
/// </summary>
public class TopUser
{
    public ulong UserId { get; set; }
    public int TokensUsed { get; set; }
    public int MessageCount { get; set; }
    public int Rank { get; set; }
}

/// <summary>
/// Daily trend data for usage statistics
/// </summary>
public class DailyTrend
{
    public DateTime Date { get; set; }
    public int TokensUsed { get; set; }
    public int MessageCount { get; set; }
    public int ActiveUsers { get; set; }
}

