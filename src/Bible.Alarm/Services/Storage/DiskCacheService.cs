#nullable enable

using System.Text.Json;
using Bible.Alarm.Common.Interfaces.Storage;
using Bible.Alarm.Services.Storage.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Storage;

/// <summary>
/// Service for caching data to disk using Preferences storage with JSON serialization.
/// Provides a simple key-value cache that can wrap any factory function.
/// Uses IThreadSafePreferencesService for centralized, thread-safe Preferences access.
/// </summary>
public sealed class DiskCacheService : IDiskCacheService
{
    private const string CacheKeyPrefix = "DiskCache_";
    private readonly ILogger logger;
    private readonly IThreadSafePreferencesService preferencesService;
    private readonly JsonSerializerOptions jsonOptions;

    public DiskCacheService(ILogger logger, IThreadSafePreferencesService preferencesService)
    {
        this.logger = logger;
        this.preferencesService = preferencesService;
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

        try
        {
            if (preferencesService.ContainsKey(cacheKey))
            {
                var json = await preferencesService.GetAsync(cacheKey, "", null, cancellationToken);
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var cached = JsonSerializer.Deserialize<T>(json, jsonOptions);
                        if (!EqualityComparer<T>.Default.Equals(cached, default) && !IsDefaultValue(cached))
                        {
                            logger.Debug("Cache hit for key: {Key}", key);
                            return cached!;
                        }
                    }
                    catch (Exception deserializeEx)
                    {
                        // Deserialization failed - remove corrupted cache entry and call factory
                        logger.Warning(deserializeEx, "Deserialization failed for key: {Key}, removing corrupted cache entry and calling factory", key);
                        try
                        {
                            await preferencesService.RemoveAsync(cacheKey);
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
        logger.Debug("Cache miss for key: {Key}, executing factory", key);
        var value = await factory();

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

    /// <summary>
    /// Gets a cached value by key, or returns default if not found.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(key);

        try
        {
            if (!preferencesService.ContainsKey(cacheKey))
            {
                return default;
            }

            var json = await preferencesService.GetAsync(cacheKey, "", null, cancellationToken);
            if (string.IsNullOrEmpty(json))
            {
                return default;
            }

            var value = JsonSerializer.Deserialize<T>(json, jsonOptions);
            return value;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error deserializing cached value for key: {Key}", key);
            try
            {
                await preferencesService.RemoveAsync(cacheKey);
            }
            catch
            {
                // Ignore removal errors
            }
            return default;
        }
    }

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(key);

        try
        {
            var json = JsonSerializer.Serialize(value, jsonOptions);
            // IThreadSafePreferencesService handles thread-safety and retries internally
            await preferencesService.SetAsync(cacheKey, json, null, cancellationToken);
            logger.Debug("Cached value for key: {Key}", key);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error serializing and caching value for key {key}.", ex);
        }
    }

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    public void Remove(string key)
    {
        var cacheKey = GetCacheKey(key);

        try
        {
            if (preferencesService.ContainsKey(cacheKey))
            {
                preferencesService.Remove(cacheKey);
                logger.Debug("Removed cache entry for key: {Key}", key);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error removing cache entry for key: {Key}", key);
        }
    }

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
    public bool ContainsKey(string key)
    {
        var cacheKey = GetCacheKey(key);
        return preferencesService.ContainsKey(cacheKey);
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
        if (EqualityComparer<T>.Default.Equals(value, default))
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

