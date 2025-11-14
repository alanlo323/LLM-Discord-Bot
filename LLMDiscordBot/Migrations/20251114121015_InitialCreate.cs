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
                    table.PrimaryKey("PKBotSettings", x => x.Key);
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
                    table.PrimaryKey("PKUsers", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "ChatHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PKChatHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FKChatHistoriesUsersUserId",
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
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokensUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PKTokenUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FKTokenUsagesUsersUserId",
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
                    { "GlobalDailyLimit", new DateTime(2025, 11, 14, 12, 10, 14, 505, DateTimeKind.Utc).AddTicks(2826), null, "100000" },
                    { "MaxTokens", new DateTime(2025, 11, 14, 12, 10, 14, 505, DateTimeKind.Utc).AddTicks(2824), null, "2000" },
                    { "Model", new DateTime(2025, 11, 14, 12, 10, 14, 505, DateTimeKind.Utc).AddTicks(2822), null, "default" },
                    { "SystemPrompt", new DateTime(2025, 11, 14, 12, 10, 14, 505, DateTimeKind.Utc).AddTicks(2825), null, "You are a helpful AI assistant." },
                    { "Temperature", new DateTime(2025, 11, 14, 12, 10, 14, 505, DateTimeKind.Utc).AddTicks(2823), null, "0.7" }
                });

            migrationBuilder.CreateIndex(
                name: "IXBotSettingsUpdatedAt",
                table: "BotSettings",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IXChatHistoriesUserIdChannelIdTimestamp",
                table: "ChatHistories",
                columns: new[] { "UserId", "ChannelId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IXTokenUsagesUserIdDate",
                table: "TokenUsages",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IXUsersCreatedAt",
                table: "Users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IXUsersIsBlocked",
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
                name: "TokenUsages");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
