using Microsoft.SemanticKernel;
using LLMDiscordBot.Services;
using System.ComponentModel;

namespace LLMDiscordBot.Plugins;

/// <summary>
/// Semantic Kernel plugin for web search using Tavily MCP
/// </summary>
public class TavilySearchPlugin
{
    private readonly McpService mcpService;

    public TavilySearchPlugin(McpService mcpService)
    {
        this.mcpService = mcpService;
    }

    [KernelFunction("web_search")]
    [Description("Search the web for current information, news, facts, or any real-time data. Use this when you need up-to-date information that you don't have in your training data.")]
    public async Task<string> SearchAsync(
        [Description("The search query to find information on the web")] string query,
        CancellationToken cancellationToken = default)
    {
        return await mcpService.SearchAsync(query, cancellationToken);
    }
}

