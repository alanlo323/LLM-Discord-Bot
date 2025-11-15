using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using LLMDiscordBot.Services;
using LLMDiscordBot.Data;
using LLMDiscordBot.Models;
using Serilog;

namespace LLMDiscordBot.Commands;

/// <summary>
/// Admin commands for managing users and bot settings
/// Split into GlobalAdmin and GuildAdmin commands
/// </summary>
[Group("admin", "管理員命令")]
public class AdminCommands(
    TokenControlService tokenControl,
    IRepository repository,
    DiscordSocketClient client,
    ILogger logger) : InteractionModuleBase<SocketInteractionContext>
{
    #region Permission Checks

    /// <summary>
    /// Check if user is a global admin (bot owner)
    /// </summary>
    private async Task<bool> IsGlobalAdminAsync()
    {
        var application = await client.GetApplicationInfoAsync();
        return Context.User.Id == application.Owner.Id;
    }

    /// <summary>
    /// Check if user is a guild admin
    /// </summary>
    private async Task<bool> IsGuildAdminAsync(ulong guildId)
    {
        if (await IsGlobalAdminAsync())
            return true;

        return await repository.IsGuildAdminAsync(guildId, Context.User.Id);
    }

    /// <summary>
    /// Require global admin permission
    /// </summary>
    private async Task<bool> RequireGlobalAdminAsync()
    {
        if (!await IsGlobalAdminAsync())
        {
            await RespondAsync("❌ 此命令需要全域管理員權限（僅限 Bot 擁有者）。", ephemeral: true);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Require guild admin permission
    /// </summary>
    private async Task<bool> RequireGuildAdminAsync()
    {
        if (Context.Guild == null)
        {
            await RespondAsync("❌ 此命令只能在伺服器中使用。", ephemeral: true);
            return false;
        }

        if (!await IsGuildAdminAsync(Context.Guild.Id))
        {
            await RespondAsync("❌ 此命令需要伺服器管理員權限。", ephemeral: true);
            return false;
        }

        return true;
    }

    #endregion

    #region Global Admin Commands

    [Group("global", "全域管理命令（僅限 Bot 擁有者）")]
    public class GlobalAdminCommands(
        TokenControlService tokenControl,
        IRepository repository,
        DiscordSocketClient client,
        ILogger logger) : InteractionModuleBase<SocketInteractionContext>
    {
        private async Task<bool> RequireGlobalAdminAsync()
        {
            var application = await client.GetApplicationInfoAsync();
            if (Context.User.Id != application.Owner.Id)
            {
                await RespondAsync("❌ 此命令需要全域管理員權限（僅限 Bot 擁有者）。", ephemeral: true);
                return false;
            }
            return true;
        }

        [SlashCommand("set-model", "設定 LLM 模型名稱")]
        public async Task SetModelAsync(
            [Summary("model", "模型名稱")]
            string model)
        {
            if (!await RequireGlobalAdminAsync()) return;

            try
            {
                await repository.SetSettingAsync("Model", model, Context.User.Username);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 全域設定已更新")
                    .WithDescription($"已將 LLM 模型設定為 **{model}**。")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: embed);
                logger.Information("GlobalAdmin {AdminId} set model to {Model}", Context.User.Id, model);
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
            if (!await RequireGlobalAdminAsync()) return;

            try
            {
                await repository.SetSettingAsync("Temperature", temperature.ToString(), Context.User.Username);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 全域設定已更新")
                    .WithDescription($"已將生成溫度設定為 **{temperature:F2}**。")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: embed);
                logger.Information("GlobalAdmin {AdminId} set temperature to {Temperature}", Context.User.Id, temperature);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting temperature");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("set-max-tokens", "設定全域最大回應 Token 數")]
        public async Task SetGlobalMaxTokensAsync(
            [Summary("max-tokens", "最大 Token 數")]
            [MinValue(1)]
            [MaxValue(32000)]
            int maxTokens)
        {
            if (!await RequireGlobalAdminAsync()) return;

            try
            {
                await DeferAsync();

                var oldMaxTokensStr = await repository.GetSettingAsync("GlobalMaxTokens");
                await repository.SetSettingAsync("GlobalMaxTokens", maxTokens.ToString(), Context.User.Username);

                // Adjust guild settings if necessary
                var adjustedGuilds = await repository.AdjustGuildSettingsToGlobalLimitsAsync(int.MaxValue, maxTokens);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Orange)
                    .WithTitle("✅ 全域設定已更新")
                    .WithDescription($"已將全域最大回應 Token 數設定為 **{maxTokens:N0}**。\n\n" +
                                   (adjustedGuilds.Any() 
                                       ? $"**注意：** {adjustedGuilds.Count} 個伺服器的設定已自動調整。" 
                                       : "沒有伺服器需要調整設定。"))
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: embed);

                // Send notifications to affected guilds
                if (adjustedGuilds.Any())
                {
                    _ = Task.Run(async () => await NotifyGuildsAboutAdjustmentsAsync(adjustedGuilds));
                }

                logger.Information("GlobalAdmin {AdminId} set global max tokens to {MaxTokens}", Context.User.Id, maxTokens);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting global max tokens");
                await FollowupAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("set-system-prompt", "設定全域系統提示")]
        public async Task SetGlobalSystemPromptAsync(
            [Summary("prompt", "系統提示內容")]
            string prompt)
        {
            if (!await RequireGlobalAdminAsync()) return;

            try
            {
                await repository.SetSettingAsync("GlobalSystemPrompt", prompt, Context.User.Username);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 全域設定已更新")
                    .WithDescription($"已更新全域系統提示。\n\n**新的系統提示：**\n> {prompt}")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: embed);
                logger.Information("GlobalAdmin {AdminId} updated global system prompt", Context.User.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting global system prompt");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("set-daily-limit", "設定全域預設每日額度")]
        public async Task SetGlobalDailyLimitAsync(
            [Summary("tokens", "預設每日 Token 額度")]
            [MinValue(0)]
            int tokens)
        {
            if (!await RequireGlobalAdminAsync()) return;

            try
            {
                await DeferAsync();

                await repository.SetSettingAsync("GlobalDailyLimit", tokens.ToString(), Context.User.Username);

                // Adjust guild settings if necessary
                var adjustedGuilds = await repository.AdjustGuildSettingsToGlobalLimitsAsync(tokens, int.MaxValue);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Orange)
                    .WithTitle("✅ 全域設定已更新")
                    .WithDescription($"已將全域預設每日額度設定為 **{tokens:N0}** tokens。\n\n" +
                                   "**注意：** 此設定只影響新用戶，現有用戶的額度不會改變。\n" +
                                   (adjustedGuilds.Any() 
                                       ? $"{adjustedGuilds.Count} 個伺服器的設定已自動調整。" 
                                       : "沒有伺服器需要調整設定。"))
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: embed);

                // Send notifications to affected guilds
                if (adjustedGuilds.Any())
                {
                    _ = Task.Run(async () => await NotifyGuildsAboutAdjustmentsAsync(adjustedGuilds));
                }

                logger.Information("GlobalAdmin {AdminId} set global daily limit to {Tokens}", Context.User.Id, tokens);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting global daily limit");
                await FollowupAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("view-settings", "查看當前全域設定")]
        public async Task ViewGlobalSettingsAsync()
        {
            if (!await RequireGlobalAdminAsync()) return;

            try
            {
                var settings = await repository.GetAllSettingsAsync();

                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithTitle("⚙️ 全域 Bot 設定")
                    .WithCurrentTimestamp();

                foreach (var setting in settings.OrderBy(s => s.Key))
                {
                    var value = setting.Value.Length > 100 
                        ? setting.Value.Substring(0, 100) + "..." 
                        : setting.Value;
                    embed.AddField(setting.Key, $"`{value}`", true);
                }

                await RespondAsync(embed: embed.Build());
                logger.Information("GlobalAdmin {AdminId} viewed global settings", Context.User.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error viewing global settings");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("add-guild-admin", "新增伺服器管理員")]
        public async Task AddGuildAdminAsync(
            [Summary("guild-id", "伺服器 ID")]
            string guildIdStr,
            [Summary("user", "要新增為管理員的用戶")]
            IUser user)
        {
            if (!await RequireGlobalAdminAsync()) return;

            if (!ulong.TryParse(guildIdStr, out var guildId))
            {
                await RespondAsync("❌ 無效的伺服器 ID。", ephemeral: true);
                return;
            }

            try
            {
                await repository.AddGuildAdminAsync(guildId, user.Id, Context.User.Username);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 管理員已新增")
                    .WithDescription($"已將 {user.Mention} 新增為伺服器 {guildId} 的管理員。")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: embed);
                logger.Information("GlobalAdmin {AdminId} added {UserId} as admin of guild {GuildId}", 
                    Context.User.Id, user.Id, guildId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error adding guild admin");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("remove-guild-admin", "移除伺服器管理員")]
        public async Task RemoveGuildAdminAsync(
            [Summary("guild-id", "伺服器 ID")]
            string guildIdStr,
            [Summary("user", "要移除的管理員")]
            IUser user)
        {
            if (!await RequireGlobalAdminAsync()) return;

            if (!ulong.TryParse(guildIdStr, out var guildId))
            {
                await RespondAsync("❌ 無效的伺服器 ID。", ephemeral: true);
                return;
            }

            try
            {
                await repository.RemoveGuildAdminAsync(guildId, user.Id);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 管理員已移除")
                    .WithDescription($"已將 {user.Mention} 從伺服器 {guildId} 的管理員中移除。")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: embed);
                logger.Information("GlobalAdmin {AdminId} removed {UserId} as admin of guild {GuildId}", 
                    Context.User.Id, user.Id, guildId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error removing guild admin");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("stats", "查看全域使用統計")]
        public async Task GlobalStatsAsync()
        {
            if (!await RequireGlobalAdminAsync()) return;

            try
            {
                await DeferAsync();

                var today = DateTime.UtcNow;
                
                var totalUsers = await repository.GetTotalUsersCountAsync();
                var blockedUsers = await repository.GetBlockedUsersCountAsync();
                var activeUsersToday = await repository.GetActiveUsersTodayCountAsync(today);
                
                var totalTokenUsage = await repository.GetTotalTokenUsageAsync();
                var totalMessageCount = await repository.GetTotalMessageCountAsync();
                
                var todayTokenUsage = await repository.GetTodayTokenUsageAsync(today);
                var todayMessageCount = await repository.GetTodayMessageCountAsync(today);
                
                var topUsers = await repository.GetTopUsersByTokenUsageAsync(today, 5);
                
                var last7DaysStart = today.AddDays(-6).Date;
                var last30DaysStart = today.AddDays(-29).Date;
                
                var last7DaysTrend = await repository.GetDailyTokenUsageTrendAsync(last7DaysStart, today);
                var last30DaysTrend = await repository.GetDailyTokenUsageTrendAsync(last30DaysStart, today);
                
                var avgTokensPerUser = totalUsers > 0 ? (double)totalTokenUsage / totalUsers : 0;
                
                var last7DaysTotal = last7DaysTrend.Sum(t => (long)t.TokensUsed);
                var last30DaysTotal = last30DaysTrend.Sum(t => (long)t.TokensUsed);
                var last7DaysAverage = last7DaysTrend.Count > 0 ? (double)last7DaysTotal / last7DaysTrend.Count : 0;
                var last30DaysAverage = last30DaysTrend.Count > 0 ? (double)last30DaysTotal / last30DaysTrend.Count : 0;

                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithTitle("📊 全域使用統計")
                    .WithDescription("Bot 的完整使用統計資訊")
                    .WithCurrentTimestamp();

                embed.AddField("👥 用戶統計", 
                    $"總用戶數：**{totalUsers:N0}**\n" +
                    $"今日活躍：**{activeUsersToday:N0}**\n" +
                    $"封鎖用戶：**{blockedUsers:N0}**",
                    inline: true);

                embed.AddField("📅 今日活動",
                    $"Token 使用：**{todayTokenUsage:N0}**\n" +
                    $"訊息數量：**{todayMessageCount:N0}**\n" +
                    $"平均每訊息：**{(todayMessageCount > 0 ? (double)todayTokenUsage / todayMessageCount : 0):N0}** tokens",
                    inline: true);

                embed.AddField("📈 歷史總計",
                    $"總 Token 數：**{totalTokenUsage:N0}**\n" +
                    $"總訊息數：**{totalMessageCount:N0}**\n" +
                    $"平均每用戶：**{avgTokensPerUser:N0}** tokens",
                    inline: true);

                embed.AddField("📊 近 7 天趨勢",
                    $"總使用量：**{last7DaysTotal:N0}** tokens\n" +
                    $"日均使用：**{last7DaysAverage:N0}** tokens\n" +
                    $"總訊息數：**{last7DaysTrend.Sum(t => t.MessageCount):N0}**",
                    inline: true);

                embed.AddField("📊 近 30 天趨勢",
                    $"總使用量：**{last30DaysTotal:N0}** tokens\n" +
                    $"日均使用：**{last30DaysAverage:N0}** tokens\n" +
                    $"總訊息數：**{last30DaysTrend.Sum(t => t.MessageCount):N0}**",
                    inline: true);

                if (topUsers.Any())
                {
                    var topUsersText = string.Join("\n", topUsers.Select(u =>
                        $"{u.Rank}. <@{u.UserId}>: **{u.TokensUsed:N0}** tokens ({u.MessageCount} 則訊息)"));
                    embed.AddField("🏆 今日使用排行 (Top 5)", topUsersText, inline: false);
                }
                else
                {
                    embed.AddField("🏆 今日使用排行 (Top 5)", "今日尚無使用記錄", inline: false);
                }

                var trendChart7Days = CreateSimpleTrendChart(last7DaysTrend.TakeLast(7).ToList());
                embed.AddField("📉 近 7 天使用趨勢", trendChart7Days, inline: false);

                await FollowupAsync(embed: embed.Build());
                logger.Information("GlobalAdmin {AdminId} viewed global stats", Context.User.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error viewing global stats");
                await FollowupAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        private string CreateSimpleTrendChart(List<DailyTrend> trends)
        {
            if (!trends.Any())
                return "無資料";

            var maxTokens = trends.Max(t => t.TokensUsed);
            var lines = new List<string>();

            foreach (var trend in trends)
            {
                var barLength = maxTokens > 0 ? (int)((double)trend.TokensUsed / maxTokens * 20) : 0;
                var bar = new string('█', Math.Max(1, barLength));
                var dateStr = trend.Date.ToString("MM/dd");
                lines.Add($"`{dateStr}` {bar} {trend.TokensUsed:N0} tokens");
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Send notifications to guild admins and system channels about setting adjustments
        /// </summary>
        private async Task NotifyGuildsAboutAdjustmentsAsync(List<(GuildSettings guild, List<string> adjustments)> adjustedGuilds)
        {
            try
            {
                foreach (var (guildSettings, adjustments) in adjustedGuilds)
                {
                    var guild = client.GetGuild(guildSettings.GuildId);
                    if (guild == null) continue;

                    var adjustmentText = string.Join("\n", adjustments.Select(a => $"• {a}"));
                    var message = $"⚠️ **伺服器設定自動調整通知**\n\n" +
                                $"由於全域限制已降低，本伺服器的以下設定已自動調整以符合新的全域限制：\n\n" +
                                $"{adjustmentText}\n\n" +
                                $"調整時間：{guildSettings.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC";

                    var embed = new EmbedBuilder()
                        .WithColor(Color.Orange)
                        .WithTitle("⚠️ 設定自動調整通知")
                        .WithDescription(message)
                        .WithCurrentTimestamp()
                        .Build();

                    // Try to send to system channel
                    var systemChannel = guild.SystemChannel ?? guild.TextChannels.FirstOrDefault();
                    if (systemChannel != null)
                    {
                        try
                        {
                            await systemChannel.SendMessageAsync(embed: embed);
                            logger.Information("Sent guild adjustment notification to system channel for guild {GuildId}", guildSettings.GuildId);
                        }
                        catch (Exception ex)
                        {
                            logger.Warning(ex, "Failed to send notification to system channel for guild {GuildId}", guildSettings.GuildId);
                        }
                    }

                    // Send DM to all guild admins
                    var guildAdmins = await repository.GetGuildAdminsAsync(guildSettings.GuildId);
                    foreach (var admin in guildAdmins)
                    {
                        try
                        {
                            var user = await client.GetUserAsync(admin.UserId);
                            if (user != null)
                            {
                                await user.SendMessageAsync(
                                    text: $"來自伺服器 **{guild.Name}** 的通知：",
                                    embed: embed);
                                logger.Information("Sent guild adjustment DM to admin {UserId} for guild {GuildId}", 
                                    admin.UserId, guildSettings.GuildId);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Warning(ex, "Failed to send DM to guild admin {UserId} for guild {GuildId}", 
                                admin.UserId, guildSettings.GuildId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error sending guild adjustment notifications");
            }
        }
    }

    #endregion

    #region Guild Admin Commands

    [Group("guild", "伺服器管理命令")]
    public class GuildAdminCommands(
        TokenControlService tokenControl,
        IRepository repository,
        DiscordSocketClient client,
        ILogger logger) : InteractionModuleBase<SocketInteractionContext>
    {
        private async Task<bool> IsGlobalAdminAsync()
        {
            var application = await client.GetApplicationInfoAsync();
            return Context.User.Id == application.Owner.Id;
        }

        private async Task<bool> RequireGuildAdminAsync()
        {
            if (Context.Guild == null)
            {
                await RespondAsync("❌ 此命令只能在伺服器中使用。", ephemeral: true);
                return false;
            }

            var isGlobalAdmin = await IsGlobalAdminAsync();
            var isGuildAdmin = await repository.IsGuildAdminAsync(Context.Guild.Id, Context.User.Id);

            if (!isGlobalAdmin && !isGuildAdmin)
            {
                await RespondAsync("❌ 此命令需要伺服器管理員權限。", ephemeral: true);
                return false;
            }

            return true;
        }

        [SlashCommand("set-system-prompt", "設定伺服器專屬系統提示")]
        public async Task SetGuildSystemPromptAsync(
            [Summary("prompt", "系統提示內容（會附加在全域提示之後）")]
            string prompt)
        {
            if (!await RequireGuildAdminAsync()) return;

            try
            {
                var guildSettings = await repository.GetOrCreateGuildSettingsAsync(Context.Guild!.Id);
                guildSettings.SystemPrompt = prompt;
                guildSettings.UpdatedBy = Context.User.Username;
                await repository.UpdateGuildSettingsAsync(guildSettings);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 伺服器設定已更新")
                    .WithDescription($"已更新伺服器專屬系統提示。\n\n**新的系統提示：**\n> {prompt}\n\n" +
                                   "**注意：** 此提示會附加在全域系統提示之後。")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: embed);
                logger.Information("GuildAdmin {AdminId} updated system prompt for guild {GuildId}", 
                    Context.User.Id, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting guild system prompt");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("set-daily-limit", "設定伺服器預設每日額度")]
        public async Task SetGuildDailyLimitAsync(
            [Summary("tokens", "每日 Token 額度")]
            [MinValue(0)]
            int tokens)
        {
            if (!await RequireGuildAdminAsync()) return;

            try
            {
                var globalLimitStr = await repository.GetSettingAsync("GlobalDailyLimit");
                var globalLimit = int.TryParse(globalLimitStr, out var gl) ? gl : int.MaxValue;

                if (tokens > globalLimit)
                {
                    await RespondAsync($"❌ 設定失敗：伺服器額度不能超過全域限制（{globalLimit:N0} tokens）。", ephemeral: true);
                    return;
                }

                var guildSettings = await repository.GetOrCreateGuildSettingsAsync(Context.Guild!.Id);
                guildSettings.DailyLimit = tokens;
                guildSettings.UpdatedBy = Context.User.Username;
                await repository.UpdateGuildSettingsAsync(guildSettings);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 伺服器設定已更新")
                    .WithDescription($"已將伺服器預設每日額度設定為 **{tokens:N0}** tokens。")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: embed);
                logger.Information("GuildAdmin {AdminId} set daily limit to {Tokens} for guild {GuildId}", 
                    Context.User.Id, tokens, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting guild daily limit");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("set-max-tokens", "設定伺服器最大回應 Token 數")]
        public async Task SetGuildMaxTokensAsync(
            [Summary("max-tokens", "最大 Token 數")]
            [MinValue(1)]
            [MaxValue(32000)]
            int maxTokens)
        {
            if (!await RequireGuildAdminAsync()) return;

            try
            {
                var globalMaxTokensStr = await repository.GetSettingAsync("GlobalMaxTokens");
                var globalMaxTokens = int.TryParse(globalMaxTokensStr, out var gmt) ? gmt : int.MaxValue;

                if (maxTokens > globalMaxTokens)
                {
                    await RespondAsync($"❌ 設定失敗：伺服器 MaxTokens 不能超過全域限制（{globalMaxTokens:N0} tokens）。", ephemeral: true);
                    return;
                }

                var guildSettings = await repository.GetOrCreateGuildSettingsAsync(Context.Guild!.Id);
                guildSettings.MaxTokens = maxTokens;
                guildSettings.UpdatedBy = Context.User.Username;
                await repository.UpdateGuildSettingsAsync(guildSettings);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 伺服器設定已更新")
                    .WithDescription($"已將伺服器最大回應 Token 數設定為 **{maxTokens:N0}**。")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: embed);
                logger.Information("GuildAdmin {AdminId} set max tokens to {MaxTokens} for guild {GuildId}", 
                    Context.User.Id, maxTokens, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting guild max tokens");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("set-enable-limits", "設定伺服器是否啟用限制")]
        public async Task SetGuildEnableLimitsAsync(
            [Summary("enabled", "是否啟用限制")]
            bool enabled)
        {
            if (!await RequireGuildAdminAsync()) return;

            try
            {
                var guildSettings = await repository.GetOrCreateGuildSettingsAsync(Context.Guild!.Id);
                guildSettings.EnableLimits = enabled;
                guildSettings.UpdatedBy = Context.User.Username;
                await repository.UpdateGuildSettingsAsync(guildSettings);

                var embed = new EmbedBuilder()
                    .WithColor(enabled ? Color.Orange : Color.Green)
                    .WithTitle("✅ 伺服器設定已更新")
                    .WithDescription($"已將伺服器限制設定為 **{(enabled ? "啟用" : "停用")}**。\n\n" +
                                   "**注意：** 如果全域限制啟用時，伺服器限制將被強制啟用。")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: embed);
                logger.Information("GuildAdmin {AdminId} set enable limits to {Enabled} for guild {GuildId}", 
                    Context.User.Id, enabled, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting guild enable limits");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("view-settings", "查看伺服器當前設定")]
        public async Task ViewGuildSettingsAsync()
        {
            if (!await RequireGuildAdminAsync()) return;

            try
            {
                var guildSettings = await repository.GetGuildSettingsAsync(Context.Guild!.Id);
                var globalSettings = await repository.GetAllSettingsAsync();

                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithTitle($"⚙️ {Context.Guild.Name} 伺服器設定")
                    .WithCurrentTimestamp();

                if (guildSettings != null)
                {
                    embed.AddField("系統提示", 
                        guildSettings.SystemPrompt != null && guildSettings.SystemPrompt.Length > 0
                            ? (guildSettings.SystemPrompt.Length > 100 
                                ? guildSettings.SystemPrompt.Substring(0, 100) + "..." 
                                : guildSettings.SystemPrompt)
                            : "（使用全域設定）", 
                        false);

                    embed.AddField("每日額度", 
                        guildSettings.DailyLimit.HasValue 
                            ? $"{guildSettings.DailyLimit.Value:N0} tokens" 
                            : "（使用用戶設定）", 
                        true);

                    embed.AddField("最大 Token 數", 
                        guildSettings.MaxTokens.HasValue 
                            ? $"{guildSettings.MaxTokens.Value:N0} tokens" 
                            : "（使用全域設定）", 
                        true);

                    embed.AddField("啟用限制", 
                        guildSettings.EnableLimits ? "✅ 是" : "❌ 否", 
                        true);

                    if (guildSettings.UpdatedBy != null)
                    {
                        embed.WithFooter($"最後更新：{guildSettings.UpdatedBy} 於 {guildSettings.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                    }
                }
                else
                {
                    embed.WithDescription("此伺服器尚未設定自訂設定，使用全域預設值。");
                }

                embed.AddField("\n📋 全域設定參考", 
                    $"全域系統提示：`{(globalSettings.ContainsKey("GlobalSystemPrompt") ? (globalSettings["GlobalSystemPrompt"].Length > 50 ? globalSettings["GlobalSystemPrompt"].Substring(0, 50) + "..." : globalSettings["GlobalSystemPrompt"]) : "無")}`\n" +
                    $"全域每日額度：`{(globalSettings.ContainsKey("GlobalDailyLimit") ? globalSettings["GlobalDailyLimit"] : "無")} tokens`\n" +
                    $"全域最大 Token：`{(globalSettings.ContainsKey("GlobalMaxTokens") ? globalSettings["GlobalMaxTokens"] : "無")} tokens`",
                    false);

                await RespondAsync(embed: embed.Build());
                logger.Information("GuildAdmin {AdminId} viewed settings for guild {GuildId}", 
                    Context.User.Id, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error viewing guild settings");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("list-admins", "查看伺服器管理員列表")]
        public async Task ListGuildAdminsAsync()
        {
            if (!await RequireGuildAdminAsync()) return;

            try
            {
                var admins = await repository.GetGuildAdminsAsync(Context.Guild!.Id);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithTitle($"👥 {Context.Guild.Name} 管理員列表")
                    .WithCurrentTimestamp();

                if (admins.Any())
                {
                    var adminList = string.Join("\n", admins.Select(a => 
                        $"<@{a.UserId}> - 新增於 {a.CreatedAt:yyyy-MM-dd} by {a.CreatedBy ?? "系統"}"));
                    embed.WithDescription(adminList);
                }
                else
                {
                    embed.WithDescription("此伺服器尚未設定管理員。");
                }

                await RespondAsync(embed: embed.Build());
                logger.Information("GuildAdmin {AdminId} viewed admin list for guild {GuildId}", 
                    Context.User.Id, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error listing guild admins");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("add-admin", "新增伺服器管理員（需為現有管理員或全域管理員）")]
        public async Task AddAdminAsync(
            [Summary("user", "要新增為管理員的用戶")]
            IUser user)
        {
            if (!await RequireGuildAdminAsync()) return;

            try
            {
                await repository.AddGuildAdminAsync(Context.Guild!.Id, user.Id, Context.User.Username);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 管理員已新增")
                    .WithDescription($"已將 {user.Mention} 新增為本伺服器的管理員。")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: embed);
                logger.Information("GuildAdmin {AdminId} added {UserId} as admin of guild {GuildId}", 
                    Context.User.Id, user.Id, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error adding guild admin");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }

        [SlashCommand("remove-admin", "移除伺服器管理員")]
        public async Task RemoveAdminAsync(
            [Summary("user", "要移除的管理員")]
            IUser user)
        {
            if (!await RequireGuildAdminAsync()) return;

            try
            {
                await repository.RemoveGuildAdminAsync(Context.Guild!.Id, user.Id);

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ 管理員已移除")
                    .WithDescription($"已將 {user.Mention} 從本伺服器的管理員中移除。")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: embed);
                logger.Information("GuildAdmin {AdminId} removed {UserId} as admin of guild {GuildId}", 
                    Context.User.Id, user.Id, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error removing guild admin");
                await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
            }
        }
    }

    #endregion

    #region User Management Commands (Guild Admin)

    [SlashCommand("user-stats", "查看指定用戶的使用統計")]
    public async Task UserStatsAsync(
        [Summary("user", "要查看的用戶")]
        IUser user)
    {
        if (!await RequireGuildAdminAsync()) return;

        try
        {
            var guildId = Context.Guild?.Id;
            var stats = await tokenControl.GetUserStatsAsync(user.Id, guildId);

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
            logger.Information("GuildAdmin {AdminId} viewed stats for user {UserId} in guild {GuildId}", 
                Context.User.Id, user.Id, guildId);
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
        if (!await RequireGuildAdminAsync()) return;

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
            logger.Information("GuildAdmin {AdminId} set limit for user {UserId} to {Tokens} in guild {GuildId}",
                Context.User.Id, user.Id, tokens, Context.Guild?.Id);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error setting user limit");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("block", "封鎖用戶")]
    public async Task BlockAsync(
        [Summary("user", "要封鎖的用戶")]
        IUser user)
    {
        if (!await RequireGuildAdminAsync()) return;

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
            logger.Warning("GuildAdmin {AdminId} blocked user {UserId} in guild {GuildId}", 
                Context.User.Id, user.Id, Context.Guild?.Id);
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
        if (!await RequireGuildAdminAsync()) return;

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
            logger.Information("GuildAdmin {AdminId} unblocked user {UserId} in guild {GuildId}", 
                Context.User.Id, user.Id, Context.Guild?.Id);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error unblocking user");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    #endregion
}
