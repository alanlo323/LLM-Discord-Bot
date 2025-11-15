using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LLMDiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildSettingsAndAdmins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate existing custom settings (not seed data) to new keys
            // Only migrate if the old keys exist in the database (not from seed data)
            migrationBuilder.Sql(@"
                INSERT OR IGNORE INTO BotSettings (Key, Value, UpdatedAt, UpdatedBy)
                SELECT 'GlobalMaxTokens', Value, UpdatedAt, UpdatedBy
                FROM BotSettings
                WHERE Key = 'MaxTokens' AND UpdatedBy IS NOT NULL;
            ");

            migrationBuilder.Sql(@"
                INSERT OR IGNORE INTO BotSettings (Key, Value, UpdatedAt, UpdatedBy)
                SELECT 'GlobalSystemPrompt', Value, UpdatedAt, UpdatedBy
                FROM BotSettings
                WHERE Key = 'SystemPrompt' AND UpdatedBy IS NOT NULL;
            ");

            migrationBuilder.DeleteData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "MaxTokens");

            migrationBuilder.DeleteData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "SystemPrompt");

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

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalDailyLimit",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 12, 53, 32, 576, DateTimeKind.Utc).AddTicks(722));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Model",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 12, 53, 32, 576, DateTimeKind.Utc).AddTicks(718));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Temperature",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 12, 53, 32, 576, DateTimeKind.Utc).AddTicks(720));

            migrationBuilder.InsertData(
                table: "BotSettings",
                columns: new[] { "Key", "UpdatedAt", "UpdatedBy", "Value" },
                values: new object[,]
                {
                    { "GlobalMaxTokens", new DateTime(2025, 11, 15, 12, 53, 32, 576, DateTimeKind.Utc).AddTicks(721), null, "2000" },
                    { "GlobalSystemPrompt", new DateTime(2025, 11, 15, 12, 53, 32, 576, DateTimeKind.Utc).AddTicks(721), null, "You are a helpful AI assistant." }
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildAdmins");

            migrationBuilder.DropTable(
                name: "GuildSettings");

            migrationBuilder.DeleteData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalMaxTokens");

            migrationBuilder.DeleteData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalSystemPrompt");

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalDailyLimit",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 13, 54, 56, 420, DateTimeKind.Utc).AddTicks(6442));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Model",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 13, 54, 56, 420, DateTimeKind.Utc).AddTicks(6437));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Temperature",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 13, 54, 56, 420, DateTimeKind.Utc).AddTicks(6439));

            migrationBuilder.InsertData(
                table: "BotSettings",
                columns: new[] { "Key", "UpdatedAt", "UpdatedBy", "Value" },
                values: new object[,]
                {
                    { "MaxTokens", new DateTime(2025, 11, 14, 13, 54, 56, 420, DateTimeKind.Utc).AddTicks(6440), null, "2000" },
                    { "SystemPrompt", new DateTime(2025, 11, 14, 13, 54, 56, 420, DateTimeKind.Utc).AddTicks(6441), null, "You are a helpful AI assistant." }
                });
        }
    }
}
