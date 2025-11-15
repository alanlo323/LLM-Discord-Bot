using System.ComponentModel.DataAnnotations;

namespace LLMDiscordBot.Models;

/// <summary>
/// Log of user interactions for habit learning
/// </summary>
public class InteractionLog
{
    [Key]
    public long Id { get; set; }

    public ulong UserId { get; set; }

    public ulong? GuildId { get; set; }

    [MaxLength(100)]
    public string CommandType { get; set; } = string.Empty; // e.g., "chat", "template", "export"

    public int MessageLength { get; set; }

    public int ResponseLength { get; set; }

    public TimeSpan ResponseTime { get; set; }

    [MaxLength(50)]
    public string? TopicCategory { get; set; }

    public bool UserSatisfied { get; set; } = true; // Based on feedback or follow-up questions

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Metadata { get; set; } // JSON for additional context
}

