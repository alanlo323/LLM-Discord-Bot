using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LLMDiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintToTokenUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IXTokenUsagesUserIdDate",
                table: "TokenUsages");

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalDailyLimit",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 13, 54, 56, 420, DateTimeKind.Utc).AddTicks(6442));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "MaxTokens",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 13, 54, 56, 420, DateTimeKind.Utc).AddTicks(6440));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Model",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 13, 54, 56, 420, DateTimeKind.Utc).AddTicks(6437));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "SystemPrompt",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 13, 54, 56, 420, DateTimeKind.Utc).AddTicks(6441));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Temperature",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 13, 54, 56, 420, DateTimeKind.Utc).AddTicks(6439));

            migrationBuilder.CreateIndex(
                name: "IXTokenUsagesUserIdDate",
                table: "TokenUsages",
                columns: new[] { "UserId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IXTokenUsagesUserIdDate",
                table: "TokenUsages");

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalDailyLimit",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 12, 10, 14, 505, DateTimeKind.Utc).AddTicks(2826));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "MaxTokens",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 12, 10, 14, 505, DateTimeKind.Utc).AddTicks(2824));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Model",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 12, 10, 14, 505, DateTimeKind.Utc).AddTicks(2822));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "SystemPrompt",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 12, 10, 14, 505, DateTimeKind.Utc).AddTicks(2825));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Temperature",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 14, 12, 10, 14, 505, DateTimeKind.Utc).AddTicks(2823));

            migrationBuilder.CreateIndex(
                name: "IXTokenUsagesUserIdDate",
                table: "TokenUsages",
                columns: new[] { "UserId", "Date" });
        }
    }
}
