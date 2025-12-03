using Discord;
using Discord.Interactions;
using LLMDiscordBot.Models;
using LLMDiscordBot.Services;
using Serilog;
using System.Text;
using System.Threading;

namespace LLMDiscordBot.Commands;

/// <summary>
/// Discord slash commands that expose Magentic-UI style planning and approval flows.
/// </summary>
[Group("task", "Magentic-UI 互動式任務控制")]
public class TaskCommands(
    TaskOrchestrationService orchestrationService,
    TaskAutoRunnerService autoRunnerService,
    LLMService taskModelService,
    ILogger taskLogger) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TaskOrchestrationService orchestrationService = orchestrationService;
    private readonly TaskAutoRunnerService autoRunnerService = autoRunnerService;
    private readonly LLMService llmService = taskModelService;
    private readonly ILogger logger = taskLogger;

    [SlashCommand("plan-start", "建立新的共規劃任務")]
    public async Task StartPlanAsync(
        [Summary("title", "計畫標題")] string title,
        [Summary("description", "計畫描述")] string? description = null,
        [Summary("approval-policy", "審批策略")]
        [Choice("永不要求", "never")]
        [Choice("需要人工審批", "always")]
        string approvalPolicy = "never",
        [Summary("allowed-websites", "允許使用的網址清單")] string? allowedWebsites = null)
    {
        await DeferAsync(ephemeral: true);
        try
        {
            var session = await this.orchestrationService.CreatePlanAsync(
                Context.User.Id,
                Context.Channel.Id,
                Context.Guild?.Id,
                title,
                description,
                approvalPolicy,
                allowedWebsites);

            var embed = BuildSessionEmbed(session);
            if (!string.IsNullOrWhiteSpace(description))
            {
                var insights = await GeneratePlanInsightsAsync(title, description);
                if (!string.IsNullOrWhiteSpace(insights))
                {
                    embed.AddField("Fara-7B 建議", SafeTruncate(insights!, 700), inline: false);
                }
            }

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            this.logger.Error(ex, "Failed to create plan for user {UserId}", Context.User.Id);
            await FollowupAsync("建立計畫時發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("autorun", "輸入任務描述，系統自動規劃並執行")]
    public async Task AutorunAsync(
        [Summary("task", "任務描述，告訴我想完成什麼")] string taskDescription,
        [Summary("title", "自訂計畫名稱")] string? customTitle = null,
        [Summary("approval-policy", "預設審批策略")]
        [Choice("總是要求", "always")]
        [Choice("永不要求", "never")]
        string approvalPolicy = "always",
        [Summary("allowed-websites", "允許瀏覽的網站 (逗號分隔)")] string? allowedWebsites = null,
        [Summary("max-steps", "最大步驟數 (1-8)")] int maxSteps = 5,
        [Summary("auto-approve", "自動批准需要審批的步驟")] bool autoApprove = false)
    {
        await DeferAsync(ephemeral: false);

        maxSteps = Math.Clamp(maxSteps, 1, 8);
        var title = string.IsNullOrWhiteSpace(customTitle)
            ? $"Autorun - {SafeTruncate(taskDescription, 40)}"
            : customTitle;

        var requiresApprovalByDefault = approvalPolicy != "never";

        TaskSession session;
        try
        {
            session = await orchestrationService.CreatePlanAsync(
                Context.User.Id,
                Context.Channel.Id,
                Context.Guild?.Id,
                title,
                taskDescription,
                approvalPolicy,
                allowedWebsites);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to create autorun session");
            await FollowupAsync("建立 Autorun 任務失敗，請稍後再試。", ephemeral: true);
            return;
        }

        IReadOnlyList<TaskPlanStep> generatedSteps;
        try
        {
            generatedSteps = await orchestrationService.GenerateStepsFromDescriptionAsync(
                session,
                taskDescription,
                maxSteps,
                requiresApprovalByDefault);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to generate autorun steps");
            await FollowupAsync("無法從描述產生步驟，請縮短描述或稍後再試。", ephemeral: true);
            return;
        }

        var stepStates = generatedSteps.ToDictionary(
            s => s.Id,
            s => new StepProgressState(s.SequenceNumber, s.Title, s.Status, s.ResultSummary));

        var initialEmbed = BuildAutorunEmbed(
            session,
            stepStates.Values.OrderBy(s => s.Sequence).ToList(),
            "⚙️ 正在準備自動執行...",
            false);

        var progressMessage = await FollowupAsync(embed: initialEmbed, ephemeral: false);

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Content = $"Autorun 任務 `{session.Id}` 已建立，進度將同步在下方訊息。";
            msg.Embed = null;
        });

        var updateLock = new SemaphoreSlim(1, 1);
        var cancellationSource = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        async Task HandleProgress(TaskAutoRunnerService.AutoRunProgressUpdate update)
        {
            await updateLock.WaitAsync();
            try
            {
                if (update.StepId != Guid.Empty && stepStates.TryGetValue(update.StepId, out var state))
                {
                    state.Status = update.Status;
                    if (!string.IsNullOrWhiteSpace(update.Message))
                    {
                        state.LastMessage = update.Message;
                    }
                }

                var embed = BuildAutorunEmbed(
                    session,
                    stepStates.Values.OrderBy(s => s.Sequence).ToList(),
                    update.Message ?? "進度更新",
                    update.Status == TaskPlanStepStatus.WaitingApproval);

                await progressMessage.ModifyAsync(msg =>
                {
                    msg.Embed = embed;
                });
            }
            finally
            {
                updateLock.Release();
            }
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await autoRunnerService.AutoRunAsync(
                    session.Id,
                    Context.User.Id,
                    autoApprove,
                    HandleProgress,
                    cancellationSource.Token);
            }
            catch (OperationCanceledException)
            {
                await HandleProgress(new TaskAutoRunnerService.AutoRunProgressUpdate(
                    session.Id,
                    Guid.Empty,
                    0,
                    session.Title,
                    TaskPlanStepStatus.Failed,
                    "⏱️ 任務逾時，已停止",
                    null,
                    null,
                    true));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Autorun execution failed");
                await HandleProgress(new TaskAutoRunnerService.AutoRunProgressUpdate(
                    session.Id,
                    Guid.Empty,
                    0,
                    session.Title,
                    TaskPlanStepStatus.Failed,
                    $"❌ 任務執行失敗：{ex.Message}",
                    null,
                    null,
                    true));
            }
        });
    }

    [SlashCommand("plan-list", "檢視最近的計畫")]
    public async Task ListPlansAsync(
        [Summary("include-archived", "是否包含封存計畫")] bool includeArchived = false)
    {
        await DeferAsync(ephemeral: true);
        try
        {
            var plans = await this.orchestrationService.GetRecentPlansAsync(Context.User.Id, 5, includeArchived);
            if (plans.Count == 0)
            {
                await FollowupAsync("目前沒有計畫，先使用 /task plan-start 建立一個吧！", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithColor(Color.Purple)
                .WithTitle("📋 最近的計畫");

            foreach (var session in plans)
            {
                embed.AddField(
                    $"{session.Title} · {session.Status}",
                    $"ID: `{session.Id}`\n更新時間：{session.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC\n摘要：{session.CurrentStepSummary ?? "（尚未設定）"}",
                    inline: false);
            }

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            this.logger.Error(ex, "Failed to list plans for user {UserId}", Context.User.Id);
            await FollowupAsync("讀取計畫清單時發生錯誤。", ephemeral: true);
        }
    }

    [SlashCommand("plan-show", "顯示指定計畫與步驟")]
    public async Task ShowPlanAsync([Summary("session-id", "計畫 ID")] string sessionId)
    {
        await DeferAsync(ephemeral: true);
        if (!TryParseSessionId(sessionId, out var parsedId))
        {
            await FollowupAsync("請提供有效的計畫 ID。", ephemeral: true);
            return;
        }

        try
        {
            var (session, steps) = await this.orchestrationService.GetPlanDetailAsync(parsedId, Context.User.Id);
            var embed = BuildSessionEmbed(session);

            if (steps.Count == 0)
            {
                embed.AddField("步驟", "尚未建立任何步驟。", inline: false);
            }
            else
            {
                var builder = new StringBuilder();
                foreach (var step in steps)
                {
                    builder.AppendLine($"**{step.SequenceNumber}. {step.Title}** ({step.Status})");
                    if (!string.IsNullOrWhiteSpace(step.Description))
                    {
                        builder.AppendLine(step.Description);
                    }
                    if (step.RequiresApproval)
                    {
                        builder.AppendLine("• 需要審批");
                    }
                    builder.AppendLine();
                }
                embed.AddField("步驟", builder.ToString(), inline: false);
            }

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            this.logger.Error(ex, "Failed to show plan {SessionId}", sessionId);
            await FollowupAsync("無法讀取該計畫，請確認您擁有操作權限。", ephemeral: true);
        }
    }

    [SlashCommand("plan-add-step", "新增計畫步驟")]
    public async Task AddPlanStepAsync(
        [Summary("session-id", "計畫 ID")] string sessionId,
        [Summary("title", "步驟標題")] string title,
        [Summary("description", "步驟說明")] string? description = null,
        [Summary("requires-approval", "是否需要審批")] bool requiresApproval = false,
        [Summary("tool-name", "工具代號")] string? toolName = null,
        [Summary("tool-arguments", "工具參數 (JSON)")] string? toolArguments = null)
    {
        await DeferAsync(ephemeral: true);
        if (!TryParseSessionId(sessionId, out var parsedId))
        {
            await FollowupAsync("請提供有效的計畫 ID。", ephemeral: true);
            return;
        }

        try
        {
            var step = await this.orchestrationService.AddPlanStepAsync(
                parsedId,
                Context.User.Id,
                title,
                description,
                requiresApproval,
                toolName,
                toolArguments);

            var embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle($"✅ 已新增步驟 #{step.SequenceNumber}")
                .WithDescription(step.Title)
                .AddField("狀態", step.Status.ToString(), inline: true)
                .AddField("需要審批", requiresApproval ? "是" : "否", inline: true);

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            this.logger.Error(ex, "Failed to add step for session {SessionId}", sessionId);
            await FollowupAsync("新增步驟時發生錯誤，請確認您擁有操作權限。", ephemeral: true);
        }
    }

    [SlashCommand("plan-status", "更新計畫狀態")]
    public async Task UpdatePlanStatusAsync(
        [Summary("session-id", "計畫 ID")] string sessionId,
        [Summary("status", "新狀態")]
        [Choice("草稿", "Draft")]
        [Choice("已準備", "Ready")]
        [Choice("執行中", "Executing")]
        [Choice("待審批", "WaitingApproval")]
        [Choice("已暫停", "Paused")]
        [Choice("監控中", "Monitoring")]
        [Choice("監控完成", "MonitoringCompleted")]
        [Choice("已完成", "Completed")]
        [Choice("失敗", "Failed")]
        [Choice("已取消", "Cancelled")]
        string newStatus,
        [Summary("summary", "狀態摘要")] string? summary = null)
    {
        await DeferAsync(ephemeral: true);
        if (!TryParseSessionId(sessionId, out var parsedId))
        {
            await FollowupAsync("請提供有效的計畫 ID。", ephemeral: true);
            return;
        }

        if (!Enum.TryParse<TaskSessionStatus>(newStatus, out var parsedStatus))
        {
            await FollowupAsync("無法辨識的狀態。", ephemeral: true);
            return;
        }

        try
        {
            var session = await this.orchestrationService.UpdateSessionStatusAsync(parsedId, Context.User.Id, parsedStatus, summary);
            await FollowupAsync(embed: BuildSessionEmbed(session).Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            this.logger.Error(ex, "Failed to update status for session {SessionId}", sessionId);
            await FollowupAsync("更新狀態時發生錯誤，請確認轉換是否合法。", ephemeral: true);
        }
    }

    [SlashCommand("plan-archive", "封存計畫")]
    public async Task ArchivePlanAsync([Summary("session-id", "計畫 ID")] string sessionId)
    {
        await DeferAsync(ephemeral: true);
        if (!TryParseSessionId(sessionId, out var parsedId))
        {
            await FollowupAsync("請提供有效的計畫 ID。", ephemeral: true);
            return;
        }

        try
        {
            await this.orchestrationService.ArchivePlanAsync(parsedId, Context.User.Id);
            await FollowupAsync($"計畫 `{parsedId}` 已封存。", ephemeral: true);
        }
        catch (Exception ex)
        {
            this.logger.Error(ex, "Failed to archive session {SessionId}", sessionId);
            await FollowupAsync("封存計畫時發生錯誤。", ephemeral: true);
        }
    }

    [SlashCommand("approval-pending", "列出待審批項目")]
    public async Task PendingApprovalsAsync()
    {
        await DeferAsync(ephemeral: true);
        try
        {
            var approvals = await this.orchestrationService.GetPendingApprovalsAsync(Context.User.Id);
            if (approvals.Count == 0)
            {
                await FollowupAsync("目前沒有等待您審批的項目。", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithColor(Color.Orange)
                .WithTitle("🛡️ 待審批項目");

            foreach (var approval in approvals)
            {
                var description = new StringBuilder()
                    .AppendLine(approval.ActionSummary ?? string.Empty)
                    .AppendLine($"請求者：<@{approval.RequestedBy}>")
                    .AppendLine($"建立時間：{approval.RequestedAt:yyyy-MM-dd HH:mm:ss} UTC");
                embed.AddField($"ID: {approval.Id}", description.ToString(), inline: false);
            }

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            this.logger.Error(ex, "Failed to load pending approvals for {UserId}", Context.User.Id);
            await FollowupAsync("讀取審批清單時發生錯誤。", ephemeral: true);
        }
    }

    [SlashCommand("approval-resolve", "處理審批請求")]
    public async Task ResolveApprovalAsync(
        [Summary("approval-id", "審批 ID")] string approvalId,
        [Summary("decision", "審批決策")]
        [Choice("核准", "approve")]
        [Choice("拒絕", "reject")]
        string decision,
        [Summary("notes", "備註")] string? notes = null)
    {
        await DeferAsync(ephemeral: true);
        if (!Guid.TryParse(approvalId, out var parsedId))
        {
            await FollowupAsync("請提供有效的審批 ID。", ephemeral: true);
            return;
        }

        try
        {
            var approved = string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase);
            var result = await this.orchestrationService.ResolveApprovalAsync(parsedId, Context.User.Id, approved, notes);
            await FollowupAsync($"審批 `{result.Id}` 已標記為 **{result.Status}**。", ephemeral: true);
        }
        catch (Exception ex)
        {
            this.logger.Error(ex, "Failed to resolve approval {ApprovalId}", approvalId);
            await FollowupAsync("處理審批時發生錯誤，請確認此項目仍待處理。", ephemeral: true);
        }
    }

    private static bool TryParseSessionId(string? raw, out Guid sessionId)
    {
        return Guid.TryParse(raw, out sessionId);
    }

    private async Task<string?> GeneratePlanInsightsAsync(string title, string description)
    {
        try
        {
            var history = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            history.AddSystemMessage("You are Fara-7B Task Planner. Generate concise bullet list suggestions in Traditional Chinese.");
            history.AddUserMessage($"計畫名稱：{title}\n描述：{description}\n請輸出 2-4 條建議步驟，以項目符號呈現。");

            var taskResult = await llmService.GetTaskChatCompletionAsync(history);
            return string.IsNullOrWhiteSpace(taskResult.response) ? null : taskResult.response.Trim();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to generate Fara suggestions");
            return null;
        }
    }

    private static EmbedBuilder BuildSessionEmbed(TaskSession session)
    {
        return new EmbedBuilder()
            .WithColor(Color.Teal)
            .WithTitle($"🟪 {session.Title}")
            .WithDescription(session.Description ?? "（無描述）")
            .AddField("ID", session.Id.ToString(), inline: false)
            .AddField("狀態", session.Status.ToString(), inline: true)
            .AddField("審批策略", session.ApprovalPolicy ?? "未設定", inline: true)
            .AddField("摘要", session.CurrentStepSummary ?? "尚未開始", inline: false)
            .WithFooter($"最後更新：{session.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC");
    }

    private static Embed BuildAutorunEmbed(TaskSession session, IReadOnlyList<StepProgressState> steps, string? headline, bool waitingApproval)
    {
        var builder = new EmbedBuilder()
            .WithColor(waitingApproval ? Color.Orange : Color.Blue)
            .WithTitle($"⚙️ Autorun · {session.Title}")
            .WithDescription(headline ?? "正在執行中...")
            .AddField("任務 ID", session.Id.ToString(), true)
            .AddField("狀態", session.Status.ToString(), true)
            .AddField("審批策略", session.ApprovalPolicy ?? "default", true);

        foreach (var state in steps)
        {
            var statusLine = $"{GetStatusEmoji(state.Status)} {state.Status}";
            var detail = string.IsNullOrWhiteSpace(state.LastMessage) ? "等待執行..." : state.LastMessage;
            builder.AddField($"步驟 {state.Sequence}. {state.Title}", $"{statusLine}\n{detail}", inline: false);
        }

        return builder.Build();
    }

    private static string GetStatusEmoji(TaskPlanStepStatus status) => status switch
    {
        TaskPlanStepStatus.Completed => "✅",
        TaskPlanStepStatus.Running => "⏳",
        TaskPlanStepStatus.WaitingApproval => "🛑",
        TaskPlanStepStatus.Failed => "❌",
        TaskPlanStepStatus.Rejected => "🚫",
        TaskPlanStepStatus.Approved => "👍",
        _ => "▪️"
    };

    private static string SafeTruncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;
        return value[..maxLength] + "…";
    }

    private sealed class StepProgressState
    {
        public StepProgressState(int sequence, string title, TaskPlanStepStatus status, string? message)
        {
            Sequence = sequence;
            Title = title;
            Status = status;
            LastMessage = message;
        }

        public int Sequence { get; }
        public string Title { get; }
        public TaskPlanStepStatus Status { get; set; }
        public string? LastMessage { get; set; }
    }
}

