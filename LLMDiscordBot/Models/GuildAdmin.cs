using System.ComponentModel.DataAnnotations;

namespace LLMDiscordBot.Models;

/// <summary>
/// Guild administrator mapping entity
/// </summary>
public class GuildAdmin
{
    [Key]
    public int Id { get; set; }

    [Required]
    public ulong GuildId { get; set; }

    [Required]
    public ulong UserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }
}

