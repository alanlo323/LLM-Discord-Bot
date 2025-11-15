namespace LLMDiscordBot.Configuration;

/// <summary>
/// Discord Bot configuration
/// </summary>
public class DiscordConfig
{
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// LLM API configuration
/// </summary>
public class LLMConfig
{
    public string ApiEndpoint { get; set; } = string.Empty;
    public string Model { get; set; } = "default";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
    public string SystemPrompt { get; set; } = "You are a helpful AI assistant.";
}

/// <summary>
/// Token usage limits configuration
/// </summary>
public class TokenLimitsConfig
{
    public int DefaultDailyLimit { get; set; } = 100000;
    public bool EnableLimits { get; set; } = true;
}

/// <summary>
/// Database configuration
/// </summary>
public class DatabaseConfig
{
    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>
/// MCP (Model Context Protocol) configuration
/// </summary>
public class McpConfig
{
    public TavilyConfig Tavily { get; set; } = new();
}

/// <summary>
/// Tavily MCP configuration
/// </summary>
public class TavilyConfig
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

