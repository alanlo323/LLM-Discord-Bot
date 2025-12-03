using System.Linq;
using System.Text;
using System.Text.Json;
using LLMDiscordBot.Data;
using LLMDiscordBot.Models;
using Serilog;

namespace LLMDiscordBot.Services;

/// <summary>
/// Coordinates Magentic-UI inspired planning, step approvals, and monitoring sessions inside Discord.
/// </summary>
public class TaskOrchestrationService(IRepository repository, LLMService taskModelService, ILogger logger)
{
    private static readonly IReadOnlyDictionary<TaskSessionStatus, HashSet<TaskSessionStatus>> AllowedTransitions =
        new Dictionary<TaskSessionStatus, HashSet<TaskSessionStatus>>
        {
            [TaskSessionStatus.Draft] = new() { TaskSessionStatus.Ready, TaskSessionStatus.Cancelled },
            [TaskSessionStatus.Ready] = new() { TaskSessionStatus.Executing, TaskSessionStatus.Cancelled },
            [TaskSessionStatus.Executing] = new()
            {
                TaskSessionStatus.WaitingApproval,
                TaskSessionStatus.Paused,
                TaskSessionStatus.Completed,
                TaskSessionStatus.Failed
            },
            [TaskSessionStatus.WaitingApproval] = new() { TaskSessionStatus.Executing, TaskSessionStatus.Cancelled },
            [TaskSessionStatus.Paused] = new() { TaskSessionStatus.Executing, TaskSessionStatus.Cancelled },
            [TaskSessionStatus.Monitoring] = new()
            {
                TaskSessionStatus.MonitoringCompleted,
                TaskSessionStatus.Completed,
                TaskSessionStatus.Cancelled
            },
            [TaskSessionStatus.MonitoringCompleted] = new() { TaskSessionStatus.Completed },
            [TaskSessionStatus.Completed] = new() { },
            [TaskSessionStatus.Failed] = new() { },
            [TaskSessionStatus.Cancelled] = new() { }
        };

    public async Task<TaskSession> CreatePlanAsync(
        ulong userId,
        ulong channelId,
        ulong? guildId,
        string title,
        string? description,
        string? approvalPolicy = null,
        string? allowedWebsites = null)
    {
        var session = new TaskSession
        {
            UserId = userId,
            ChannelId = channelId,
            GuildId = guildId,
            Title = title,
            Description = description,
            ApprovalPolicy = approvalPolicy,
            AllowedWebsites = allowedWebsites
        };

        await repository.AddTaskSessionAsync(session);
        logger.Information("Created task session {SessionId} for user {UserId}", session.Id, userId);
        return session;
    }

    public async Task<IReadOnlyList<TaskSession>> GetRecentPlansAsync(ulong userId, int count = 5, bool includeArchived = false)
    {
        return await repository.GetUserTaskSessionsAsync(userId, count, includeArchived);
    }

