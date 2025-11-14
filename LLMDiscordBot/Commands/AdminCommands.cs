using Discord;
using Discord.Interactions;
using LLMDiscordBot.Services;
using LLMDiscordBot.Data;
using Serilog;

namespace LLMDiscordBot.Commands;

/// <summary>
/// Admin commands for managing users and bot settings
/// </summary>
[Group("admin", "管理員命令")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public class AdminCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TokenControlService tokenControl;
    private readonly IRepository repository;
    private readonly ILogger logger;

    public AdminCommands(
        TokenControlService tokenControl,
        IRepository repository,
        ILogger logger)
    {
        this.tokenControl = tokenControl;
        this.repository = repository;
        this.logger = logger;
    }

    #region User Management

    [SlashCommand("user-stats", "查看指定用戶的使用統計")]
    public async Task UserStatsAsync(
        [Summary("user", "要查看的用戶")]
        IUser user)
    {
        try
        {
            var stats = await tokenControl.GetUserStatsAsync(user.Id);

            var percentage = stats.DailyLimit > 0
                ? (stats.UsedToday * 100.0 / stats.DailyLimit)
                : 0;

            var embed = new EmbedBuilder()
                .WithColor(stats.IsBlocked ? Color.Red : Color.Blue)
                .WithTitle($"📊 {user.Username} 的使用統計")
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .AddField("用戶 ID", user.Id, true)
                .AddField("今日使用", $"{stats.UsedToday:N0} tokens", true)
                .AddField("每日限額", $"{stats.DailyLimit:N0} tokens", true)
                .AddField("剩餘額度", $"{stats.Remaining:N0} tokens", true)
                .AddField("使用百分比", $"{percentage:F1}%", true)
                .AddField("帳戶狀態", stats.IsBlocked ? "🔒 已封鎖" : "✅ 正常", true)
                .WithFooter($"帳戶建立於 {stats.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            logger.Information("Admin {AdminId} viewed stats for user {UserId}", Context.User.Id, user.Id);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting user stats");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("set-limit", "設定用戶的每日 Token 額度")]
    public async Task SetLimitAsync(
        [Summary("user", "要設定的用戶")]
        IUser user,
        [Summary("tokens", "每日 Token 額度")]
        [MinValue(0)]
        int tokens)
    {
        try
        {
            await tokenControl.SetUserLimitAsync(user.Id, tokens);

            var embed = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithTitle("✅ 額度已更新")
                .WithDescription($"已將 {user.Mention} 的每日 Token 額度設定為 **{tokens:N0}** tokens。")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            logger.Information("Admin {AdminId} set limit for user {UserId} to {Tokens}",
                Context.User.Id, user.Id, tokens);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting user limit");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("reset-usage", "重置用戶今日的使用量")]
    public async Task ResetUsageAsync(
        [Summary("user", "要重置的用戶")]
        IUser user)
    {
        try
        {
            await tokenControl.ResetUserUsageAsync(user.Id);

            var embed = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithTitle("✅ 使用量已重置")
                .WithDescription($"已重置 {user.Mention} 今日的使用量。")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            logger.Information("Admin {AdminId} reset usage for user {UserId}", Context.User.Id, user.Id);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error resetting user usage");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("block", "封鎖用戶")]
    public async Task BlockAsync(
        [Summary("user", "要封鎖的用戶")]
        IUser user)
    {
        try
        {
            await tokenControl.SetUserBlockStatusAsync(user.Id, true);

            var embed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle("🔒 用戶已封鎖")
                .WithDescription($"已封鎖 {user.Mention}，該用戶將無法使用 Bot。")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            logger.Warning("Admin {AdminId} blocked user {UserId}", Context.User.Id, user.Id);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error blocking user");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("unblock", "解封用戶")]
    public async Task UnblockAsync(
        [Summary("user", "要解封的用戶")]
        IUser user)
    {
        try
        {
            await tokenControl.SetUserBlockStatusAsync(user.Id, false);

            var embed = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithTitle("✅ 用戶已解封")
                .WithDescription($"已解封 {user.Mention}，該用戶現在可以使用 Bot。")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            logger.Information("Admin {AdminId} unblocked user {UserId}", Context.User.Id, user.Id);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error unblocking user");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    #endregion

    #region Bot Settings

    [SlashCommand("set-model", "設定 LLM 模型名稱")]
    public async Task SetModelAsync(
        [Summary("model", "模型名稱")]
        string model)
    {
        try
        {
            await repository.SetSettingAsync("Model", model, Context.User.Username);

            var embed = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithTitle("✅ 設定已更新")
                .WithDescription($"已將 LLM 模型設定為 **{model}**。")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            logger.Information("Admin {AdminId} set model to {Model}", Context.User.Id, model);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting model");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("set-temperature", "設定生成溫度")]
    public async Task SetTemperatureAsync(
        [Summary("temperature", "溫度值 (0.0 - 2.0)")]
        [MinValue(0)]
        [MaxValue(2)]
        double temperature)
    {
        try
        {
            await repository.SetSettingAsync("Temperature", temperature.ToString(), Context.User.Username);

            var embed = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithTitle("✅ 設定已更新")
                .WithDescription($"已將生成溫度設定為 **{temperature:F2}**。")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            logger.Information("Admin {AdminId} set temperature to {Temperature}", Context.User.Id, temperature);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting temperature");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("set-max-tokens", "設定最大回應 Token 數")]
    public async Task SetMaxTokensAsync(
        [Summary("max-tokens", "最大 Token 數")]
        [MinValue(1)]
        [MaxValue(32000)]
        int maxTokens)
    {
        try
        {
            await repository.SetSettingAsync("MaxTokens", maxTokens.ToString(), Context.User.Username);

            var embed = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithTitle("✅ 設定已更新")
                .WithDescription($"已將最大回應 Token 數設定為 **{maxTokens:N0}**。")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            logger.Information("Admin {AdminId} set max tokens to {MaxTokens}", Context.User.Id, maxTokens);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting max tokens");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("set-system-prompt", "設定系統提示")]
    public async Task SetSystemPromptAsync(
        [Summary("prompt", "系統提示內容")]
        string prompt)
    {
        try
        {
            await repository.SetSettingAsync("SystemPrompt", prompt, Context.User.Username);

            var embed = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithTitle("✅ 設定已更新")
                .WithDescription($"已更新系統提示。\n\n**新的系統提示：**\n> {prompt}")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            logger.Information("Admin {AdminId} updated system prompt", Context.User.Id);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting system prompt");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("set-global-limit", "設定全域預設每日額度")]
    public async Task SetGlobalLimitAsync(
        [Summary("tokens", "預設每日 Token 額度")]
        [MinValue(0)]
        int tokens)
    {
        try
        {
            await repository.SetSettingAsync("GlobalDailyLimit", tokens.ToString(), Context.User.Username);

            var embed = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithTitle("✅ 設定已更新")
                .WithDescription($"已將全域預設每日額度設定為 **{tokens:N0}** tokens。\n\n" +
                               "**注意：** 此設定只影響新用戶，現有用戶的額度不會改變。")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            logger.Information("Admin {AdminId} set global daily limit to {Tokens}", Context.User.Id, tokens);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting global limit");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("view-settings", "查看當前所有設定")]
    public async Task ViewSettingsAsync()
    {
        try
        {
            var settings = await repository.GetAllSettingsAsync();

            var embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle("⚙️ Bot 設定")
                .WithCurrentTimestamp();

            foreach (var setting in settings.OrderBy(s => s.Key))
            {
                var value = setting.Value.Length > 100 
                    ? setting.Value.Substring(0, 100) + "..." 
                    : setting.Value;
                embed.AddField(setting.Key, $"`{value}`", true);
            }

            await RespondAsync(embed: embed.Build());

            logger.Information("Admin {AdminId} viewed bot settings", Context.User.Id);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error viewing settings");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("stats", "查看全域使用統計")]
    public async Task StatsAsync()
    {
        try
        {
            // This would require additional repository methods for global stats
            // For now, return a placeholder
            var embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle("📊 全域統計")
                .WithDescription("全域統計功能開發中...")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            logger.Information("Admin {AdminId} viewed global stats", Context.User.Id);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error viewing stats");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    #endregion
}

