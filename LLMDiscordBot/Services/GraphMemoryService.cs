using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using LLMDiscordBot.Configuration;
using Serilog;

namespace LLMDiscordBot.Services;

/// <summary>
/// Service for managing GraphRAG memory storage and retrieval
/// </summary>
public class GraphMemoryService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger logger;
    private readonly MemoryExtractionConfig config;
    private dynamic? graphService;

    public GraphMemoryService(
        IServiceProvider serviceProvider,
        ILogger logger,
        IOptions<GraphRagConfig> graphRagConfig)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.config = graphRagConfig.Value.MemoryExtraction;
        
        // Try to resolve IGraphService at initialization time with multiple possible type names
        InitializeGraphService();
    }

    private void InitializeGraphService()
    {
        // Try different possible type names for IGraphService
        var possibleTypeNames = new[]
        {
            "GraphRag.Net.Domain.IGraphService, GraphRag.Net",
            "GraphRag.Net.IGraphService, GraphRag.Net",
            "GraphRag.Net.Service.IGraphService, GraphRag.Net",
            "GraphRag.Net.Abstraction.IGraphService, GraphRag.Net"
        };

        foreach (var typeName in possibleTypeNames)
        {
            var graphServiceType = Type.GetType(typeName);
            if (graphServiceType != null)
            {
                graphService = serviceProvider.GetService(graphServiceType);
                if (graphService != null)
                {
                    logger.Information("Successfully resolved IGraphService using type: {TypeName}", typeName);
                    return;
                }
            }
        }
        
        // If we couldn't resolve the service, log a warning but don't throw
        logger.Warning("Could not resolve IGraphService. Memory features will be unavailable.");
    }

    private dynamic? GetGraphService()
    {
        if (graphService == null)
        {
            logger.Debug("GraphService is not available");
        }
        return graphService;
    }

    /// <summary>
    /// Generate index name for user memory in guild
    /// </summary>
    public string GetUserMemoryIndex(ulong userId, ulong? guildId)
    {
        if (guildId.HasValue)
        {
            return $"user_{userId}_guild_{guildId.Value}";
        }
        return $"user_{userId}_dm";
    }

    /// <summary>
    /// Generate index name for guild shared memory
    /// </summary>
    public string GetGuildSharedMemoryIndex(ulong guildId)
    {
        return $"guild_{guildId}_shared";
    }

    /// <summary>
    /// Store conversation memory
    /// </summary>
    public async Task StoreConversationMemoryAsync(
        ulong userId,
        ulong? guildId,
        string conversationText,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var service = GetGraphService();
            if (service == null)
            {
                logger.Debug("GraphService not available, skipping memory storage");
                return;
            }

            var index = GetUserMemoryIndex(userId, guildId);
            
            logger.Debug("Storing conversation memory to index {Index}", index);

            // Insert the conversation text into GraphRag
            await service.InsertTextChunkAsync(index, conversationText);

            // Generate communities and global summaries for better retrieval
            await service.GraphCommunitiesAsync(index);
            await service.GraphGlobalAsync(index);

            logger.Information("Successfully stored conversation memory to index {Index}", index);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error storing conversation memory for user {UserId} in guild {GuildId}", 
                userId, guildId);
            // Don't throw - memory storage failures shouldn't break normal conversation
        }
    }

    /// <summary>
    /// Search relevant memories based on query
    /// </summary>
    public async Task<string?> SearchRelevantMemoriesAsync(
        ulong userId,
        ulong? guildId,
        string query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var service = GetGraphService();
            if (service == null)
            {
                logger.Debug("GraphService not available, skipping memory search");
                return null;
            }

            var index = GetUserMemoryIndex(userId, guildId);

            // Check if index exists and has content
            var hasContent = await CheckIfIndexHasContentAsync(index);
            if (!hasContent)
            {
                logger.Debug("No memories found in index {Index}", index);
                return null;
            }

            logger.Debug("Searching memories in index {Index} for query: {Query}", index, query);

            // Search using GraphRag community-based search (more comprehensive)
            var searchResult = await service.SearchGraphCommunityAsync(index, query);

            if (!string.IsNullOrWhiteSpace(searchResult))
            {
                logger.Information("Found relevant memories in index {Index}, length: {Length}", 
                    index, searchResult.Length);
                return searchResult;
            }

            logger.Debug("No relevant memories found in index {Index}", index);
            return null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error searching memories for user {UserId} in guild {GuildId}", 
                userId, guildId);
            return null; // Don't throw - memory retrieval failures shouldn't break conversation
        }
    }

    /// <summary>
    /// Determine if memory retrieval is needed based on user message
    /// </summary>
    public async Task<bool> ShouldRetrieveMemoryAsync(
        string userMessage,
        ulong userId,
        ulong? guildId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Heuristic rules
            if (userMessage.Length < 10)
            {
                return false;
            }

            // Keywords that suggest memory retrieval is needed
            var memoryKeywords = new[]
            {
                "記得", "之前", "上次", "你說過", "我們討論過", "提到過", "講過",
                "remember", "previous", "before", "you said", "we discussed", "mentioned",
                "earlier", "last time", "recall"
            };

            var shouldRetrieve = memoryKeywords.Any(k =>
                userMessage.Contains(k, StringComparison.OrdinalIgnoreCase));

            if (shouldRetrieve)
            {
                logger.Debug("Memory retrieval triggered by keyword in message");
                return true;
            }

            // Check if memory index exists and has content
            var index = GetUserMemoryIndex(userId, guildId);
            var hasMemories = await CheckIfIndexHasContentAsync(index);

            if (hasMemories)
            {
                // If memories exist, retrieve them for context (intelligent default)
                logger.Debug("Memory index {Index} has content, will retrieve", index);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error determining if memory retrieval is needed");
            return false;
        }
    }

    /// <summary>
    /// Check if a memory index exists and has content
    /// </summary>
    public Task<bool> CheckIfIndexHasContentAsync(string index)
    {
        try
        {
            var service = GetGraphService();
            if (service == null)
            {
                return Task.FromResult(false);
            }

            var allIndexes = (IEnumerable<string>)service.GetAllIndex();
            var result = allIndexes.Any(i => i.Equals(index, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error checking if index {Index} has content", index);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Get all memory indexes for user
    /// </summary>
    public Task<List<string>> GetUserMemoryIndexesAsync(ulong userId)
    {
        try
        {
            var service = GetGraphService();
            if (service == null)
            {
                return Task.FromResult(new List<string>());
            }

            var allIndexes = (IEnumerable<string>)service.GetAllIndex();
            var userPrefix = $"user_{userId}_";
            
            var result = allIndexes
                .Where(i => i.StartsWith(userPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting memory indexes for user {UserId}", userId);
            return Task.FromResult(new List<string>());
        }
    }

    /// <summary>
    /// Delete memory index
    /// </summary>
    public async Task DeleteMemoryIndexAsync(string index)
    {
        try
        {
            var service = GetGraphService();
            if (service == null)
            {
                logger.Warning("GraphService not available, cannot delete memory index {Index}", index);
                throw new InvalidOperationException("GraphService not available");
            }

            logger.Information("Deleting memory index {Index}", index);
            await service.DeleteGraph(index);
            logger.Information("Successfully deleted memory index {Index}", index);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error deleting memory index {Index}", index);
            throw;
        }
    }

    /// <summary>
    /// Get memory statistics for an index
    /// </summary>
    public Task<MemoryStats?> GetMemoryStatsAsync(string index)
    {
        try
        {
            var service = GetGraphService();
            if (service == null)
            {
                return Task.FromResult<MemoryStats?>(null);
            }

            dynamic graphModel = service.GetAllGraphs(index);
            
            if (graphModel == null)
            {
                return Task.FromResult<MemoryStats?>(null);
            }

            var stats = new MemoryStats
            {
                Index = index,
                NodeCount = graphModel.Nodes?.Count ?? 0,
                EdgeCount = graphModel.Edges?.Count ?? 0,
                HasCommunities = graphModel.Communities?.Any() ?? false,
                CommunityCount = graphModel.Communities?.Count ?? 0
            };
            return Task.FromResult<MemoryStats?>(stats);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting memory stats for index {Index}", index);
            return Task.FromResult<MemoryStats?>(null);
        }
    }
}

/// <summary>
/// Memory statistics for an index
/// </summary>
public class MemoryStats
{
    public string Index { get; set; } = string.Empty;
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public bool HasCommunities { get; set; }
    public int CommunityCount { get; set; }
}

