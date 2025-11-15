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
    /// Search the web using Tavily API
    /// </summary>
    public async Task<string> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.Information("Performing web search: {Query}", query);

            // Create the direct Tavily API request
            var tavilyRequest = new TavilySearchRequest
            {
                Query = query,
                IncludeRawContent = "markdown",
                IncludeImages = true,
                IncludeFavicon = true
            };

            logger.Debug("Sending request to Tavily API: {Request}", JsonSerializer.Serialize(tavilyRequest));

            // Set authorization header
            using var request = new HttpRequestMessage(HttpMethod.Post, config.Endpoint);
            request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
            request.Content = JsonContent.Create(tavilyRequest);

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.Error("Tavily API request failed with status {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                return $"Search failed: {response.StatusCode}";
            }

            // Read the raw response content for debugging
            var rawResponseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.Debug("Raw response content: {RawContent}", rawResponseContent);
            logger.Debug("Response headers: {Headers}", response.Headers.ToString());
            logger.Debug("Content headers: {ContentHeaders}", response.Content.Headers.ToString());

            // Deserialize the response directly as TavilyResponse
            var tavilyData = JsonSerializer.Deserialize<TavilyResponse>(rawResponseContent);

            if (tavilyData?.Results == null || tavilyData.Results.Count == 0)
            {
                logger.Warning("Tavily API returned empty response");
                return "No search results found.";
            }

            // Format the search results
            var formattedResults = FormatSearchResults(tavilyData);
            logger.Information("Web search completed successfully, returned {Count} results and {ImageCount} images", 
                tavilyData.Results?.Count ?? 0, tavilyData.Images?.Count ?? 0);

            return formattedResults;
        }
        catch (JsonException ex)
        {
            logger.Error(ex, "JSON deserialization error during Tavily MCP search. This typically indicates the response is not valid JSON.");
            return "Search error: Invalid response format received from search service. Please try again later.";
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

    /// <summary>
    /// Format search results for LLM consumption
    /// </summary>
    private string FormatSearchResults(TavilyResponse tavilyData)
    {
        var resultText = new System.Text.StringBuilder();
        
        resultText.AppendLine($"Search Query: {tavilyData.Query}");
        resultText.AppendLine();

        // Add images if available
        if (tavilyData.Images != null && tavilyData.Images.Count > 0)
        {
            resultText.AppendLine("### Images:");
            foreach (var imageUrl in tavilyData.Images.Take(5))
            {
                resultText.AppendLine($"- {imageUrl}");
            }
            resultText.AppendLine();
        }

        // Add search results
        if (tavilyData.Results != null && tavilyData.Results.Count > 0)
        {
            resultText.AppendLine("### Search Results:");
            resultText.AppendLine();

            foreach (var result in tavilyData.Results)
            {
                resultText.AppendLine($"**{result.Title}**");
                resultText.AppendLine($"URL: {result.Url}");
                resultText.AppendLine($"Relevance Score: {result.Score:F2}");
                resultText.AppendLine();
                
                // Use raw_content if available, otherwise use content
                if (!string.IsNullOrWhiteSpace(result.RawContent))
                {
                    resultText.AppendLine(result.RawContent);
                }
                else if (!string.IsNullOrWhiteSpace(result.Content))
                {
                    resultText.AppendLine(result.Content);
                }
                
                resultText.AppendLine();
                resultText.AppendLine("---");
                resultText.AppendLine();
            }
        }

        return resultText.ToString();
    }
}

#region MCP Protocol Models
// NOTE: These MCP protocol classes are currently not in use but retained for future flexibility
// if we need to switch back to using the MCP server instead of direct API calls.

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
    [JsonPropertyName("include_raw_content")]
    public string IncludeRawContent { get; set; } = string.Empty;
    [JsonPropertyName("include_images")]
    public bool IncludeImage { get; set; } = false;
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

#region Tavily Request and Response Models

/// <summary>
/// Tavily search request structure
/// </summary>
public class TavilySearchRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("max_results")]
    public int MaxResults { get; set; } = 5;

    [JsonPropertyName("search_depth")]
    public string SearchDepth { get; set; } = "basic";

    [JsonPropertyName("include_raw_content")]
    public string IncludeRawContent { get; set; } = string.Empty;

    [JsonPropertyName("include_images")]
    public bool IncludeImages { get; set; } = false;

    [JsonPropertyName("include_favicon")]
    public bool IncludeFavicon { get; set; } = false;
}

/// <summary>
/// Tavily search response structure
/// </summary>
public class TavilyResponse
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("follow_up_questions")]
    public List<string>? FollowUpQuestions { get; set; }

    [JsonPropertyName("answer")]
    public string? Answer { get; set; }

    [JsonPropertyName("images")]
    public List<string> Images { get; set; } = new();

    [JsonPropertyName("results")]
    public List<TavilySearchResult> Results { get; set; } = new();

    [JsonPropertyName("response_time")]
    public double ResponseTime { get; set; }

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;
}

/// <summary>
/// Individual Tavily search result
/// </summary>
public class TavilySearchResult
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("raw_content")]
    public string? RawContent { get; set; }

    [JsonPropertyName("favicon")]
    public string? Favicon { get; set; }
}

#endregion

