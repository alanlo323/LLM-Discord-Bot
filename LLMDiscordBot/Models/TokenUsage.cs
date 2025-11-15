using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLMDiscordBot.Models;

/// <summary>
/// Token usage tracking entity
/// </summary>
public class TokenUsage
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(User))]
    public ulong UserId { get; set; }

    public ulong? GuildId { get; set; }

    public DateTime Date { get; set; }

    public int TokensUsed { get; set; }

    public int MessageCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual User? User { get; set; }
}

