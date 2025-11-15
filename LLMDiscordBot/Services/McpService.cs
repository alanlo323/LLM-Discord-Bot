using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http;
using LLMDiscordBot.Configuration;
using Serilog;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMDiscordBot.Services;

/// <summary>
/// Service for interacting with Tavily MCP (Model Context Protocol) server
/// </summary>
public class McpService
{
    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly TavilyConfig config;

    public McpService(
        IOptions<McpConfig> mcpConfig,
        IHttpClientFactory httpClientFactory,
        ILogger logger)
    {
        this.config = mcpConfig.Value.Tavily;
        this.httpClient = httpClientFactory.CreateClient("TavilyMcp");
        this.logger = logger;

        // Configure base address
        if (!string.IsNullOrEmpty(this.config.Endpoint))
        {
            this.httpClient.BaseAddress = new Uri(this.config.Endpoint);
        }

        this.logger.Information("MCP Service initialized with endpoint: {Endpoint}", this.config.Endpoint);
    }

    /// <summary>
    /// Search the web using Tavily MCP
    /// </summary>
    public async Task<string> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.Information("Performing web search: {Query}", query);

            // Build the request URL with the API key as query parameter
            var requestUrl = $"?tavilyApiKey={Uri.EscapeDataString(config.ApiKey)}";

            // Create the MCP request
            var mcpRequest = new McpRequest
            {
                Method = "tools/call",
                Params = new McpRequestParams
                {
                    Name = "tavily_search",
                    Arguments = new TavilySearchArguments
                    {
                        Query = query
                    }
                }
            };

            logger.Debug("Sending MCP request to Tavily: {Request}", JsonSerializer.Serialize(mcpRequest));

            var response = await httpClient.PostAsJsonAsync(requestUrl, mcpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.Error("Tavily MCP request failed with status {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                return $"Search failed: {response.StatusCode}";
            }

            var mcpResponse = await response.Content.ReadFromJsonAsync<McpResponse>(cancellationToken: cancellationToken);

            if (mcpResponse?.Result?.Content == null || mcpResponse.Result.Content.Count == 0)
            {
                logger.Warning("Tavily MCP returned empty response");
                return "No search results found.";
            }

            // Extract text content from the response
            var results = new List<string>();
            foreach (var content in mcpResponse.Result.Content)
            {
                if (content.Type == "text" && !string.IsNullOrWhiteSpace(content.Text))
                {
                    results.Add(content.Text);
                }
            }

            var searchResults = string.Join("\n\n", results);
            logger.Information("Web search completed successfully, returned {Count} results", results.Count);

            return searchResults;
        }
        catch (HttpRequestException ex)
        {
            logger.Error(ex, "HTTP error during Tavily MCP search");
            return $"Search error: Unable to connect to search service. {ex.Message}";
        }
        catch (TaskCanceledException ex)
        {
            logger.Error(ex, "Tavily MCP search timed out");
            return "Search error: Request timed out.";
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Unexpected error during Tavily MCP search");
            return $"Search error: {ex.Message}";
        }
    }
}

#region MCP Protocol Models

/// <summary>
/// MCP request structure
/// </summary>
public class McpRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public McpRequestParams Params { get; set; } = new();

    [JsonPropertyName("id")]
    public int Id { get; set; } = 1;
}

/// <summary>
/// MCP request parameters
/// </summary>
public class McpRequestParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public TavilySearchArguments Arguments { get; set; } = new();
}

/// <summary>
/// Tavily search arguments
/// </summary>
public class TavilySearchArguments
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("max_results")]
    public int MaxResults { get; set; } = 5;

    [JsonPropertyName("search_depth")]
    public string SearchDepth { get; set; } = "basic";
}

/// <summary>
/// MCP response structure
/// </summary>
public class McpResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public McpResult? Result { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }
}

/// <summary>
/// MCP result
/// </summary>
public class McpResult
{
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; set; } = new();

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
}

/// <summary>
/// MCP content item
/// </summary>
public class McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

#endregion

