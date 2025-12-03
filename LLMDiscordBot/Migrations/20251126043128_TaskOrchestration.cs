using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LLMDiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class TaskOrchestration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: true),
                    ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ApprovalPolicy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AllowedWebsites = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    MemoryControllerKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PlanSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentStepSummary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastErrorAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonitoredTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MonitorType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TargetDescriptor = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ConditionJson = table.Column<string>(type: "TEXT", nullable: true),
                    CheckIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    NextCheckAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastCheckAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastResultSummary = table.Column<string>(type: "TEXT", nullable: true),
                    FailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoredTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoredTasks_TaskSessions_TaskSessionId",
                        column: x => x.TaskSessionId,
                        principalTable: "TaskSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskPlanSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "INTEGER", nullable: false),
                    ToolName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ToolArgumentsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ResultSummary = table.Column<string>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedBy = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskPlanSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskPlanSteps_TaskSessions_TaskSessionId",
                        column: x => x.TaskSessionId,
                        principalTable: "TaskSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActionApprovalLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskPlanStepId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ActionType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ActionSummary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RequestedBy = table.Column<ulong>(type: "INTEGER", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApproverUserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    RespondedBy = table.Column<ulong>(type: "INTEGER", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResponseNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ApprovalChannelContext = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionApprovalLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionApprovalLogs_TaskPlanSteps_TaskPlanStepId",
                        column: x => x.TaskPlanStepId,
                        principalTable: "TaskPlanSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ActionApprovalLogs_TaskSessions_TaskSessionId",
                        column: x => x.TaskSessionId,
                        principalTable: "TaskSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalDailyLimit",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 26, 4, 31, 27, 818, DateTimeKind.Utc).AddTicks(412));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalMaxTokens",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 26, 4, 31, 27, 818, DateTimeKind.Utc).AddTicks(411));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Model",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 26, 4, 31, 27, 818, DateTimeKind.Utc).AddTicks(408));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Temperature",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 26, 4, 31, 27, 818, DateTimeKind.Utc).AddTicks(410));

            migrationBuilder.CreateIndex(
                name: "IX_ActionApprovalLogs_RequestedBy_Status",
                table: "ActionApprovalLogs",
                columns: new[] { "RequestedBy", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionApprovalLogs_ApproverUserId_Status",
                table: "ActionApprovalLogs",
                columns: new[] { "ApproverUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionApprovalLogs_TaskPlanStepId",
                table: "ActionApprovalLogs",
                column: "TaskPlanStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionApprovalLogs_TaskSessionId_Status",
                table: "ActionApprovalLogs",
                columns: new[] { "TaskSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoredTasks_Status_NextCheckAt",
                table: "MonitoredTasks",
                columns: new[] { "Status", "NextCheckAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoredTasks_TaskSessionId",
                table: "MonitoredTasks",
                column: "TaskSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskPlanSteps_Status",
                table: "TaskPlanSteps",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TaskPlanSteps_TaskSessionId_SequenceNumber",
                table: "TaskPlanSteps",
                columns: new[] { "TaskSessionId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskSessions_UpdatedAt",
                table: "TaskSessions",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TaskSessions_UserId_GuildId_Status_IsArchived",
                table: "TaskSessions",
                columns: new[] { "UserId", "GuildId", "Status", "IsArchived" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionApprovalLogs");

            migrationBuilder.DropTable(
                name: "MonitoredTasks");

            migrationBuilder.DropTable(
                name: "TaskPlanSteps");

            migrationBuilder.DropTable(
                name: "TaskSessions");

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalDailyLimit",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 19, 17, 19, 7, 29, DateTimeKind.Utc).AddTicks(2183));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalMaxTokens",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 19, 17, 19, 7, 29, DateTimeKind.Utc).AddTicks(2181));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Model",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 19, 17, 19, 7, 29, DateTimeKind.Utc).AddTicks(2178));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Temperature",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 19, 17, 19, 7, 29, DateTimeKind.Utc).AddTicks(2180));
        }
    }
}
