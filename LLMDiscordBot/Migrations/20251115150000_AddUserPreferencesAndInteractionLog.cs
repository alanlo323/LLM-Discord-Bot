using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LLMDiscordBot.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferencesAndInteractionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create UserPreferences table
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
                    TotalInteractions = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    AverageMessageLength = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0),
                    MostUsedTopics = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PreferredTimeZone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    EnableSmartSuggestions = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    RememberConversationContext = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    LastInteractionAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConsecutiveDays = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    AverageSessionDuration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    FavoriteCommands = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PreferCodeExamples = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    PreferStepByStep = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    PreferVisualContent = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
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

            // Create InteractionLogs table
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
                    UserSatisfied = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InteractionLogs", x => x.Id);
                });

            // Create indexes for UserPreferences
            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_LastInteractionAt",
                table: "UserPreferences",
                column: "LastInteractionAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UpdatedAt",
                table: "UserPreferences",
                column: "UpdatedAt");

            // Create indexes for InteractionLogs
            migrationBuilder.CreateIndex(
                name: "IX_InteractionLogs_UserId_Timestamp",
                table: "InteractionLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_InteractionLogs_GuildId",
                table: "InteractionLogs",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_InteractionLogs_CommandType",
                table: "InteractionLogs",
                column: "CommandType");

            migrationBuilder.CreateIndex(
                name: "IX_InteractionLogs_Timestamp",
                table: "InteractionLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserPreferences");
            migrationBuilder.DropTable(name: "InteractionLogs");
        }
    }
}

