using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using Serilog;

namespace LLMDiscordBot.Services;

/// <summary>
/// Background service for processing memory extraction queue
/// </summary>
public class MemoryExtractionBackgroundService : BackgroundService
{
    private readonly Channel<MemoryExtractionTask> taskQueue;
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger logger;

    public MemoryExtractionBackgroundService(
        IServiceProvider serviceProvider,
        ILogger logger)
    {
        // Create unbounded channel for task queue
        this.taskQueue = Channel.CreateUnbounded<MemoryExtractionTask>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true
        });
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    /// <summary>
    /// Queue a memory extraction task for background processing
    /// </summary>
    public void QueueMemoryExtraction(MemoryExtractionTask task)
    {
        if (task == null)
        {
            throw new ArgumentNullException(nameof(task));
        }

        var success = taskQueue.Writer.TryWrite(task);
        if (success)
        {
            logger.Debug("Queued memory extraction task for user {UserId} in guild {GuildId}", 
                task.UserId, task.GuildId);
        }
        else
        {
            logger.Warning("Failed to queue memory extraction task for user {UserId}", task.UserId);
        }
    }

    /// <summary>
    /// Background task executor
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.Information("Memory extraction background service started");

        try
        {
            await foreach (var task in taskQueue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessMemoryExtractionTaskAsync(task, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error processing memory extraction task for user {UserId}", task.UserId);
                    // Continue processing other tasks
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.Information("Memory extraction background service is stopping");
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "Memory extraction background service encountered fatal error");
        }
    }

    /// <summary>
    /// Process a single memory extraction task
    /// </summary>
    private async Task ProcessMemoryExtractionTaskAsync(
        MemoryExtractionTask task,
        CancellationToken cancellationToken)
    {
        logger.Debug("Processing memory extraction for user {UserId} in guild {GuildId}", 
            task.UserId, task.GuildId);

        // Create a scope for scoped services
        using var scope = serviceProvider.CreateScope();
        var memoryAnalyzer = scope.ServiceProvider.GetRequiredService<MemoryAnalyzerService>();
        var graphMemory = scope.ServiceProvider.GetRequiredService<GraphMemoryService>();

        try
        {
            // Analyze conversation and extract memory-worthy content
            var extractedContent = await memoryAnalyzer.AnalyzeConversationForMemoryAsync(
                task.RecentConversation,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(extractedContent))
            {
                // Store the extracted memory in GraphRag
                await graphMemory.StoreConversationMemoryAsync(
                    task.UserId,
                    task.GuildId,
                    extractedContent,
                    cancellationToken);

                logger.Information(
                    "Successfully processed and stored memory for user {UserId} in guild {GuildId}", 
                    task.UserId, task.GuildId);
            }
            else
            {
                logger.Debug(
                    "No memory-worthy content found for user {UserId} in guild {GuildId}", 
                    task.UserId, task.GuildId);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, 
                "Error processing memory extraction for user {UserId} in guild {GuildId}", 
                task.UserId, task.GuildId);
            throw;
        }
    }

    /// <summary>
    /// Cleanup on service shutdown
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.Information("Memory extraction background service is shutting down");
        
        // Signal no more writes
        taskQueue.Writer.Complete();
        
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Task for memory extraction
/// </summary>
public class MemoryExtractionTask
{
    public ulong UserId { get; set; }
    public ulong? GuildId { get; set; }
    public List<ChatMessage> RecentConversation { get; set; } = new();
}


