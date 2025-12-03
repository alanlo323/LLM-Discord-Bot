using System.ComponentModel.DataAnnotations;

namespace LLMDiscordBot.Models;

/// <summary>
/// User entity for storing Discord user information
/// </summary>
public class User
{
    [Key]
    public ulong UserId { get; set; } // Discord User ID

    public int DailyTokenLimit { get; set; } = 100000;

    public bool IsBlocked { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastAccessAt { get; set; }

    // Navigation properties
    public virtual ICollection<TokenUsage> TokenUsages { get; set; } = new List<TokenUsage>();
    public virtual ICollection<ChatHistory> ChatHistories { get; set; } = new List<ChatHistory>();
    public virtual ICollection<TaskSession> TaskSessions { get; set; } = new List<TaskSession>();
}

