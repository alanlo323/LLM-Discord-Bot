using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLMDiscordBot.Models;

/// <summary>
/// Chat history entity for storing conversation messages
/// </summary>
public class ChatHistory
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(User))]
    public ulong UserId { get; set; }

    public ulong? GuildId { get; set; }

    public ulong ChannelId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = string.Empty; // "user" or "assistant"

    [Required]
    public string Content { get; set; } = string.Empty;

    public int TokenCount { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual User? User { get; set; }
}

