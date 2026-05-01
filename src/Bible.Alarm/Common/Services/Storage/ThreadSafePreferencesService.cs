#nullable enable

using System;

using Bible.Alarm.Common.Helpers;
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
    private static readonly ILogger logger = Log.ForContext<ThreadSafePreferencesService>();

    private const string LogMessageErrorReadingPreferencesForKey = "Error reading from Preferences for key: {Key}";
    private const string LogMessageErrorWritingPreferencesRetry =
        "Error writing to Preferences (likely file locked), retrying (attempt {Attempt}/{MaxRetries}) after {DelayMs}ms for key: {Key}";

    // SemaphoreSlim for async/await support and thread-safe access
    // Initial count of 1 ensures only one operation at a time
    private static readonly SemaphoreSlim preferencesLock = new(1, 1);

    public string Get(string key, string defaultValue = "", string? sharedName = null)
    {
        preferencesLock.Wait();
        try
        {
            return Preferences.Get(key, defaultValue, sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
            return defaultValue;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public int Get(string key, int defaultValue = 0, string? sharedName = null)
    {
        preferencesLock.Wait();
        try
        {
            return Preferences.Get(key, defaultValue, sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
            return defaultValue;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public bool Get(string key, bool defaultValue = false, string? sharedName = null)
    {
        preferencesLock.Wait();
        try
        {
            return Preferences.Get(key, defaultValue, sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
            return defaultValue;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public double Get(string key, double defaultValue = 0.0, string? sharedName = null)
    {
        preferencesLock.Wait();
        try
        {
            return Preferences.Get(key, defaultValue, sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
            return defaultValue;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public float Get(string key, float defaultValue = 0f, string? sharedName = null)
    {
        preferencesLock.Wait();
        try
        {
            return Preferences.Get(key, defaultValue, sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
            return defaultValue;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public long Get(string key, long defaultValue = 0L, string? sharedName = null)
    {
        preferencesLock.Wait();
        try
        {
            return Preferences.Get(key, defaultValue, sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
            return defaultValue;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public DateTime Get(string key, DateTime defaultValue, string? sharedName = null)
    {
        preferencesLock.Wait();
        try
        {
            return Preferences.Get(key, defaultValue, sharedName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
            return defaultValue;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    public void Set(string key, string value, string? sharedName = null)
    {
        SetInternal(key, value, sharedName);
    }

    public void Set(string key, int value, string? sharedName = null)
    {
        SetInternal(key, value, sharedName);
    }

    public void Set(string key, bool value, string? sharedName = null)
    {
        SetInternal(key, value, sharedName);
    }

    public void Set(string key, double value, string? sharedName = null)
    {
        SetInternal(key, value, sharedName);
    }

    public void Set(string key, float value, string? sharedName = null)
    {
        SetInternal(key, value, sharedName);
    }

    public void Set(string key, long value, string? sharedName = null)
    {
        SetInternal(key, value, sharedName);
    }

    public void Set(string key, DateTime value, string? sharedName = null)
    {
        SetInternal(key, value, sharedName);
    }

    private static void SetInternal<T>(string key, T value, string? sharedName)
    {
        preferencesLock.Wait();
        try
        {
            const int maxRetries = 5;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    SetPreferencesByType(key, value, sharedName);
                    return;
                }
                catch (IOException ioEx) when (attempt < maxRetries)
                {
                    var delayMs = 100 * (int)Math.Pow(2, attempt - 1);
                    logger.Warning(ioEx, LogMessageErrorWritingPreferencesRetry,
                        attempt, maxRetries, delayMs, key);
                    Thread.Sleep(delayMs);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error writing to Preferences for key {key}.", ex);
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    private static void SetPreferencesByType<T>(string key, T value, string? sharedName)
    {
        switch (value)
        {
            case string s:
                Preferences.Set(key, s, sharedName);
                break;
            case int i:
                Preferences.Set(key, i, sharedName);
                break;
            case bool b:
                Preferences.Set(key, b, sharedName);
                break;
            case double d:
                Preferences.Set(key, d, sharedName);
                break;
            case float f:
                Preferences.Set(key, f, sharedName);
                break;
            case long l:
                Preferences.Set(key, l, sharedName);
                break;
            case DateTime dt:
                Preferences.Set(key, dt, sharedName);
                break;
            default:
                throw new NotSupportedException($"Preferences.Set does not support type {typeof(T).Name}");
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
            throw new InvalidOperationException($"Error removing key from Preferences: {key}", ex);
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
            throw new InvalidOperationException("Error clearing Preferences.", ex);
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

    public async Task<string> GetAsync(string key, string defaultValue = "", string? sharedName = null, CancellationToken cancellationToken = default)
    {
        return await ConcurrencyHelper.ExecuteAsync(preferencesLock, async () =>
        {
            try
            {
                return Preferences.Get(key, defaultValue, sharedName);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
                return defaultValue;
            }
        }, cancellationToken: cancellationToken) ?? defaultValue;
    }

    public async Task<int> GetAsync(string key, int defaultValue = 0, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        return await ConcurrencyHelper.ExecuteAsync(preferencesLock, async () =>
        {
            try
            {
                return Preferences.Get(key, defaultValue, sharedName);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
                return defaultValue;
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<bool> GetAsync(string key, bool defaultValue = false, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        return await ConcurrencyHelper.ExecuteAsync(preferencesLock, async () =>
        {
            try
            {
                return Preferences.Get(key, defaultValue, sharedName);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
                return defaultValue;
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<double> GetAsync(string key, double defaultValue = 0.0, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        return await ConcurrencyHelper.ExecuteAsync(preferencesLock, async () =>
        {
            try
            {
                return Preferences.Get(key, defaultValue, sharedName);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
                return defaultValue;
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<float> GetAsync(string key, float defaultValue = 0f, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        return await ConcurrencyHelper.ExecuteAsync(preferencesLock, async () =>
        {
            try
            {
                return Preferences.Get(key, defaultValue, sharedName);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
                return defaultValue;
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<long> GetAsync(string key, long defaultValue = 0L, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        return await ConcurrencyHelper.ExecuteAsync(preferencesLock, async () =>
        {
            try
            {
                return Preferences.Get(key, defaultValue, sharedName);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
                return defaultValue;
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<DateTime> GetAsync(string key, DateTime defaultValue, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        return await ConcurrencyHelper.ExecuteAsync(preferencesLock, async () =>
        {
            try
            {
                return Preferences.Get(key, defaultValue, sharedName);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, LogMessageErrorReadingPreferencesForKey, key);
                return defaultValue;
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task SetAsync(string key, string value, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        await SetAsyncInternal(key, value, sharedName, cancellationToken);
    }

    public async Task SetAsync(string key, int value, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        await SetAsyncInternal(key, value, sharedName, cancellationToken);
    }

    public async Task SetAsync(string key, bool value, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        await SetAsyncInternal(key, value, sharedName, cancellationToken);
    }

    public async Task SetAsync(string key, double value, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        await SetAsyncInternal(key, value, sharedName, cancellationToken);
    }

    public async Task SetAsync(string key, float value, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        await SetAsyncInternal(key, value, sharedName, cancellationToken);
    }

    public async Task SetAsync(string key, long value, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        await SetAsyncInternal(key, value, sharedName, cancellationToken);
    }

    public async Task SetAsync(string key, DateTime value, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        await SetAsyncInternal(key, value, sharedName, cancellationToken);
    }

    private static async Task SetAsyncInternal<T>(string key, T value, string? sharedName, CancellationToken cancellationToken)
    {
        await ConcurrencyHelper.ExecuteAsync(preferencesLock, async () =>
        {
            const int maxRetries = 5;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    SetPreferencesByType(key, value!, sharedName);
                    return;
                }
                catch (IOException ioEx) when (attempt < maxRetries)
                {
                    var delayMs = 100 * (int)Math.Pow(2, attempt - 1);
                    logger.Warning(ioEx, LogMessageErrorWritingPreferencesRetry,
                        attempt, maxRetries, delayMs, key);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task RemoveAsync(string key, string? sharedName = null, CancellationToken cancellationToken = default)
    {
        await ConcurrencyHelper.ExecuteAsync(preferencesLock, async () =>
        {
            try
            {
                Preferences.Remove(key, sharedName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error removing key from Preferences: {key}", ex);
            }
        }, cancellationToken: cancellationToken);
    }
}
