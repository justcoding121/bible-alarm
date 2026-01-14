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
    Task<string?> GetOrDownloadTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets or downloads a track with progress reporting.
    /// </summary>
    /// <param name="playItem">The play item to download.</param>
    /// <param name="progressCallback">Called with (bytesDownloaded, totalBytes). totalBytes may be null if unknown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached file URI, or null if download failed.</returns>
    Task<string?> GetOrDownloadTrackUriWithProgressAsync(PlayItem playItem, Action<long, long?>? progressCallback, CancellationToken cancellationToken = default);
    
    Task DeleteScheduleCacheAsync(int scheduleId);
}

