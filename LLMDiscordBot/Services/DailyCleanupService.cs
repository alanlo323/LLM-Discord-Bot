using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using LLMDiscordBot.Data;
using Serilog;

namespace LLMDiscordBot.Services;

/// <summary>
/// Background service for daily cleanup tasks
/// </summary>
public class DailyCleanupService : BackgroundService
{
    private readonly IServiceProvider services;
    private readonly ILogger logger;
    private readonly TimeSpan checkInterval = TimeSpan.FromHours(1);

    public DailyCleanupService(IServiceProvider services, ILogger logger)
    {
        this.services = services;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.Information("Daily cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during daily cleanup");
            }

            // Wait for next check
            await Task.Delay(checkInterval, stoppingToken);
        }

        logger.Information("Daily cleanup service stopped");
    }

    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        try
        {
            // Clean up old token usage records (keep last 90 days)
            var cutoffDate = DateTime.UtcNow.Date.AddDays(-90);
            var oldUsages = await context.TokenUsages
                .Where(t => t.Date < cutoffDate)
                .ToListAsync(cancellationToken);

            if (oldUsages.Any())
            {
                context.TokenUsages.RemoveRange(oldUsages);
                await context.SaveChangesAsync(cancellationToken);
                logger.Information("Cleaned up {Count} old token usage records", oldUsages.Count);
            }

            // Clean up old chat history (keep last 30 days)
            var chatCutoffDate = DateTime.UtcNow.AddDays(-30);
            var oldChats = await context.ChatHistories
                .Where(c => c.Timestamp < chatCutoffDate)
                .ToListAsync(cancellationToken);

            if (oldChats.Any())
            {
                context.ChatHistories.RemoveRange(oldChats);
                await context.SaveChangesAsync(cancellationToken);
                logger.Information("Cleaned up {Count} old chat history records", oldChats.Count);
            }

            // Log database statistics
            var userCount = await context.Users.CountAsync(cancellationToken);
            var todayUsageCount = await context.TokenUsages
                .Where(t => t.Date == DateTime.UtcNow.Date)
                .CountAsync(cancellationToken);

            logger.Information("Database stats - Users: {Users}, Today's usage records: {UsageCount}",
                userCount, todayUsageCount);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error performing cleanup tasks");
        }
    }
}

