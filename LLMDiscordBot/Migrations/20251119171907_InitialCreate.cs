using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LLMDiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BotSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "GuildAdmins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildAdmins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuildSettings",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    SystemPrompt = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DailyLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    EnableLimits = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildSettings", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "InteractionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: true),
                    CommandType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MessageLength = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseLength = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    TopicCategory = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    UserSatisfied = table.Column<bool>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InteractionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyTokenLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    IsBlocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAccessAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "ChatHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: true),
                    ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TokenUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokensUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokenUsages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    PreferredLanguage = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PreferredTemperature = table.Column<double>(type: "REAL", nullable: true),
                    PreferredMaxTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    PreferredResponseStyle = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CustomSystemPrompt = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TotalInteractions = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageMessageLength = table.Column<double>(type: "REAL", nullable: false),
                    MostUsedTopics = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PreferredTimeZone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    EnableSmartSuggestions = table.Column<bool>(type: "INTEGER", nullable: false),
                    RememberConversationContext = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastInteractionAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConsecutiveDays = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageSessionDuration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    FavoriteCommands = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PreferCodeExamples = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreferStepByStep = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreferVisualContent = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "BotSettings",
                columns: new[] { "Key", "UpdatedAt", "UpdatedBy", "Value" },
                values: new object[,]
                {
                    { "GlobalDailyLimit", new DateTime(2025, 11, 19, 17, 19, 7, 29, DateTimeKind.Utc).AddTicks(2183), null, "100000" },
                    { "GlobalMaxTokens", new DateTime(2025, 11, 19, 17, 19, 7, 29, DateTimeKind.Utc).AddTicks(2181), null, "2000" },
                    { "Model", new DateTime(2025, 11, 19, 17, 19, 7, 29, DateTimeKind.Utc).AddTicks(2178), null, "default" },
                    { "Temperature", new DateTime(2025, 11, 19, 17, 19, 7, 29, DateTimeKind.Utc).AddTicks(2180), null, "0.7" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BotSettings_UpdatedAt",
                table: "BotSettings",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatHistories_GuildId",
                table: "ChatHistories",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatHistories_UserId_ChannelId_Timestamp",
                table: "ChatHistories",
                columns: new[] { "UserId", "ChannelId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildAdmins_GuildId",
                table: "GuildAdmins",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildAdmins_GuildId_UserId",
                table: "GuildAdmins",
                columns: new[] { "GuildId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildAdmins_UserId",
                table: "GuildAdmins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildSettings_UpdatedAt",
                table: "GuildSettings",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InteractionLogs_CommandType",
                table: "InteractionLogs",
                column: "CommandType");

            migrationBuilder.CreateIndex(
                name: "IX_InteractionLogs_GuildId",
                table: "InteractionLogs",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_InteractionLogs_Timestamp",
                table: "InteractionLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_InteractionLogs_UserId_Timestamp",
                table: "InteractionLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenUsages_Date",
                table: "TokenUsages",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_TokenUsages_GuildId",
                table: "TokenUsages",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_TokenUsages_UserId_GuildId_Date",
                table: "TokenUsages",
                columns: new[] { "UserId", "GuildId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_LastInteractionAt",
                table: "UserPreferences",
                column: "LastInteractionAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UpdatedAt",
                table: "UserPreferences",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedAt",
                table: "Users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsBlocked",
                table: "Users",
                column: "IsBlocked");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotSettings");

            migrationBuilder.DropTable(
                name: "ChatHistories");

            migrationBuilder.DropTable(
                name: "GuildAdmins");

            migrationBuilder.DropTable(
                name: "GuildSettings");

            migrationBuilder.DropTable(
                name: "InteractionLogs");

            migrationBuilder.DropTable(
                name: "TokenUsages");

            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
