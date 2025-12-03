using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLMDiscordBot.Models;

/// <summary>
/// Represents a single orchestrated step within a task session plan.
/// </summary>
public class TaskPlanStep()
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [ForeignKey(nameof(Session))]
    public Guid TaskSessionId { get; set; }

    public int SequenceNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "Step";

    [MaxLength(2000)]
    public string? Description { get; set; }

    public TaskPlanStepStatus Status { get; set; } = TaskPlanStepStatus.Draft;

    public bool RequiresApproval { get; set; }

    [MaxLength(100)]
    public string? ToolName { get; set; }

    public string? ToolArgumentsJson { get; set; }

    public string? ResultSummary { get; set; }

    [MaxLength(1000)]
    public string? LastError { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public ulong? ApprovedBy { get; set; }

    public virtual TaskSession? Session { get; set; }
    public virtual ICollection<ActionApprovalLog> ApprovalLogs { get; set; } = new List<ActionApprovalLog>();
}

public enum TaskPlanStepStatus
{
    Draft,
    Ready,
    Running,
    WaitingApproval,
    Approved,
    Rejected,
    Completed,
    Failed,
    Skipped
}

