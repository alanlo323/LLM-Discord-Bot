using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLMDiscordBot.Models;

/// <summary>
/// Represents a Magentic-UI style orchestration session persisted for Discord.
/// </summary>
public class TaskSession()
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [ForeignKey(nameof(User))]
    public ulong UserId { get; set; }

    public ulong? GuildId { get; set; }

    public ulong ChannelId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "New Plan";

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public TaskSessionStatus Status { get; set; } = TaskSessionStatus.Draft;

    [MaxLength(50)]
    public string? ApprovalPolicy { get; set; }

    [MaxLength(1000)]
    public string? AllowedWebsites { get; set; }

    [MaxLength(200)]
    public string? MemoryControllerKey { get; set; }

    public string? PlanSnapshot { get; set; }

    [MaxLength(500)]
    public string? CurrentStepSummary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? LastErrorAt { get; set; }

    [MaxLength(1000)]
    public string? LastError { get; set; }

    public bool IsArchived { get; set; }

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual ICollection<TaskPlanStep> Steps { get; set; } = new List<TaskPlanStep>();
    public virtual ICollection<ActionApprovalLog> ApprovalLogs { get; set; } = new List<ActionApprovalLog>();
    public virtual MonitoredTask? Monitor { get; set; }
}

public enum TaskSessionStatus
{
    Draft,
    Ready,
    Executing,
    WaitingApproval,
    Paused,
    Monitoring,
    MonitoringCompleted,
    Completed,
    Failed,
    Cancelled
}

