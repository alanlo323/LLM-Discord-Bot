using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLMDiscordBot.Models;

/// <summary>
/// Represents a long running monitoring workflow derived from a task session.
/// </summary>
public class MonitoredTask()
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [ForeignKey(nameof(Session))]
    public Guid TaskSessionId { get; set; }

    [Required]
    [MaxLength(100)]
    public string MonitorType { get; set; } = "tell_me_when";

    [MaxLength(500)]
    public string? TargetDescriptor { get; set; }

    public string? ConditionJson { get; set; }

    public int CheckIntervalMinutes { get; set; } = 30;

    public DateTime NextCheckAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastCheckAt { get; set; }

    public MonitoringStatus Status { get; set; } = MonitoringStatus.Pending;

    public string? LastResultSummary { get; set; }

    public int FailureCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual TaskSession? Session { get; set; }
}

public enum MonitoringStatus
{
    Pending,
    Active,
    Waiting,
    Completed,
    Failed,
    Cancelled
}

