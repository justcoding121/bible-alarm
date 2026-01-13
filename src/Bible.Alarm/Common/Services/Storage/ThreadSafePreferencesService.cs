#nullable enable

using Bible.Alarm.Common.Interfaces.Storage;
using Serilog;

namespace Bible.Alarm.Common.Services.Storage;

/// <summary>
/// Thread-safe implementation of Preferences access.
/// Uses a SemaphoreSlim to serialize all Preferences operations to prevent
/// file locking issues on Windows when multiple threads access the preferences file concurrently.
/// </summary>
public sealed class ThreadSafePreferencesService : IThreadSafePreferencesService
{
    private static readonly ILogger logger = Log.ForContext(typeof(ThreadSafePreferencesService));
    
    // SemaphoreSlim for async/await support and thread-safe access
    // Initial count of 1 ensures only one operation at a time
    private static readonly SemaphoreSlim preferencesLock = new(1, 1);

    public T? Get<T>(string key, T? defaultValue = default, string? sharedName = null)
    {
        preferencesLock.Wait();
        try
        {
            return Preferences.Get(key, defaultValue, sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error reading from Preferences for key: {Key}", key);
            return defaultValue;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public void Set<T>(string key, T? value, string? sharedName = null)
    {
        preferencesLock.Wait();
        try
        {
            // Retry logic for Preferences.Set() which can throw IOException if file is locked on Windows
            const int maxRetries = 5;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Preferences.Set(key, value, sharedName);
                    return;
                }
                catch (IOException ioEx) when (attempt < maxRetries)
                {
                    var delayMs = 100 * (int)Math.Pow(2, attempt - 1); // 100ms, 200ms, 400ms, 800ms, 1600ms
                    logger.Warning(ioEx, "Error writing to Preferences (likely file locked), retrying (attempt {Attempt}/{MaxRetries}) after {DelayMs}ms for key: {Key}",
                        attempt, maxRetries, delayMs, key);
                    Thread.Sleep(delayMs);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error writing to Preferences for key: {Key}", key);
            throw;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public void Remove(string key, string? sharedName = null)
    {
        preferencesLock.Wait();
        try
        {
            Preferences.Remove(key, sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error removing key from Preferences: {Key}", key);
            throw;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public void Clear(string? sharedName = null)
    {
        preferencesLock.Wait();
        try
        {
            Preferences.Clear(sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error clearing Preferences");
            throw;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public bool ContainsKey(string key, string? sharedName = null)
    {
        preferencesLock.Wait();
        try
        {
            return Preferences.ContainsKey(key, sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error checking key existence in Preferences: {Key}", key);
            return false;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public async Task<T?> GetAsync<T>(string key, T? defaultValue = default, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        await preferencesLock.WaitAsync(cancellationToken);
        try
        {
            return Preferences.Get(key, defaultValue, sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error reading from Preferences for key: {Key}", key);
            return defaultValue;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public async Task SetAsync<T>(string key, T? value, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        await preferencesLock.WaitAsync(cancellationToken);
        try
        {
            // Retry logic for Preferences.Set() which can throw IOException if file is locked on Windows
            const int maxRetries = 5;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Preferences.Set(key, value, sharedName);
                    return;
                }
                catch (IOException ioEx) when (attempt < maxRetries)
                {
                    var delayMs = 100 * (int)Math.Pow(2, attempt - 1); // 100ms, 200ms, 400ms, 800ms, 1600ms
                    logger.Warning(ioEx, "Error writing to Preferences (likely file locked), retrying (attempt {Attempt}/{MaxRetries}) after {DelayMs}ms for key: {Key}",
                        attempt, maxRetries, delayMs, key);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error writing to Preferences for key: {Key}", key);
            throw;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public async Task RemoveAsync(string key, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        await preferencesLock.WaitAsync(cancellationToken);
        try
        {
            Preferences.Remove(key, sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error removing key from Preferences: {Key}", key);
            throw;
        }
        finally
        {
            preferencesLock.Release();
        }
    }
}
