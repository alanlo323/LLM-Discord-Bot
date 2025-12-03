using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLMDiscordBot.Models;

/// <summary>
/// Stores approval records for guarded actions requested by the orchestrator.
/// </summary>
public class ActionApprovalLog()
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [ForeignKey(nameof(Session))]
    public Guid TaskSessionId { get; set; }

    [ForeignKey(nameof(Step))]
    public Guid? TaskPlanStepId { get; set; }

    public ActionApprovalStatus Status { get; set; } = ActionApprovalStatus.Pending;

    [MaxLength(200)]
    public string? ActionType { get; set; }

    [MaxLength(2000)]
    public string? ActionSummary { get; set; }

    public ulong RequestedBy { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public ulong ApproverUserId { get; set; }

    public ulong? RespondedBy { get; set; }

    public DateTime? RespondedAt { get; set; }

    [MaxLength(1000)]
    public string? ResponseNotes { get; set; }

    [MaxLength(500)]
    public string? ApprovalChannelContext { get; set; }

    public virtual TaskSession? Session { get; set; }
    public virtual TaskPlanStep? Step { get; set; }
}

public enum ActionApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Expired,
    Cancelled
}

