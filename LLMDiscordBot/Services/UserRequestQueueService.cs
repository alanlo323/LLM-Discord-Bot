using System.Collections.Concurrent;
using Serilog;

namespace LLMDiscordBot.Services;

/// <summary>
/// Service to serialize requests per user to prevent concurrent processing issues
/// </summary>
public class UserRequestQueueService : IDisposable
{
    private readonly ILogger logger;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> userSemaphores = new();
    private readonly ConcurrentDictionary<ulong, DateTime> lastAccessTimes = new();
    private readonly SemaphoreSlim cleanupLock = new(1, 1);
    private readonly TimeSpan semaphoreTimeout = TimeSpan.FromMinutes(5);
    private readonly Timer cleanupTimer;
    private bool disposed;

    public UserRequestQueueService(ILogger logger)
    {
        this.logger = logger;
        // Start cleanup timer to run every 5 minutes
        cleanupTimer = new Timer(CleanupUnusedSemaphores, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Acquire lock for a specific user to serialize their requests
    /// </summary>
    public async Task<IDisposable> AcquireUserLockAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var semaphore = userSemaphores.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        lastAccessTimes[userId] = DateTime.UtcNow;

        logger.Debug("User {UserId} attempting to acquire request lock", userId);
        await semaphore.WaitAsync(cancellationToken);
        logger.Debug("User {UserId} acquired request lock", userId);

        return new UserLockReleaser(userId, this);
    }

    /// <summary>
    /// Release lock for a specific user
    /// </summary>
    private void ReleaseUserLock(ulong userId)
    {
        if (userSemaphores.TryGetValue(userId, out var semaphore))
        {
            semaphore.Release();
            lastAccessTimes[userId] = DateTime.UtcNow;
            logger.Debug("User {UserId} released request lock", userId);
        }
    }

    /// <summary>
    /// Cleanup unused semaphores to prevent memory leaks
    /// </summary>
    private async void CleanupUnusedSemaphores(object? state)
    {
        if (!await cleanupLock.WaitAsync(0))
        {
            // Cleanup already in progress
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var keysToRemove = new List<ulong>();

            foreach (var kvp in lastAccessTimes)
            {
                if (now - kvp.Value > semaphoreTimeout)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            var cleanedCount = 0;
            foreach (var userId in keysToRemove)
            {
                // Check if the semaphore still exists and hasn't been recently accessed
                if (userSemaphores.TryGetValue(userId, out var semaphore))
                {
                    // Double-check the last access time to avoid race condition
                    // with concurrent AcquireUserLockAsync calls
                    if (lastAccessTimes.TryGetValue(userId, out var lastAccess) && 
                        (now - lastAccess) > semaphoreTimeout)
                    {
                        // Try to acquire the semaphore with zero timeout to check if it's in use
                        if (semaphore.Wait(0))
                        {
                            try
                            {
                                // Successfully acquired - semaphore is not in use, safe to remove
                                if (userSemaphores.TryRemove(userId, out _))
                                {
                                    lastAccessTimes.TryRemove(userId, out _);
                                    semaphore.Dispose();
                                    cleanedCount++;
                                    logger.Debug("Cleaned up semaphore for user {UserId}", userId);
                                }
                            }
                            catch
                            {
                                // If dispose fails, release the semaphore
                                semaphore.Release();
                            }
                        }
                        // If semaphore is in use (Wait returned false), skip cleanup
                    }
                }
            }

            if (cleanedCount > 0)
            {
                logger.Information("Cleaned up {Count} unused user semaphores", cleanedCount);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error during semaphore cleanup");
        }
        finally
        {
            cleanupLock.Release();
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        
        disposed = true;
        cleanupTimer?.Dispose();
        cleanupLock?.Dispose();

        foreach (var semaphore in userSemaphores.Values)
        {
            semaphore.Dispose();
        }

        userSemaphores.Clear();
        lastAccessTimes.Clear();
    }

    /// <summary>
    /// Helper class to automatically release lock when disposed
    /// </summary>
    private class UserLockReleaser(ulong userId, UserRequestQueueService service) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            service.ReleaseUserLock(userId);
        }
    }
}

