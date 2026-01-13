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
    /// Gets a string value from Preferences, or returns the default if not found.
    /// </summary>
    string Get(string key, string defaultValue = "", string? sharedName = null);

    /// <summary>
    /// Gets an int value from Preferences, or returns the default if not found.
    /// </summary>
    int Get(string key, int defaultValue = 0, string? sharedName = null);

    /// <summary>
    /// Gets a bool value from Preferences, or returns the default if not found.
    /// </summary>
    bool Get(string key, bool defaultValue = false, string? sharedName = null);

    /// <summary>
    /// Gets a double value from Preferences, or returns the default if not found.
    /// </summary>
    double Get(string key, double defaultValue = 0.0, string? sharedName = null);

    /// <summary>
    /// Gets a float value from Preferences, or returns the default if not found.
    /// </summary>
    float Get(string key, float defaultValue = 0f, string? sharedName = null);

    /// <summary>
    /// Gets a long value from Preferences, or returns the default if not found.
    /// </summary>
    long Get(string key, long defaultValue = 0L, string? sharedName = null);

    /// <summary>
    /// Gets a DateTime value from Preferences, or returns the default if not found.
    /// </summary>
    DateTime Get(string key, DateTime defaultValue, string? sharedName = null);

    /// <summary>
    /// Sets a string value in Preferences.
    /// </summary>
    void Set(string key, string value, string? sharedName = null);

    /// <summary>
    /// Sets an int value in Preferences.
    /// </summary>
    void Set(string key, int value, string? sharedName = null);

    /// <summary>
    /// Sets a bool value in Preferences.
    /// </summary>
    void Set(string key, bool value, string? sharedName = null);

    /// <summary>
    /// Sets a double value in Preferences.
    /// </summary>
    void Set(string key, double value, string? sharedName = null);

    /// <summary>
    /// Sets a float value in Preferences.
    /// </summary>
    void Set(string key, float value, string? sharedName = null);

    /// <summary>
    /// Sets a long value in Preferences.
    /// </summary>
    void Set(string key, long value, string? sharedName = null);

    /// <summary>
    /// Sets a DateTime value in Preferences.
    /// </summary>
    void Set(string key, DateTime value, string? sharedName = null);

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
    /// Gets a string value from Preferences asynchronously, or returns the default if not found.
    /// </summary>
    Task<string> GetAsync(string key, string defaultValue = "", string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an int value from Preferences asynchronously, or returns the default if not found.
    /// </summary>
    Task<int> GetAsync(string key, int defaultValue = 0, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a bool value from Preferences asynchronously, or returns the default if not found.
    /// </summary>
    Task<bool> GetAsync(string key, bool defaultValue = false, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a double value from Preferences asynchronously, or returns the default if not found.
    /// </summary>
    Task<double> GetAsync(string key, double defaultValue = 0.0, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a float value from Preferences asynchronously, or returns the default if not found.
    /// </summary>
    Task<float> GetAsync(string key, float defaultValue = 0f, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a long value from Preferences asynchronously, or returns the default if not found.
    /// </summary>
    Task<long> GetAsync(string key, long defaultValue = 0L, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a DateTime value from Preferences asynchronously, or returns the default if not found.
    /// </summary>
    Task<DateTime> GetAsync(string key, DateTime defaultValue, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a string value in Preferences asynchronously.
    /// </summary>
    Task SetAsync(string key, string value, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets an int value in Preferences asynchronously.
    /// </summary>
    Task SetAsync(string key, int value, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a bool value in Preferences asynchronously.
    /// </summary>
    Task SetAsync(string key, bool value, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a double value in Preferences asynchronously.
    /// </summary>
    Task SetAsync(string key, double value, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a float value in Preferences asynchronously.
    /// </summary>
    Task SetAsync(string key, float value, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a long value in Preferences asynchronously.
    /// </summary>
    Task SetAsync(string key, long value, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a DateTime value in Preferences asynchronously.
    /// </summary>
    Task SetAsync(string key, DateTime value, string? sharedName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a key from Preferences asynchronously.
    /// </summary>
    Task RemoveAsync(string key, string? sharedName = null, CancellationToken cancellationToken = default);
}
