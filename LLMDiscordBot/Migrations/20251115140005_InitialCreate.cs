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

            migrationBuilder.InsertData(
                table: "BotSettings",
                columns: new[] { "Key", "UpdatedAt", "UpdatedBy", "Value" },
                values: new object[,]
                {
                    { "GlobalDailyLimit", new DateTime(2025, 11, 15, 14, 0, 4, 908, DateTimeKind.Utc).AddTicks(1145), null, "100000" },
                    { "GlobalMaxTokens", new DateTime(2025, 11, 15, 14, 0, 4, 908, DateTimeKind.Utc).AddTicks(1143), null, "2000" },
                    { "GlobalSystemPrompt", new DateTime(2025, 11, 15, 14, 0, 4, 908, DateTimeKind.Utc).AddTicks(1144), null, "You are a helpful AI assistant." },
                    { "Model", new DateTime(2025, 11, 15, 14, 0, 4, 908, DateTimeKind.Utc).AddTicks(1141), null, "default" },
                    { "Temperature", new DateTime(2025, 11, 15, 14, 0, 4, 908, DateTimeKind.Utc).AddTicks(1142), null, "0.7" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BotSettings_UpdatedAt",
                table: "BotSettings",
                column: "UpdatedAt");

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
                name: "IX_TokenUsages_UserId_Date",
                table: "TokenUsages",
                columns: new[] { "UserId", "Date" },
                unique: true);

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
                name: "TokenUsages");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
