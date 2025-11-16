using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using LLMDiscordBot.Services;
using Serilog;

namespace LLMDiscordBot.Commands;

/// <summary>
/// Commands for managing GraphRAG memory system
/// </summary>
[Group("memory", "管理 AI 記憶圖譜")]
public class MemoryCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly GraphMemoryService graphMemoryService;
    private readonly MemoryExtractionBackgroundService memoryExtractionService;
    private readonly ILogger logger;

    public MemoryCommands(
        GraphMemoryService graphMemoryService,
        MemoryExtractionBackgroundService memoryExtractionService,
        ILogger logger)
    {
        this.graphMemoryService = graphMemoryService;
        this.memoryExtractionService = memoryExtractionService;
        this.logger = logger;
    }

    /// <summary>
    /// Manually save content to memory
    /// </summary>
    [SlashCommand("save", "手動標記重要內容以記憶")]
    public async Task SaveMemoryAsync(
        [Summary("content", "要記憶的內容")] string content)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var userId = Context.User.Id;
            var guildId = Context.Guild?.Id;

            logger.Information("User {UserId} manually saving memory in guild {GuildId}", userId, guildId);

            // Store the content directly
            await graphMemoryService.StoreConversationMemoryAsync(userId, guildId, content);

            var embed = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithTitle("✅ 記憶已儲存")
                .WithDescription($"已成功儲存到您的記憶圖譜中。\n\n**內容預覽：**\n{TruncateText(content, 200)}")
                .WithFooter($"記憶索引: {GraphMemoryService.GetUserMemoryIndex(userId, guildId)}")
                .WithCurrentTimestamp()
                .Build();

            await FollowupAsync(embed: embed, ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error saving memory manually");
            
            var errorEmbed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle("❌ 儲存失敗")
                .WithDescription("儲存記憶時發生錯誤，請稍後再試。")
                .Build();

            await FollowupAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Query memory graph
    /// </summary>
    [SlashCommand("recall", "查詢記憶圖譜")]
    public async Task RecallMemoryAsync(
        [Summary("query", "查詢關鍵字")] string query)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var userId = Context.User.Id;
            var guildId = Context.Guild?.Id;

            logger.Information("User {UserId} recalling memory with query: {Query}", userId, query);

            var result = await graphMemoryService.SearchRelevantMemoriesAsync(userId, guildId, query);

            if (!string.IsNullOrWhiteSpace(result))
            {
                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithTitle($"🔍 記憶搜尋結果: {TruncateText(query, 50)}")
                    .WithDescription(TruncateText(result, 4000))
                    .WithFooter($"記憶索引: {GraphMemoryService.GetUserMemoryIndex(userId, guildId)}")
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: embed, ephemeral: true);
            }
            else
            {
                var embed = new EmbedBuilder()
                    .WithColor(Color.Orange)
                    .WithTitle("🔍 未找到相關記憶")
                    .WithDescription($"查詢「{query}」沒有找到相關的記憶內容。")
                    .Build();

                await FollowupAsync(embed: embed, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error recalling memory");
            
            var errorEmbed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle("❌ 查詢失敗")
                .WithDescription("查詢記憶時發生錯誤，請稍後再試。")
                .Build();

            await FollowupAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// List all memory indexes
    /// </summary>
    [SlashCommand("list", "列出您的記憶索引")]
    public async Task ListMemoriesAsync()
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var userId = Context.User.Id;
            
            logger.Information("User {UserId} listing memory indexes", userId);

            var indexes = await graphMemoryService.GetUserMemoryIndexesAsync(userId);

            if (indexes.Count > 0)
            {
                var embedBuilder = new EmbedBuilder()
                    .WithColor(Color.Purple)
                    .WithTitle($"📚 您的記憶索引 ({indexes.Count})")
                    .WithDescription("以下是您在不同伺服器的記憶圖譜：")
                    .WithCurrentTimestamp();

                foreach (var index in indexes)
                {
                    var stats = await graphMemoryService.GetMemoryStatsAsync(index);
                    var statsText = stats != null
                        ? $"節點: {stats.NodeCount}, 邊: {stats.EdgeCount}, 社群: {stats.CommunityCount}"
                        : "無統計資訊";

                    embedBuilder.AddField(index, statsText, inline: false);
                }

                await FollowupAsync(embed: embedBuilder.Build(), ephemeral: true);
            }
            else
            {
                var embed = new EmbedBuilder()
                    .WithColor(Color.Orange)
                    .WithTitle("📚 無記憶索引")
                    .WithDescription("您目前還沒有任何記憶圖譜。記憶會在對話中自動建立。")
                    .Build();

                await FollowupAsync(embed: embed, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error listing memory indexes");
            
            var errorEmbed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle("❌ 列表失敗")
                .WithDescription("列出記憶索引時發生錯誤，請稍後再試。")
                .Build();

            await FollowupAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Clear memory index
    /// </summary>
    [SlashCommand("clear", "清除記憶圖譜")]
    public async Task ClearMemoryAsync(
        [Summary("scope", "清除範圍：當前伺服器或所有")] 
        [Choice("當前伺服器", "current")]
        [Choice("所有記憶", "all")]
        string scope = "current")
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var userId = Context.User.Id;
            var guildId = Context.Guild?.Id;

            logger.Information("User {UserId} clearing memory with scope: {Scope}", userId, scope);

            if (scope == "current")
            {
                var index = GraphMemoryService.GetUserMemoryIndex(userId, guildId);
                var hasContent = await graphMemoryService.CheckIfIndexHasContentAsync(index);

                if (hasContent)
                {
                    await graphMemoryService.DeleteMemoryIndexAsync(index);

                    var embed = new EmbedBuilder()
                        .WithColor(Color.Green)
                        .WithTitle("🗑️ 記憶已清除")
                        .WithDescription($"已成功清除當前伺服器的記憶圖譜。\n\n索引：`{index}`")
                        .WithCurrentTimestamp()
                        .Build();

                    await FollowupAsync(embed: embed, ephemeral: true);
                }
                else
                {
                    var embed = new EmbedBuilder()
                        .WithColor(Color.Orange)
                        .WithTitle("🗑️ 無記憶可清除")
                        .WithDescription("當前伺服器沒有記憶圖譜。")
                        .Build();

                    await FollowupAsync(embed: embed, ephemeral: true);
                }
            }
            else if (scope == "all")
            {
                var indexes = await graphMemoryService.GetUserMemoryIndexesAsync(userId);
                var deletedCount = 0;

                foreach (var index in indexes)
                {
                    try
                    {
                        await graphMemoryService.DeleteMemoryIndexAsync(index);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        logger.Warning(ex, "Error deleting index {Index}", index);
                    }
                }

                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("🗑️ 記憶已清除")
                    .WithDescription($"已成功清除 {deletedCount} 個記憶圖譜。")
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: embed, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error clearing memory");
            
            var errorEmbed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle("❌ 清除失敗")
                .WithDescription("清除記憶時發生錯誤，請稍後再試。")
                .Build();

            await FollowupAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Get memory statistics
    /// </summary>
    [SlashCommand("stats", "查看記憶統計資訊")]
    public async Task GetMemoryStatsAsync()
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var userId = Context.User.Id;
            var guildId = Context.Guild?.Id;
            var index = GraphMemoryService.GetUserMemoryIndex(userId, guildId);

            logger.Information("User {UserId} getting memory stats for index {Index}", userId, index);

            var stats = await graphMemoryService.GetMemoryStatsAsync(index);

            if (stats != null && stats.NodeCount > 0)
            {
                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithTitle("📊 記憶統計資訊")
                    .WithDescription($"您在當前伺服器的記憶圖譜統計：")
                    .AddField("索引", $"`{stats.Index}`", inline: false)
                    .AddField("節點數量", stats.NodeCount.ToString(), inline: true)
                    .AddField("邊數量", stats.EdgeCount.ToString(), inline: true)
                    .AddField("社群數量", stats.CommunityCount.ToString(), inline: true)
                    .AddField("社群分析", stats.HasCommunities ? "✅ 已完成" : "⏳ 待完成", inline: true)
                    .WithFooter("記憶圖譜會隨著對話自動更新")
                    .WithCurrentTimestamp()
                    .Build();

                await FollowupAsync(embed: embed, ephemeral: true);
            }
            else
            {
                var embed = new EmbedBuilder()
                    .WithColor(Color.Orange)
                    .WithTitle("📊 無記憶資料")
                    .WithDescription($"當前伺服器還沒有建立記憶圖譜。\n\n記憶會在對話中自動建立。")
                    .WithFooter($"索引：{index}")
                    .Build();

                await FollowupAsync(embed: embed, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting memory stats");
            
            var errorEmbed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle("❌ 獲取失敗")
                .WithDescription("獲取記憶統計時發生錯誤，請稍後再試。")
                .Build();

            await FollowupAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Truncate text to maximum length
    /// </summary>
    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..(maxLength - 3)] + "...";
    }
}


