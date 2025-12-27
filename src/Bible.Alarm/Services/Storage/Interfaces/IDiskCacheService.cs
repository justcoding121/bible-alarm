#nullable enable

namespace Bible.Alarm.Services.Storage.Interfaces;

/// <summary>
/// Service for caching data to disk using Preferences storage with JSON serialization.
/// Provides a simple key-value cache that can wrap any factory function.
/// </summary>
public interface IDiskCacheService
{
    /// <summary>
    /// Gets a cached value by key, or executes the factory function and caches the result.
    /// </summary>
    /// <typeparam name="T">The type of value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">The factory function to execute if cache miss.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached or newly created value.</returns>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a cached value by key, or returns default if not found.
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached value, or default(T) if not found.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    /// <typeparam name="T">The type of value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    void Remove(string key);

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>True if the key exists, false otherwise.</returns>
    bool ContainsKey(string key);

    /// <summary>
    /// Clears all cached values.
    /// </summary>
    void Clear();
}

