using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLMDiscordBot.Models;

/// <summary>
/// Guild-specific settings entity
/// </summary>
public class GuildSettings
{
    [Key]
    public ulong GuildId { get; set; }

    [MaxLength(2000)]
    public string? SystemPrompt { get; set; }

    public int? DailyLimit { get; set; }

    public int? MaxTokens { get; set; }

    public bool EnableLimits { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }
}

