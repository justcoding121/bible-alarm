#nullable enable

namespace Bible.Alarm.Common.Interfaces.Storage;

/// <summary>
/// Thread-safe wrapper for MAUI Preferences API.
/// Centralizes all Preferences access to prevent file locking issues on Windows
/// when multiple threads access the preferences file concurrently.
/// </summary>
public interface IThreadSafePreferencesService
{
    string Get(string key, string defaultValue = "", string? sharedName = null);

    int Get(string key, int defaultValue = 0, string? sharedName = null);

    bool Get(string key, bool defaultValue = false, string? sharedName = null);

    double Get(string key, double defaultValue = 0.0, string? sharedName = null);

    float Get(string key, float defaultValue = 0f, string? sharedName = null);

    long Get(string key, long defaultValue = 0L, string? sharedName = null);

    DateTime Get(string key, DateTime defaultValue, string? sharedName = null);

    void Set(string key, string value, string? sharedName = null);

    void Set(string key, int value, string? sharedName = null);

    void Set(string key, bool value, string? sharedName = null);

    void Set(string key, double value, string? sharedName = null);

    void Set(string key, float value, string? sharedName = null);

    void Set(string key, long value, string? sharedName = null);

    void Set(string key, DateTime value, string? sharedName = null);

    void Remove(string key, string? sharedName = null);

    /// <summary>
    /// Clears all Preferences (use with caution).
    /// </summary>
    void Clear(string? sharedName = null);

    bool ContainsKey(string key, string? sharedName = null);

    Task<string> GetAsync(string key, string defaultValue = "", string? sharedName = null, CancellationToken cancellationToken = default);

    Task<int> GetAsync(string key, int defaultValue = 0, string? sharedName = null, CancellationToken cancellationToken = default);

    Task<bool> GetAsync(string key, bool defaultValue = false, string? sharedName = null, CancellationToken cancellationToken = default);

    Task<double> GetAsync(string key, double defaultValue = 0.0, string? sharedName = null, CancellationToken cancellationToken = default);

    Task<float> GetAsync(string key, float defaultValue = 0f, string? sharedName = null, CancellationToken cancellationToken = default);

    Task<long> GetAsync(string key, long defaultValue = 0L, string? sharedName = null, CancellationToken cancellationToken = default);

    Task<DateTime> GetAsync(string key, DateTime defaultValue, string? sharedName = null, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string value, string? sharedName = null, CancellationToken cancellationToken = default);

    Task SetAsync(string key, int value, string? sharedName = null, CancellationToken cancellationToken = default);

    Task SetAsync(string key, bool value, string? sharedName = null, CancellationToken cancellationToken = default);

    Task SetAsync(string key, double value, string? sharedName = null, CancellationToken cancellationToken = default);

    Task SetAsync(string key, float value, string? sharedName = null, CancellationToken cancellationToken = default);

    Task SetAsync(string key, long value, string? sharedName = null, CancellationToken cancellationToken = default);

    Task SetAsync(string key, DateTime value, string? sharedName = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, string? sharedName = null, CancellationToken cancellationToken = default);
}
