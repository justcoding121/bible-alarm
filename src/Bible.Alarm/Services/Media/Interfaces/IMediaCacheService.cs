#nullable enable
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaCacheService : IDisposable
{
    Task<bool> ExistsAsync(string url);
    string GetCacheFileName(string url);
    string GetCacheFilePath(string url);

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

