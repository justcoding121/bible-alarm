#nullable enable

namespace Bible.Alarm.Common.Interfaces.Storage;

/// <summary>
/// Thread-safe wrapper for MAUI Preferences API.
/// Centralizes all Preferences access to prevent file locking issues on Windows
/// when multiple threads access the preferences file concurrently.
/// </summary>
public interface IThreadSafePreferencesService
{
    /// <summary>
    /// Gets a value from Preferences, or returns the default if not found.
    /// </summary>
    T? Get<T>(string key, T? defaultValue = default, string? sharedName = null);

    /// <summary>
    /// Sets a value in Preferences.
    /// </summary>
    void Set<T>(string key, T? value, string? sharedName = null);

    /// <summary>
    /// Removes a key from Preferences.
    /// </summary>
    void Remove(string key, string? sharedName = null);

    /// <summary>
    /// Clears all Preferences (use with caution).
    /// </summary>
    void Clear(string? sharedName = null);

    /// <summary>
    /// Checks if a key exists in Preferences.
    /// </summary>
    bool ContainsKey(string key, string? sharedName = null);

    /// <summary>
    /// Gets a value from Preferences asynchronously, or returns the default if not found.
    /// </summary>
    Task<T?> GetAsync<T>(string key, T? defaultValue = default, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in Preferences asynchronously.
    /// </summary>
    Task SetAsync<T>(string key, T? value, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a key from Preferences asynchronously.
    /// </summary>
    Task RemoveAsync(string key, string? sharedName = null, CancellationToken cancellationToken = default);
}
