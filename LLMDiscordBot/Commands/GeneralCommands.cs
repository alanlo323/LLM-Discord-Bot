using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using LLMDiscordBot.Data;
using Serilog;

namespace LLMDiscordBot.Commands;

/// <summary>
/// General commands for help and bot information
/// </summary>
public class GeneralCommands(
    DiscordSocketClient client,
    IRepository repository,
    ILogger logger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("help", "顯示指令說明和使用教學")]
    public async Task HelpAsync(
        [Summary("category", "選擇指令分類")]
        [Autocomplete(typeof(HelpCategoryAutocompleteHandler))]
        string category = "all")
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var userId = Context.User.Id;
            var guildId = Context.Guild?.Id;

            // Check user permissions
            var isGlobalAdmin = await IsGlobalAdminAsync();
            var isGuildAdmin = guildId.HasValue && await repository.IsGuildAdminAsync(guildId.Value, userId);

            // Normalize category for case-insensitive comparison
            var normalizedCategory = category.ToLower();

            // Validate category access
            if (normalizedCategory == "global-admin" && !isGlobalAdmin)
            {
                await FollowupAsync(
                    embed: new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("❌ 權限不足")
                        .WithDescription("您沒有權限查看全域管理指令。")
                        .Build(),
                    ephemeral: true);
                return;
            }

            if (normalizedCategory == "guild-admin" && !isGlobalAdmin && !isGuildAdmin)
            {
                await FollowupAsync(
                    embed: new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("❌ 權限不足")
                        .WithDescription("您沒有權限查看伺服器管理指令。")
                        .Build(),
                    ephemeral: true);
                return;
            }

            var embed = normalizedCategory switch
            {
                "chat" => BuildChatHelpEmbed(),
                "memory" => BuildMemoryHelpEmbed(),
                "preferences" => BuildPreferencesHelpEmbed(),
                "user" => BuildUserHelpEmbed(),
                "guild-admin" => BuildGuildAdminHelpEmbed(),
                "global-admin" => BuildGlobalAdminHelpEmbed(),
                _ => BuildAllHelpEmbed(isGlobalAdmin, isGuildAdmin)
            };

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
            logger.Information("User {UserId} viewed help for category {Category}", userId, category);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error showing help");
            await FollowupAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    [SlashCommand("about", "關於這個 Bot")]
    public async Task AboutAsync()
    {
        try
        {
            var isGlobalAdmin = await IsGlobalAdminAsync();

            // Build main embed for all users
            var embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle("🤖 LLM Discord Bot")
                .WithDescription("一個功能強大的 AI 助手機器人，為您的 Discord 伺服器提供智能對話和記憶功能。")
                .AddField("✨ 核心功能",
                    "🤖 **LLM 對話** - 支援多種推理深度的智能對話\n" +
                    "🧠 **GraphRAG 記憶系統** - 自動記憶重要對話內容並建立知識圖譜\n" +
                    "⚙️ **個人化設定** - 自訂語言、風格、溫度等偏好\n" +
                    "📊 **使用統計** - 追蹤和分析您的使用習慣\n" +
                    "🔒 **Token 額度管理** - 完整的使用額度控制系統\n" +
                    "👥 **多伺服器支援** - 在不同伺服器保持獨立的記憶和設定",
                    false)
                .AddField("📚 指令說明", "使用 `/help` 查看完整的指令列表和詳細說明", false)
                .WithFooter("由 Discord.Net、Semantic Kernel 和 GraphRag.Net 提供支援")
                .WithCurrentTimestamp();

            await RespondAsync(embed: embed.Build());

            // Send additional info to bot owner
            if (isGlobalAdmin)
            {
                await SendOwnerInfoAsync();
            }

            logger.Information("User {UserId} viewed about info (IsOwner: {IsOwner})", Context.User.Id, isGlobalAdmin);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error showing about info");
            await RespondAsync("發生錯誤，請稍後再試。", ephemeral: true);
        }
    }

    #region Helper Methods

    private async Task<bool> IsGlobalAdminAsync()
    {
        try
        {
            var application = await client.GetApplicationInfoAsync();
            return Context.User.Id == application.Owner.Id;
        }
        catch
        {
            return false;
        }
    }

    private async Task SendOwnerInfoAsync()
    {
        try
        {
            // Technical stack information
            var techEmbed = new EmbedBuilder()
                .WithColor(Color.Purple)
                .WithTitle("🔧 技術資訊（僅限 Bot Owner）")
                .AddField("💻 技術棧",
                    $".NET 版本: `{Environment.Version}`\n" +
                    $"Discord.Net: `3.18.0`\n" +
                    $"Semantic Kernel: `1.67.1`\n" +
                    $"GraphRag.Net: `0.2.0`",
                    false);

            // System information
            var hostname = System.Net.Dns.GetHostName();
            var osVersion = Environment.OSVersion;
            var uptimeMs = Environment.TickCount64;
            var uptime = TimeSpan.FromMilliseconds(uptimeMs);
            var uptimeStr = $"{uptime.Days} 天 {uptime.Hours} 小時 {uptime.Minutes} 分鐘";

            techEmbed.AddField("🖥️ 系統資訊",
                $"主機名稱: `{hostname}`\n" +
                $"作業系統: `{osVersion.Platform} {osVersion.Version}`\n" +
                $"運行時間: `{uptimeStr}`\n" +
                $"處理器數量: `{Environment.ProcessorCount}`\n" +
                $"系統架構: `{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}`",
                false);

            // Get network information
            var localIPs = new List<string>();
            try
            {
                var hostEntry = await System.Net.Dns.GetHostEntryAsync(hostname);
                foreach (var ip in hostEntry.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        localIPs.Add(ip.ToString());
                    }
                }
            }
            catch { }

            var localIPsStr = localIPs.Any() ? string.Join(", ", localIPs.Select(ip => $"`{ip}`")) : "`無法取得`";

            // Get public IP
            var publicIP = "`正在取得...`";
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                var ip = await httpClient.GetStringAsync("https://api.ipify.org");
                publicIP = $"`{ip}`";
            }
            catch
            {
                publicIP = "`無法取得`";
            }

            techEmbed.AddField("🌐 網路資訊",
                $"本地 IP: {localIPsStr}\n" +
                $"公網 IP: {publicIP}",
                false);

            // Bot statistics
            var guildCount = client.Guilds.Count;
            var totalUsers = await repository.GetTotalUsersCountAsync();

            techEmbed.AddField("📊 Bot 統計",
                $"伺服器數量: **{guildCount}**\n" +
                $"總用戶數: **{totalUsers:N0}**",
                false);

            techEmbed.WithCurrentTimestamp();

            await FollowupAsync(embed: techEmbed.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error sending owner info");
        }
    }

    #endregion

    #region Help Embed Builders

    private EmbedBuilder BuildAllHelpEmbed(bool isGlobalAdmin, bool isGuildAdmin)
    {
        var embed = new EmbedBuilder()
            .WithColor(Color.Blue)
            .WithTitle("📚 指令說明")
            .WithDescription("以下是所有可用的指令分類。使用 `/help category:分類名稱` 查看該分類的詳細說明。")
            .WithCurrentTimestamp();

        embed.AddField("💬 聊天相關", "`/help category:聊天`\n與 AI 對話、清除記錄等基本功能", true);
        embed.AddField("🧠 記憶系統", "`/help category:記憶`\n管理 AI 記憶圖譜功能", true);
        embed.AddField("⚙️ 個人設定", "`/help category:個人設定`\n自訂您的個人偏好設定", true);
        embed.AddField("👤 用戶資訊", "`/help category:用戶資訊`\n查看您的統計和歷史記錄", true);

        if (isGuildAdmin || isGlobalAdmin)
        {
            embed.AddField("🛡️ 伺服器管理", "`/help category:伺服器管理`\n管理伺服器設定和用戶", true);
        }

        if (isGlobalAdmin)
        {
            embed.AddField("🔧 全域管理", "`/help category:全域管理`\n全域 Bot 設定（僅限 Owner）", true);
        }

        embed.WithFooter("提示：選擇分類參數時會根據您的權限顯示可用選項");

        return embed;
    }

    private EmbedBuilder BuildChatHelpEmbed()
    {
        var embed = new EmbedBuilder()
            .WithColor(Color.Blue)
            .WithTitle("💬 聊天相關指令")
            .WithDescription("與 AI 對話和管理聊天記錄的指令")
            .WithCurrentTimestamp();

        embed.AddField("/chat",
            "**說明：** 與 LLM 進行對話\n" +
            "**參數：**\n" +
            "  • `message` (必填) - 您想說的話\n" +
            "  • `reasoning-effort` (可選) - 推理深度，預設為 medium\n" +
            "    可選：low（快速）、medium（平衡）、high（深度思考）\n" +
            "**範例：**\n" +
            "  `/chat message:你好，請幫我解釋量子力學`\n" +
            "  `/chat message:寫一個排序演算法 reasoning-effort:high`",
            false);

        embed.AddField("/clearchat",
            "**說明：** 清除您在此頻道的聊天記錄\n" +
            "**參數：** 無\n" +
            "**範例：**\n" +
            "  `/clearchat`",
            false);

        return embed;
    }

    private EmbedBuilder BuildMemoryHelpEmbed()
    {
        var embed = new EmbedBuilder()
            .WithColor(Color.Purple)
            .WithTitle("🧠 記憶系統指令")
            .WithDescription("管理 AI 的記憶圖譜功能")
            .WithCurrentTimestamp();

        embed.AddField("/memory save",
            "**說明：** 手動標記重要內容以記憶\n" +
            "**參數：**\n" +
            "  • `content` (必填) - 要記憶的內容\n" +
            "**範例：**\n" +
            "  `/memory save content:我喜歡 Python 程式語言`",
            false);

        embed.AddField("/memory recall",
            "**說明：** 查詢記憶圖譜\n" +
            "**參數：**\n" +
            "  • `query` (必填) - 查詢關鍵字\n" +
            "**範例：**\n" +
            "  `/memory recall query:Python`",
            false);

        embed.AddField("/memory list",
            "**說明：** 列出您的記憶索引\n" +
            "**參數：** 無\n" +
            "**範例：**\n" +
            "  `/memory list`",
            false);

        embed.AddField("/memory stats",
            "**說明：** 查看記憶統計資訊\n" +
            "**參數：** 無\n" +
            "**範例：**\n" +
            "  `/memory stats`",
            false);

        embed.AddField("/memory clear",
            "**說明：** 清除記憶圖譜\n" +
            "**參數：**\n" +
            "  • `scope` (可選) - 清除範圍，預設為當前伺服器\n" +
            "    可選：當前伺服器、所有記憶\n" +
            "**範例：**\n" +
            "  `/memory clear scope:當前伺服器`",
            false);

        return embed;
    }

    private EmbedBuilder BuildPreferencesHelpEmbed()
    {
        var embed = new EmbedBuilder()
            .WithColor(Color.Green)
            .WithTitle("⚙️ 個人設定指令")
            .WithDescription("自訂您的個人偏好設定")
            .WithCurrentTimestamp();

        embed.AddField("/preferences view",
            "**說明：** 查看您的個人偏好設定和習慣統計\n" +
            "**參數：** 無\n" +
            "**範例：**\n" +
            "  `/preferences view`",
            false);

        embed.AddField("/preferences set-language",
            "**說明：** 設定您偏好的語言\n" +
            "**參數：**\n" +
            "  • `language` (必填) - 語言代碼（如 zh-TW, en-US, ja-JP）\n" +
            "**範例：**\n" +
            "  `/preferences set-language language:zh-TW`",
            false);

        embed.AddField("/preferences set-temperature",
            "**說明：** 設定您偏好的生成溫度\n" +
            "**參數：**\n" +
            "  • `temperature` (必填) - 溫度值 (0.0 - 2.0)\n" +
            "**範例：**\n" +
            "  `/preferences set-temperature temperature:0.8`",
            false);

        embed.AddField("/preferences set-max-tokens",
            "**說明：** 設定您偏好的最大回應 Token 數\n" +
            "**參數：**\n" +
            "  • `max-tokens` (必填) - 最大 Token 數 (100-32000)\n" +
            "**範例：**\n" +
            "  `/preferences set-max-tokens max-tokens:2000`",
            false);

        embed.AddField("/preferences set-style",
            "**說明：** 設定您偏好的回答風格\n" +
            "**參數：**\n" +
            "  • `style` (必填) - 回答風格\n" +
            "    可選：簡潔、詳細、輕鬆、正式、技術性、創意性\n" +
            "**範例：**\n" +
            "  `/preferences set-style style:詳細`",
            false);

        embed.AddField("/preferences set-custom-prompt",
            "**說明：** 設定您的自訂系統提示\n" +
            "**參數：**\n" +
            "  • `prompt` (必填) - 自訂系統提示內容（最多 1000 字元）\n" +
            "**範例：**\n" +
            "  `/preferences set-custom-prompt prompt:請用輕鬆幽默的方式回答`",
            false);

        embed.AddField("/preferences toggle-code-examples",
            "**說明：** 切換是否偏好在回答中包含程式碼範例\n" +
            "**參數：** 無\n" +
            "**範例：**\n" +
            "  `/preferences toggle-code-examples`",
            false);

        embed.AddField("/preferences toggle-step-by-step",
            "**說明：** 切換是否偏好逐步教學式的回答\n" +
            "**參數：** 無\n" +
            "**範例：**\n" +
            "  `/preferences toggle-step-by-step`",
            false);

        embed.AddField("/preferences stats",
            "**說明：** 查看您的使用統計和習慣分析\n" +
            "**參數：** 無\n" +
            "**範例：**\n" +
            "  `/preferences stats`",
            false);

        embed.AddField("/preferences reset",
            "**說明：** 重置所有個人偏好設定為預設值\n" +
            "**參數：** 無\n" +
            "**範例：**\n" +
            "  `/preferences reset`",
            false);

        return embed;
    }

    private EmbedBuilder BuildUserHelpEmbed()
    {
        var embed = new EmbedBuilder()
            .WithColor(Color.Orange)
            .WithTitle("👤 用戶資訊指令")
            .WithDescription("查看您的統計資料和歷史記錄")
            .WithCurrentTimestamp();

        embed.AddField("/mystats",
            "**說明：** 查看您的使用統計\n" +
            "**參數：** 無\n" +
            "**範例：**\n" +
            "  `/mystats`\n" +
            "**顯示內容：** 今日使用量、剩餘額度、每日限額、帳戶狀態等",
            false);

        embed.AddField("/myhistory",
            "**說明：** 查看您最近的聊天記錄\n" +
            "**參數：**\n" +
            "  • `count` (可選) - 要顯示的訊息數量，預設 10，最多 50\n" +
            "**範例：**\n" +
            "  `/myhistory`\n" +
            "  `/myhistory count:20`",
            false);

        return embed;
    }

    private EmbedBuilder BuildGuildAdminHelpEmbed()
    {
        var embed = new EmbedBuilder()
            .WithColor(Color.Gold)
            .WithTitle("🛡️ 伺服器管理指令")
            .WithDescription("管理伺服器設定和用戶（需要伺服器管理員權限）")
            .WithCurrentTimestamp();

        embed.AddField("伺服器設定",
            "**`/admin guild set-system-prompt`** - 設定伺服器專屬系統提示\n" +
            "**`/admin guild set-daily-limit`** - 設定伺服器預設每日額度\n" +
            "**`/admin guild set-max-tokens`** - 設定伺服器最大回應 Token 數\n" +
            "**`/admin guild set-enable-limits`** - 設定伺服器是否啟用限制\n" +
            "**`/admin guild view-settings`** - 查看伺服器當前設定\n" +
            "**`/admin guild status`** - 查看伺服器狀態和統計",
            false);

        embed.AddField("管理員管理",
            "**`/admin guild add-admin`** - 新增伺服器管理員\n" +
            "**`/admin guild remove-admin`** - 移除伺服器管理員\n" +
            "**`/admin guild list-admins`** - 查看伺服器管理員列表",
            false);

        embed.AddField("用戶管理",
            "**`/admin user-stats`** - 查看指定用戶的使用統計\n" +
            "**`/admin set-limit`** - 設定用戶的每日 Token 額度",
            false);

        embed.AddField("範例",
            "`/admin guild set-daily-limit tokens:50000`\n" +
            "`/admin guild view-settings`\n" +
            "`/admin set-limit user:@使用者 tokens:100000`",
            false);

        return embed;
    }

    private EmbedBuilder BuildGlobalAdminHelpEmbed()
    {
        var embed = new EmbedBuilder()
            .WithColor(Color.Red)
            .WithTitle("🔧 全域管理指令")
            .WithDescription("全域 Bot 設定（僅限 Bot Owner）")
            .WithCurrentTimestamp();

        embed.AddField("全域設定",
            "**`/admin global set-model`** - 設定 LLM 模型名稱\n" +
            "**`/admin global set-temperature`** - 設定生成溫度\n" +
            "**`/admin global set-max-tokens`** - 設定全域最大回應 Token 數\n" +
            "**`/admin global set-system-prompt`** - 設定全域系統提示\n" +
            "**`/admin global set-daily-limit`** - 設定全域預設每日額度\n" +
            "**`/admin global view-settings`** - 查看當前全域設定",
            false);

        embed.AddField("系統資訊",
            "**`/admin global server-info`** - 查看 Bot 主機資訊\n" +
            "**`/admin global stats`** - 查看全域使用統計",
            false);

        embed.AddField("管理員管理",
            "**`/admin global add-guild-admin`** - 新增伺服器管理員\n" +
            "**`/admin global remove-guild-admin`** - 移除伺服器管理員",
            false);

        embed.AddField("用戶管理",
            "**`/admin block`** - 封鎖用戶（全域）\n" +
            "**`/admin unblock`** - 解封用戶（全域）",
            false);

        embed.AddField("範例",
            "`/admin global set-model model:gpt-4o`\n" +
            "`/admin global set-daily-limit tokens:100000`\n" +
            "`/admin global stats`",
            false);

        return embed;
    }

    #endregion
}

