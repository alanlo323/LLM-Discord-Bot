using LLMDiscordBot.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace LLMDiscordBot.Services;

/// <summary>
/// Background worker that periodically checks Tell-me-when style monitors.
/// </summary>
public class TaskMonitoringService(IServiceProvider serviceProvider, ILogger logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.Information("Task monitoring service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

                var dueTasks = await repository.GetDueMonitoredTasksAsync(DateTime.UtcNow, 20);
                foreach (var monitor in dueTasks)
                {
                    logger.Information("Monitor {MonitorId} triggered for session {SessionId}", monitor.Id, monitor.TaskSessionId);
                    monitor.LastCheckAt = DateTime.UtcNow;
                    monitor.NextCheckAt = DateTime.UtcNow.AddMinutes(monitor.CheckIntervalMinutes);
                    await repository.UpdateMonitoredTaskAsync(monitor);
                }
            }
            catch (TaskCanceledException)
            {
                // Service shutting down.
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Task monitoring loop failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        logger.Information("Task monitoring service stopped");
    }
}

