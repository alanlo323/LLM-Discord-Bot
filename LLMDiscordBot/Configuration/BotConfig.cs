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

/// <summary>
/// GraphRag configuration
/// </summary>
public class GraphRagConfig
{
    public GraphOpenAIConfig OpenAI { get; set; } = new();
    public TextChunkerConfig TextChunker { get; set; } = new();
    public GraphDBConnectionConfig GraphDBConnection { get; set; } = new();
    public GraphSearchConfig GraphSearch { get; set; } = new();
    public GraphSysConfig GraphSys { get; set; } = new();
    public MemoryExtractionConfig MemoryExtraction { get; set; } = new();
}

/// <summary>
/// GraphRag OpenAI configuration
/// </summary>
public class GraphOpenAIConfig
{
    public string Key { get; set; } = string.Empty;
    public string EndPoint { get; set; } = string.Empty;
    public string ChatModel { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
}

/// <summary>
/// Text chunker configuration
/// </summary>
public class TextChunkerConfig
{
    public int LinesToken { get; set; } = 100;
    public int ParagraphsToken { get; set; } = 1000;
}

/// <summary>
/// Graph database connection configuration
/// </summary>
public class GraphDBConnectionConfig
{
    public string DbType { get; set; } = "Sqlite";
    public string DBConnection { get; set; } = string.Empty;
    public string VectorConnection { get; set; } = string.Empty;
    public int VectorSize { get; set; } = 1536;
}

/// <summary>
/// Graph search configuration
/// </summary>
public class GraphSearchConfig
{
    public double SearchMinRelevance { get; set; } = 0.5;
    public int SearchLimit { get; set; } = 5;
    public int NodeDepth { get; set; } = 2;
    public int MaxNodes { get; set; } = 50;
}

/// <summary>
/// Graph system configuration
/// </summary>
public class GraphSysConfig
{
    public int RetryCounnt { get; set; } = 2;
}

/// <summary>
/// Memory extraction configuration
/// </summary>
public class MemoryExtractionConfig
{
    public int MinConversationLength { get; set; } = 3;
    public bool EnableAutoExtraction { get; set; } = true;
    public string ExtractionPrompt { get; set; } = "Analyze the conversation and extract important facts, relationships, and entities that should be remembered.";
    public int HistoryMessagesCount { get; set; } = 10;
}

