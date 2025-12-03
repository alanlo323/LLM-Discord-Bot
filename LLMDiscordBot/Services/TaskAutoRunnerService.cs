using LLMDiscordBot.Data;
using LLMDiscordBot.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.ChatCompletion;
using Serilog;

namespace LLMDiscordBot.Services;

/// <summary>
/// Executes TaskSessions automatically and streams progress back to Discord.
/// </summary>
public class TaskAutoRunnerService(IServiceScopeFactory scopeFactory, ILogger logger)
{
    public record AutoRunProgressUpdate(
        Guid SessionId,
        Guid StepId,
        int SequenceNumber,
        string StepTitle,
        TaskPlanStepStatus Status,
        string? Message = null,
        Guid? ApprovalId = null,
        ActionApprovalStatus? ApprovalStatus = null,
        bool IsFinal = false);

    public async Task AutoRunAsync(
        Guid sessionId,
        ulong ownerUserId,
        bool autoApprove,
        Func<AutoRunProgressUpdate, Task> progressCallback,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();
        var llmService = scope.ServiceProvider.GetRequiredService<LLMService>();

        var session = await repository.GetTaskSessionAsync(sessionId)
            ?? throw new InvalidOperationException("找不到計畫");

        if (session.UserId != ownerUserId)
        {
            throw new InvalidOperationException("您沒有權限執行此計畫");
        }

        var steps = (await repository.GetTaskPlanStepsAsync(sessionId))
            .OrderBy(s => s.SequenceNumber)
            .ToList();

        if (steps.Count == 0)
        {
            throw new InvalidOperationException("此計畫沒有可執行的步驟");
        }

        await SetSessionStatusAsync(repository, session, TaskSessionStatus.Executing, "Autorun started");

        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await UpdateStepStatusAsync(repository, step, TaskPlanStepStatus.Running, null);
            await progressCallback(new AutoRunProgressUpdate(
                session.Id,
                step.Id,
                step.SequenceNumber,
                step.Title,
                TaskPlanStepStatus.Running,
                "開始執行"));

            if (step.RequiresApproval && !autoApprove)
            {
                var approval = await CreateApprovalAsync(repository, session, step, ownerUserId);
                await progressCallback(new AutoRunProgressUpdate(
                    session.Id,
                    step.Id,
                    step.SequenceNumber,
                    step.Title,
                    TaskPlanStepStatus.WaitingApproval,
                    "等待審批...",
                    approval.Id,
                    approval.Status));

                var approvalStatus = await WaitForApprovalAsync(repository, approval.Id, cancellationToken);
                if (approvalStatus != ActionApprovalStatus.Approved)
                {
                    await UpdateStepStatusAsync(repository, step, TaskPlanStepStatus.Rejected, "審批被拒");
                    await SetSessionStatusAsync(repository, session, TaskSessionStatus.Failed, "審批被拒，已停止");
                    await progressCallback(new AutoRunProgressUpdate(
                        session.Id,
                        step.Id,
                        step.SequenceNumber,
                        step.Title,
                        TaskPlanStepStatus.Rejected,
                        "審批被拒，autorun 已停止",
                        approval.Id,
                        approvalStatus,
                        true));
                    return;
                }

                await SetSessionStatusAsync(repository, session, TaskSessionStatus.Executing, $"步驟 {step.SequenceNumber} 已核准");
                await progressCallback(new AutoRunProgressUpdate(
                    session.Id,
                    step.Id,
                    step.SequenceNumber,
                    step.Title,
                    TaskPlanStepStatus.Running,
                    "✅ 已獲得審批"));
            }

            try
            {
                var result = await ExecuteStepAsync(llmService, session, step, cancellationToken);
                await UpdateStepStatusAsync(repository, step, TaskPlanStepStatus.Completed, result);
                await progressCallback(new AutoRunProgressUpdate(
                    session.Id,
                    step.Id,
                    step.SequenceNumber,
                    step.Title,
                    TaskPlanStepStatus.Completed,
                    result));
            }
            catch (Exception ex)
            {
                var errorMessage = $"步驟失敗：{ex.Message}";
                await UpdateStepStatusAsync(repository, step, TaskPlanStepStatus.Failed, errorMessage);
                await SetSessionStatusAsync(repository, session, TaskSessionStatus.Failed, errorMessage);
                await progressCallback(new AutoRunProgressUpdate(
                    session.Id,
                    step.Id,
                    step.SequenceNumber,
                    step.Title,
                    TaskPlanStepStatus.Failed,
                    errorMessage,
                    null,
                    null,
                    true));
                return;
            }
        }

