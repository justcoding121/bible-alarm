#nullable enable
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaCacheService : IDisposable
{
    /// <summary>
    /// Checks if a cached file exists for a lookup path in a schedule's cache folder.
    /// </summary>
    Task<bool> ExistsAsync(string lookUpPath, int scheduleId);
    
    /// <summary>
    /// Gets the cache file name from a lookup path (API query string).
    /// Uses lookup path instead of CDN URL so cache files are stable even when CDN URLs change.
    /// </summary>
    string GetCacheFileName(string lookUpPath);
    
    /// <summary>
    /// Gets the cache file path for a lookup path within a specific schedule's folder.
    /// </summary>
    string GetCacheFilePath(string lookUpPath, int scheduleId);

    Task<bool> SetupAlarmCacheAsync(int alarmScheduleId);
    Task CleanUpAsync();

    /// <summary>
    /// Resolves the playback URI for a track: returns the local cached file URI if available,
    /// otherwise returns the CDN URL for streaming. Returns null if not cached and no internet.
    /// </summary>
    Task<string?> ResolveTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default);
    
    Task DeleteScheduleCacheAsync(int scheduleId);
}

