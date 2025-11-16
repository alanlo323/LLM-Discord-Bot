using Discord;
using Discord.Interactions;
using LLMDiscordBot.Services;
using LLMDiscordBot.Data;
using Serilog;

namespace LLMDiscordBot.Commands;

/// <summary>
/// User commands for viewing personal stats and history
/// </summary>
public class UserCommands(
    TokenControlService tokenControl,
    IRepository repository,
    ILogger logger) : InteractionModuleBase<SocketInteractionContext>
{

    [SlashCommand("mystats", "查看您的使用統計")]
    public async Task MyStatsAsync()
    {
        try
        {
            var userId = Context.User.Id;
            var guildId = Context.Guild?.Id;
            var stats = await tokenControl.GetUserStatsAsync(userId, guildId);

            var percentage = stats.DailyLimit > 0 
                ? (stats.UsedToday * 100.0 / stats.DailyLimit) 
                : 0;

            var progressBar = GenerateProgressBar(percentage, 20);

            var embed = new EmbedBuilder()
                .WithColor(percentage >= 90 ? Color.Red : percentage >= 70 ? Color.Orange : Color.Green)
                .WithTitle("📊 您的使用統計")
                .WithThumbnailUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                .AddField("今日使用量", $"```\n{progressBar}\n{stats.UsedToday:N0} / {stats.DailyLimit:N0} tokens ({percentage:F1}%)\n```", false)
                .AddField("剩餘額度", $"{stats.Remaining:N0} tokens", true)
                .AddField("每日限額", $"{stats.DailyLimit:N0} tokens", true)
                .AddField("帳戶狀態", stats.IsBlocked ? "🔒 已封鎖" : "✅ 正常", true)
                .WithFooter($"帳戶建立於 {stats.CreatedAt:yyyy-MM-dd}")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);

            logger.Information("User {UserId} checked their stats", userId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting user stats");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("myhistory", "查看您最近的聊天記錄")]
    public async Task MyHistoryAsync(
        [Summary("count", "要顯示的訊息數量 (預設: 10, 最多: 50)")]
        [MinValue(1)]
        [MaxValue(50)]
        int count = 10)
    {
        try
        {
            var userId = Context.User.Id;
            var history = await repository.GetUserChatHistoryAsync(userId, count);

            if (history.Count == 0)
            {
                await RespondAsync(
                    embed: new EmbedBuilder()
                        .WithColor(Color.Blue)
                        .WithTitle("📜 聊天記錄")
                        .WithDescription("您還沒有任何聊天記錄。")
                        .Build(),
                    ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle($"📜 最近 {history.Count} 條聊天記錄")
                .WithFooter($"共使用 {history.Sum(h => h.TokenCount):N0} tokens");

            var description = "";
            foreach (var item in history)
            {
                var roleIcon = item.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "👤" : "🤖";
                var timestamp = item.Timestamp.ToString("MM/dd HH:mm");
                var preview = item.Content.Length > 100 
                    ? item.Content[..100] + "..." 
                    : item.Content;

                description += $"**{roleIcon} {item.Role}** - {timestamp} ({item.TokenCount} tokens)\n";
                description += $"> {preview}\n\n";

                // Discord embed description limit is 4096 characters
                if (description.Length > 3800)
                {
                    description += "*...更多記錄未顯示*";
                    break;
                }
            }

            embed.WithDescription(description);

            await RespondAsync(embed: embed.Build(), ephemeral: true);

            logger.Information("User {UserId} viewed their chat history", userId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting user history");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    private static string GenerateProgressBar(double percentage, int length)
    {
        var filled = (int)Math.Round(percentage / 100 * length);
        var empty = length - filled;

        var bar = "";
        for (int i = 0; i < filled; i++)
        {
            bar += "█";
        }
        for (int i = 0; i < empty; i++)
        {
            bar += "░";
        }

        return bar;
    }
}