    public async Task<TaskPlanStep> AddPlanStepAsync(
        Guid sessionId,
        ulong userId,
        string title,
        string? description,
        bool requiresApproval,
        string? toolName = null,
        string? toolArgumentsJson = null)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId);
        var existingSteps = await repository.GetTaskPlanStepsAsync(sessionId);
        var nextSequence = existingSteps.Count == 0 ? 1 : existingSteps.Max(step => step.SequenceNumber) + 1;

        var step = new TaskPlanStep
        {
            TaskSessionId = sessionId,
            SequenceNumber = nextSequence,
            Title = title,
            Description = description,
            RequiresApproval = requiresApproval,
            ToolName = toolName,
            ToolArgumentsJson = toolArgumentsJson,
            Status = session.Status == TaskSessionStatus.Executing
                ? TaskPlanStepStatus.Ready
                : TaskPlanStepStatus.Draft
        };

        await repository.AddTaskPlanStepAsync(step);
        session.CurrentStepSummary = $"{step.SequenceNumber}. {step.Title}";
        await repository.UpdateTaskSessionAsync(session);

        logger.Information("Added step {StepId} to session {SessionId} by user {UserId}", step.Id, sessionId, userId);
        return step;
    }

    public async Task<(TaskSession session, IReadOnlyList<TaskPlanStep> steps)> GetPlanDetailAsync(Guid sessionId, ulong userId)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId);
        var steps = await repository.GetTaskPlanStepsAsync(sessionId);
        return (session, steps);
    }

    public async Task<TaskSession> UpdateSessionStatusAsync(
        Guid sessionId,
        ulong userId,
        TaskSessionStatus nextStatus,
        string? summary = null)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId);
        if (!IsTransitionAllowed(session.Status, nextStatus))
        {
            throw new InvalidOperationException($"無法從 {session.Status} 切換到 {nextStatus}");
        }

        session.Status = nextStatus;
        session.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            session.CurrentStepSummary = summary;
        }

        if (nextStatus == TaskSessionStatus.Executing && session.StartedAt == null)
        {
            session.StartedAt = DateTime.UtcNow;
        }

        if (nextStatus is TaskSessionStatus.Completed or TaskSessionStatus.Cancelled or TaskSessionStatus.Failed)
        {
            session.CompletedAt = DateTime.UtcNow;
        }

        await repository.UpdateTaskSessionAsync(session);
        logger.Information("Session {SessionId} moved to {Status} by user {UserId}", sessionId, nextStatus, userId);
        return session;
    }

    public async Task ArchivePlanAsync(Guid sessionId, ulong userId)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId);
        session.IsArchived = true;
        session.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateTaskSessionAsync(session);
        logger.Information("Session {SessionId} archived by user {UserId}", sessionId, userId);
    }

    public async Task<ActionApprovalLog> RequestApprovalAsync(
        Guid sessionId,
        Guid? stepId,
        ulong operatorUserId,
        ulong approverUserId,
        string actionType,
        string summary,
        string? channelContext = null)
    {
        var session = await GetOwnedSessionAsync(sessionId, operatorUserId);

        var approval = new ActionApprovalLog
        {
            TaskSessionId = sessionId,
            TaskPlanStepId = stepId,
            ActionType = actionType,
            ActionSummary = summary,
            RequestedBy = operatorUserId,
            ApproverUserId = approverUserId,
            ApprovalChannelContext = channelContext
        };

        await repository.AddApprovalLogAsync(approval);
        logger.Information("Approval {ApprovalId} created for session {SessionId}", approval.Id, sessionId);
        return approval;
    }

    public async Task<ActionApprovalLog> ResolveApprovalAsync(Guid approvalId, ulong reviewerId, bool approved, string? notes = null)
    {
        var approval = await repository.GetApprovalLogAsync(approvalId)
            ?? throw new InvalidOperationException("找不到審批紀錄");

        if (approval.Status != ActionApprovalStatus.Pending)
        {
            throw new InvalidOperationException("此審批已處理");
        }

        approval.Status = approved ? ActionApprovalStatus.Approved : ActionApprovalStatus.Rejected;
        approval.RespondedBy = reviewerId;
        approval.RespondedAt = DateTime.UtcNow;
        approval.ResponseNotes = notes;

        await repository.UpdateApprovalLogAsync(approval);

        if (approval.TaskPlanStepId.HasValue)
        {
            var step = await repository.GetTaskPlanStepAsync(approval.TaskPlanStepId.Value);
            if (step != null)
            {
                step.Status = approved ? TaskPlanStepStatus.Approved : TaskPlanStepStatus.Rejected;
                step.ApprovedBy = reviewerId;
                step.ApprovedAt = DateTime.UtcNow;
                await repository.UpdateTaskPlanStepAsync(step);
            }
        }

        logger.Information("Approval {ApprovalId} resolved as {Status} by {UserId}", approval.Id, approval.Status, reviewerId);
        return approval;
    }

    public async Task<IReadOnlyList<ActionApprovalLog>> GetPendingApprovalsAsync(ulong approverUserId, int count = 10)
    {
        return await repository.GetPendingApprovalsAsync(approverUserId, count);
    }

    public async Task<MonitoredTask> ScheduleMonitorAsync(
        Guid sessionId,
        ulong userId,
        string monitorType,
        string? targetDescriptor,
        string? conditionJson,
        int checkIntervalMinutes = 30)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId);
        session.Status = TaskSessionStatus.Monitoring;
        await repository.UpdateTaskSessionAsync(session);

        var monitor = new MonitoredTask
        {
            TaskSessionId = sessionId,
            MonitorType = monitorType,
            TargetDescriptor = targetDescriptor,
            ConditionJson = conditionJson,
            CheckIntervalMinutes = checkIntervalMinutes,
            NextCheckAt = DateTime.UtcNow.AddMinutes(checkIntervalMinutes)
        };

        await repository.AddMonitoredTaskAsync(monitor);
        logger.Information("Monitoring scheduled for session {SessionId}", sessionId);
        return monitor;
    }

    public async Task<IReadOnlyList<TaskPlanStep>> GenerateStepsFromDescriptionAsync(
        TaskSession session,
        string taskDescription,
        int maxSteps,
        bool defaultRequiresApproval,
        CancellationToken cancellationToken = default)
    {
        maxSteps = Math.Clamp(maxSteps, 1, 10);

        var history = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        history.AddSystemMessage("""
You are an expert operations planner that outputs strict JSON only. 
Respond using the following schema:
{
  "steps": [
    {
      "title": "短句標題",
      "description": "具體操作細節 (繁體中文)",
      "tool": "web_surfer|coder|mcp|llm",
      "requires_approval": true|false
    }
  ]
}
If a tool is unclear, default to "llm". Limit steps to the requested count.
""");

        var allowed = string.IsNullOrWhiteSpace(session.AllowedWebsites)
            ? "無限制"
            : session.AllowedWebsites;

        history.AddUserMessage($"""
請根據以下任務描述制定最多 {maxSteps} 個可執行步驟：
任務描述：{taskDescription}
允許使用的網站：{allowed}
請使用繁體中文描述步驟，並依 JSON schema 回覆。
""");

        string response;
        try
        {
            var result = await taskModelService.GetTaskChatCompletionAsync(history, session.GuildId, cancellationToken);
            response = result.response;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to generate autorun steps via LLM");
            response = string.Empty;
        }

        var generatedSteps = ParseGeneratedSteps(response, maxSteps, defaultRequiresApproval);
        var createdSteps = new List<TaskPlanStep>();

        var existingSteps = await repository.GetTaskPlanStepsAsync(session.Id);
        var nextSequence = existingSteps.Count == 0 ? 1 : existingSteps.Max(step => step.SequenceNumber) + 1;

        foreach (var dto in generatedSteps)
        {
            var step = new TaskPlanStep
            {
                TaskSessionId = session.Id,
                SequenceNumber = nextSequence++,
                Title = dto.Title,
                Description = dto.Description,
                ToolName = dto.Tool,
                RequiresApproval = dto.RequiresApproval ?? defaultRequiresApproval,
                Status = TaskPlanStepStatus.Ready
            };

            await repository.AddTaskPlanStepAsync(step);
            createdSteps.Add(step);
        }

        if (createdSteps.Count == 0)
        {
            var fallbackStep = new TaskPlanStep
            {
                TaskSessionId = session.Id,
                SequenceNumber = nextSequence,
                Title = "分析與回報",
                Description = taskDescription,
                ToolName = "llm",
                RequiresApproval = defaultRequiresApproval,
                Status = TaskPlanStepStatus.Ready
            };
            await repository.AddTaskPlanStepAsync(fallbackStep);
            createdSteps.Add(fallbackStep);
        }

        session.CurrentStepSummary = $"{createdSteps.Count} steps generated";
        await repository.UpdateTaskSessionAsync(session);
        return createdSteps;
    }

    public async Task AppendConversationSnapshotAsync(Guid sessionId, string userMessage, string assistantMessage)
    {
        var session = await repository.GetTaskSessionAsync(sessionId);
        if (session == null)
        {
            return;
        }

        var builder = new StringBuilder(session.PlanSnapshot ?? string.Empty);
        builder.AppendLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] User:");
        builder.AppendLine(userMessage);
        builder.AppendLine("Assistant:");
        builder.AppendLine(assistantMessage);
        builder.AppendLine();

        session.PlanSnapshot = builder.ToString();
        session.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateTaskSessionAsync(session);
        logger.Debug("Updated plan snapshot for session {SessionId}", sessionId);
    }

    private static bool IsTransitionAllowed(TaskSessionStatus currentStatus, TaskSessionStatus nextStatus)
    {
        return AllowedTransitions.TryGetValue(currentStatus, out var allowed) && allowed.Contains(nextStatus);
    }

    private async Task<TaskSession> GetOwnedSessionAsync(Guid sessionId, ulong userId)
    {
        var session = await repository.GetTaskSessionAsync(sessionId)
            ?? throw new InvalidOperationException("找不到對應的計畫");

        if (session.UserId != userId)
        {
            throw new InvalidOperationException("您沒有權限操作此計畫");
        }

        return session;
    }

    private static IReadOnlyList<GeneratedStepDto> ParseGeneratedSteps(string? response, int maxSteps, bool defaultRequiresApproval)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return Array.Empty<GeneratedStepDto>();
        }

        try
        {
            var plan = JsonSerializer.Deserialize<GeneratedPlanDto>(
                response,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (plan?.Steps is { Count: > 0 })
            {
                return plan.Steps
                    .Select(s => s with
                    {
                        Title = string.IsNullOrWhiteSpace(s.Title) ? "未命名步驟" : s.Title.Trim(),
                        Description = string.IsNullOrWhiteSpace(s.Description) ? s.Title : s.Description.Trim(),
                        Tool = NormalizeToolName(s.Tool)
                    })
                    .Take(maxSteps)
                    .ToList();
            }
        }
        catch
        {
            // ignore parsing errors, fallback below
        }

        // fallback: treat response as bullet list
        var steps = new List<GeneratedStepDto>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int seq = 1;
        foreach (var line in lines)
        {
            if (seq > maxSteps)
                break;

            var cleaned = line.TrimStart('-', '*', ' ', '\t');
            if (string.IsNullOrWhiteSpace(cleaned))
                continue;

            steps.Add(new GeneratedStepDto
            {
                Title = cleaned.Length > 60 ? cleaned[..60] + "..." : cleaned,
                Description = cleaned,
                Tool = "llm",
                RequiresApproval = defaultRequiresApproval
            });
            seq++;
        }

        return steps;
    }

    private static string NormalizeToolName(string? rawTool)
    {
        if (string.IsNullOrWhiteSpace(rawTool))
            return "llm";

        return rawTool.Trim().ToLowerInvariant() switch
        {
            "web" or "websurfer" or "web_surfer" => "web_surfer",
            "coder" or "code" => "coder",
            "file" or "filesurfer" or "file_surfer" => "file_surfer",
            "mcp" or "api" => "mcp",
            _ => "llm"
        };
    }

    private sealed record GeneratedPlanDto(IReadOnlyList<GeneratedStepDto> Steps);

    private sealed record GeneratedStepDto
    {
        public string Title { get; init; } = "步驟";
        public string Description { get; init; } = string.Empty;
        public string Tool { get; init; } = "llm";
        public bool? RequiresApproval { get; init; }
    }
}

