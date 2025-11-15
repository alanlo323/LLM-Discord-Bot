using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLMDiscordBot.Models;

/// <summary>
/// User preferences and learned habits
/// </summary>
public class UserPreferences
{
    [Key]
    public ulong UserId { get; set; }

    // General preferences
    [MaxLength(50)]
    public string? PreferredLanguage { get; set; }

    public double? PreferredTemperature { get; set; }

    public int? PreferredMaxTokens { get; set; }

    [MaxLength(50)]
    public string? PreferredResponseStyle { get; set; } // e.g., "concise", "detailed", "casual", "formal"

    [MaxLength(1000)]
    public string? CustomSystemPrompt { get; set; }

    // Learned habits (automatically tracked)
    public int TotalInteractions { get; set; } = 0;

    public double AverageMessageLength { get; set; } = 0;

    [MaxLength(100)]
    public string? MostUsedTopics { get; set; } // JSON array of topics

    [MaxLength(50)]
    public string? PreferredTimeZone { get; set; }

    public bool EnableSmartSuggestions { get; set; } = true;

    public bool RememberConversationContext { get; set; } = true;

    // Interaction patterns
    public DateTime? LastInteractionAt { get; set; }

    public int ConsecutiveDays { get; set; } = 0;

    public TimeSpan? AverageSessionDuration { get; set; }

    [MaxLength(500)]
    public string? FavoriteCommands { get; set; } // JSON array of command usage frequency

    // Content preferences
    public bool PreferCodeExamples { get; set; } = false;

    public bool PreferStepByStep { get; set; } = false;

    public bool PreferVisualContent { get; set; } = false;

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("UserId")]
    public User? User { get; set; }
}