        await SetSessionStatusAsync(repository, session, TaskSessionStatus.Completed, "Autorun completed");
        await progressCallback(new AutoRunProgressUpdate(
            session.Id,
            Guid.Empty,
            0,
            "全部步驟",
            TaskPlanStepStatus.Completed,
            "✅ 所有步驟皆已完成",
            null,
            null,
            true));
    }

    private async Task<string> ExecuteStepAsync(LLMService llmService, TaskSession session, TaskPlanStep step, CancellationToken cancellationToken)
    {
        var history = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        history.AddSystemMessage("""
You are an autonomous agent executing a plan inside a Discord bot.
Summarize the concrete actions you would take for the given step.
If code or commands are needed, include them in well formatted markdown.
""");
        history.AddUserMessage($"""
計畫名稱：{session.Title}
步驟 {step.SequenceNumber}: {step.Title}
描述：{step.Description}
工具：{step.ToolName ?? "llm"}
請在 200 字以內說明你完成此步驟的結果與產出。
""");

        var result = await llmService.GetTaskChatCompletionAsync(history, session.GuildId, cancellationToken);
        return string.IsNullOrWhiteSpace(result.response)
            ? "（LLM 未產生任何內容）"
            : result.response.Trim();
    }

    private async Task UpdateStepStatusAsync(IRepository repository, TaskPlanStep step, TaskPlanStepStatus status, string? summary)
    {
        step.Status = status;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            step.ResultSummary = summary;
        }

        if (status == TaskPlanStepStatus.Completed)
        {
            step.CompletedAt = DateTime.UtcNow;
        }
        else if (status == TaskPlanStepStatus.Running)
        {
            step.StartedAt = DateTime.UtcNow;
        }

        await repository.UpdateTaskPlanStepAsync(step);
    }

    private async Task SetSessionStatusAsync(IRepository repository, TaskSession session, TaskSessionStatus status, string? summary)
    {
        session.Status = status;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            session.CurrentStepSummary = summary;
        }
        session.UpdatedAt = DateTime.UtcNow;

        if (status == TaskSessionStatus.Executing && session.StartedAt == null)
        {
            session.StartedAt = DateTime.UtcNow;
        }

        if (status is TaskSessionStatus.Completed or TaskSessionStatus.Failed or TaskSessionStatus.Cancelled)
        {
            session.CompletedAt = DateTime.UtcNow;
        }

        await repository.UpdateTaskSessionAsync(session);
    }

    private async Task<ActionApprovalLog> CreateApprovalAsync(IRepository repository, TaskSession session, TaskPlanStep step, ulong ownerUserId)
    {
        var approval = new ActionApprovalLog
        {
            TaskSessionId = session.Id,
            TaskPlanStepId = step.Id,
            RequestedBy = ownerUserId,
            ApproverUserId = ownerUserId,
            Status = ActionApprovalStatus.Pending,
            ActionType = $"Step {step.SequenceNumber}",
            ActionSummary = step.Description ?? step.Title
        };

        await repository.AddApprovalLogAsync(approval);
        session.Status = TaskSessionStatus.WaitingApproval;
        await repository.UpdateTaskSessionAsync(session);
        logger.Information("Autorun requested approval {ApprovalId} for session {SessionId}", approval.Id, session.Id);
        return approval;
    }

    private async Task<ActionApprovalStatus> WaitForApprovalAsync(IRepository repository, Guid approvalId, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = await repository.GetApprovalLogAsync(approvalId)
                ?? throw new InvalidOperationException("審批不存在");

            if (current.Status != ActionApprovalStatus.Pending)
            {
                return current.Status;
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }
}