/// <summary>
/// Autocomplete handler for help category selection based on user permissions
/// </summary>
public class HelpCategoryAutocompleteHandler(
    DiscordSocketClient client,
    IRepository repository) : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        try
        {
            var userId = context.User.Id;
            var guildId = context.Guild?.Id;

            // Check user permissions
            var isGlobalAdmin = await IsGlobalAdminAsync(context.User.Id);
            var isGuildAdmin = guildId.HasValue && await repository.IsGuildAdminAsync(guildId.Value, userId);

            var suggestions = new List<AutocompleteResult>
            {
                new AutocompleteResult("全部", "all"),
                new AutocompleteResult("聊天", "chat"),
                new AutocompleteResult("記憶", "memory"),
                new AutocompleteResult("個人設定", "preferences"),
                new AutocompleteResult("用戶資訊", "user")
            };

            if (isGuildAdmin || isGlobalAdmin)
            {
                suggestions.Add(new AutocompleteResult("伺服器管理", "guild-admin"));
            }

            if (isGlobalAdmin)
            {
                suggestions.Add(new AutocompleteResult("全域管理", "global-admin"));
            }

            // Filter based on current input
            var userInput = autocompleteInteraction.Data.Current.Value?.ToString()?.ToLower() ?? "";
            var filtered = suggestions
                .Where(s => s.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase) || 
                           s.Value.ToString()!.Contains(userInput, StringComparison.OrdinalIgnoreCase))
                .Take(25)
                .ToList();

            return AutocompletionResult.FromSuccess(filtered);
        }
        catch (Exception)
        {
            // Return default suggestions on error
            return AutocompletionResult.FromSuccess(new[]
            {
                new AutocompleteResult("全部", "all"),
                new AutocompleteResult("聊天", "chat"),
                new AutocompleteResult("記憶", "memory"),
                new AutocompleteResult("個人設定", "preferences"),
                new AutocompleteResult("用戶資訊", "user")
            });
        }
    }

    private async Task<bool> IsGlobalAdminAsync(ulong userId)
    {
        try
        {
            var application = await client.GetApplicationInfoAsync();
            return userId == application.Owner.Id;
        }
        catch
        {
            return false;
        }
    }
}

