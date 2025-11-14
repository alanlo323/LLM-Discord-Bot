using System.ComponentModel.DataAnnotations;

namespace LLMDiscordBot.Models;

/// <summary>
/// Bot settings entity for storing runtime configuration
/// </summary>
public class BotSettings
{
    [Key]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }
}

