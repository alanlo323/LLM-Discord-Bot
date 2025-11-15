using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LLMDiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildIdToTokenUsageAndChatHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "GuildId",
                table: "TokenUsages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "GuildId",
                table: "ChatHistories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalDailyLimit",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 14, 46, 7, 549, DateTimeKind.Utc).AddTicks(4086));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalMaxTokens",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 14, 46, 7, 549, DateTimeKind.Utc).AddTicks(4084));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalSystemPrompt",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 14, 46, 7, 549, DateTimeKind.Utc).AddTicks(4085));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Model",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 14, 46, 7, 549, DateTimeKind.Utc).AddTicks(4082));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Temperature",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 14, 46, 7, 549, DateTimeKind.Utc).AddTicks(4083));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuildId",
                table: "TokenUsages");

            migrationBuilder.DropColumn(
                name: "GuildId",
                table: "ChatHistories");

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalDailyLimit",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 14, 0, 4, 908, DateTimeKind.Utc).AddTicks(1145));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalMaxTokens",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 14, 0, 4, 908, DateTimeKind.Utc).AddTicks(1143));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "GlobalSystemPrompt",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 14, 0, 4, 908, DateTimeKind.Utc).AddTicks(1144));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Model",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 14, 0, 4, 908, DateTimeKind.Utc).AddTicks(1141));

            migrationBuilder.UpdateData(
                table: "BotSettings",
                keyColumn: "Key",
                keyValue: "Temperature",
                column: "UpdatedAt",
                value: new DateTime(2025, 11, 15, 14, 0, 4, 908, DateTimeKind.Utc).AddTicks(1142));
        }
    }
}
