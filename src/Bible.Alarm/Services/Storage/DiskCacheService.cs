#nullable enable

using System.Text.Json;
using Bible.Alarm.Services.Storage.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Storage;

/// <summary>
/// Service for caching data to disk using Preferences storage with JSON serialization.
/// Provides a simple key-value cache that can wrap any factory function.
/// </summary>
public sealed class DiskCacheService : IDiskCacheService
{
    private const string CacheKeyPrefix = "DiskCache_";
    private readonly ILogger logger;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly SemaphoreSlim preferencesLock = new(1, 1);

    public DiskCacheService(ILogger logger)
    {
        this.logger = logger;
        jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Gets a cached value by key, or executes the factory function and caches the result.
    /// If deserialization fails, the factory will be called to regenerate the value.
    /// </summary>
    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(key);
        bool cacheHit = false;

        try
        {
            // Try to get from cache first
            if (Preferences.ContainsKey(cacheKey))
            {
                var json = Preferences.Get(cacheKey, (string?)null);
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var cached = JsonSerializer.Deserialize<T>(json, jsonOptions);
                        if (cached != null && !IsDefaultValue(cached))
                        {
                            logger.Debug("Cache hit for key: {Key}", key);
                            cacheHit = true;
                            return cached;
                        }
                    }
                    catch (Exception deserializeEx)
                    {
                        // Deserialization failed - remove corrupted cache entry and call factory
                        logger.Warning(deserializeEx, "Deserialization failed for key: {Key}, removing corrupted cache entry and calling factory", key);
                        try
                        {
                            Preferences.Remove(cacheKey);
                        }
                        catch
                        {
                            // Ignore removal errors
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error reading from cache for key: {Key}, will execute factory", key);
        }

        // Cache miss, deserialization failure, or error - execute factory
        if (!cacheHit)
        {
            logger.Debug("Cache miss for key: {Key}, executing factory", key);
            var value = await factory();

            // Cache the result
            try
            {
                await SetAsync(key, value, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error caching value for key: {Key}, value will not be cached", key);
            }

            return value;
        }

        // This should never be reached, but compiler needs it
        throw new InvalidOperationException("Unexpected state in GetOrSetAsync");
    }

    /// <summary>
    /// Gets a cached value by key, or returns default if not found.
    /// </summary>
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(key);

        try
        {
            if (!Preferences.ContainsKey(cacheKey))
            {
                return Task.FromResult<T?>(default);
            }

            var json = Preferences.Get(cacheKey, (string?)null);
            if (string.IsNullOrEmpty(json))
            {
                return Task.FromResult<T?>(default);
            }

            var value = JsonSerializer.Deserialize<T>(json, jsonOptions);
            return Task.FromResult(value);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error deserializing cached value for key: {Key}", key);
            // Remove corrupted cache entry
            try
            {
                Preferences.Remove(cacheKey);
            }
            catch
            {
                // Ignore removal errors
            }
            return Task.FromResult<T?>(default);
        }
    }

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(key);

        // Serialize access to Preferences to prevent file locking issues
        await preferencesLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(value, jsonOptions);

            // Retry logic for Preferences.Set() which can throw IOException if file is locked
            const int maxRetries = 5;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Preferences.Set(cacheKey, json);
                    logger.Debug("Cached value for key: {Key}", key);
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
            logger.Error(ex, "Error serializing and caching value for key: {Key}", key);
            throw;
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    public void Remove(string key)
    {
        var cacheKey = GetCacheKey(key);

        // Serialize access to Preferences to prevent file locking issues
        preferencesLock.Wait();
        try
        {
            if (Preferences.ContainsKey(cacheKey))
            {
                Preferences.Remove(cacheKey);
                logger.Debug("Removed cache entry for key: {Key}", key);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error removing cache entry for key: {Key}", key);
        }
        finally
        {
            preferencesLock.Release();
        }
    }

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
    public bool ContainsKey(string key)
    {
        var cacheKey = GetCacheKey(key);
        return Preferences.ContainsKey(cacheKey);
    }

    /// <summary>
    /// Clears all cached values.
    /// Note: This removes all preferences with the cache prefix, which may include other cache entries.
    /// </summary>
    public void Clear()
    {
        try
        {
            // Preferences API doesn't provide a way to enumerate all keys,
            // so we can't selectively clear only cache entries.
            // This is a limitation of the Preferences API.
            logger.Warning("Clear() is not fully supported - Preferences API doesn't allow enumerating keys. Consider using Remove() for specific keys.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error clearing cache");
        }
    }

    private static string GetCacheKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or whitespace", nameof(key));
        }

        return $"{CacheKeyPrefix}{key}";
    }

    private static bool IsDefaultValue<T>(T value)
    {
        if (value == null)
        {
            return true;
        }

        // For value types, check if it's the default value
        if (typeof(T).IsValueType)
        {
            return EqualityComparer<T>.Default.Equals(value, default(T)!);
        }

        return false;
    }
}

