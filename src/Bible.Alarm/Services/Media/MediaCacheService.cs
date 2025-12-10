#nullable enable
using System.Collections.Concurrent;
using System.Text;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Essentials;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class MediaCacheService(
    ILogger logger,
    IStorageService storageService,
    IDownloadService downloadService,
    IPlaylistService mediaPlayService,
    IServiceScopeFactory scopeFactory,
    IMediaService mediaService,
    INetworkStatusService networkStatusService,
    IMediaUrlRefreshService urlRefreshService,
    IAlarmScheduleService alarmScheduleService)
    : IMediaCacheService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IMediaUrlRefreshService _urlRefreshService = urlRefreshService;
    private readonly IAlarmScheduleService _alarmScheduleService = alarmScheduleService;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isDisposed;

    // Use StorageRoot instead of CacheRoot to ensure media cache is in a permanent location
    // that the OS won't delete. We manage the cache ourselves.
    private readonly string _cacheRoot = Path.Combine(storageService.StorageRoot, AppConstants.FilePaths.MediaCacheDirectoryName);

    private static readonly ConcurrentDictionary<long, SemaphoreSlim> LockStore = new();

    public string GetCacheFileName(string url)
    {
        var uri = new Uri(url);

        var plainTextBytes = Encoding.UTF8.GetBytes(uri.PathAndQuery);
        return Convert.ToBase64String(plainTextBytes) + AppConstants.Media.MediaFileExtension;
    }

    public string GetCacheFilePath(string url)
    {
        return Path.Combine(_cacheRoot, GetCacheFileName(url));
    }

    public async Task<bool> ExistsAsync(string url)
    {
        var cachePath = Path.Combine(_cacheRoot, GetCacheFileName(url));
        return await storageService.FileExists(cachePath);
    }

    public async Task<bool> SetupAlarmCacheAsync(int alarmScheduleId)
    {
        var downloaded = false;

        var @lock = LockStore.GetOrAdd(alarmScheduleId, new SemaphoreSlim(1));

        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            try
            {
                if (!await networkStatusService.IsInternetAvailable()) return;

                var playlist = await mediaPlayService.NextTracks(alarmScheduleId);
                downloaded = await ProcessPlaylistAsync(playlist);
            }
            catch (Exception e)
            {
                _logger.Error(e, "An exception happened when downloading media files for caching.");
            }
        }, 500);

        return downloaded;
    }

    private async Task<bool> ProcessPlaylistAsync(List<PlayItem> playlist)
    {
        var downloaded = false;

        foreach (var playItem in playlist)
        {
            if (await ExistsAsync(playItem.Url)) continue;

            downloaded = true;

            var cachedUrl = await DownloadAndCacheTrackAsync(playItem);
            if (cachedUrl == null) break;
        }

        return downloaded;
    }

    public async Task<string?> GetOrDownloadTrackUriAsync(PlayItem playItem)
    {
        // Check if file exists in cache
        if (await ExistsAsync(playItem.Url))
        {
            var cachedFilePath = GetCacheFilePath(playItem.Url);
            _logger.Debug("Using cached file for track: {Url}, Path: {CachedPath}", playItem.Url, cachedFilePath);
            // On iOS, MediaElement needs the file path directly instead of file:// URI
            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                return cachedFilePath;
            }
            return new Uri(cachedFilePath).AbsoluteUri;
        }

        // Check internet connectivity before attempting download
        if (!await networkStatusService.IsInternetAvailable())
        {
            _logger.Warning("No internet connection available. Cannot download track: {Url}", playItem.Url);
            return null;
        }

        // Download and cache the file
        _logger.Information("Downloading track (not in cache): {Url}", playItem.Url);
        var cachedUrl = await DownloadAndCacheTrackAsync(playItem);
        if (cachedUrl == null)
        {
            _logger.Error("Failed to download and cache track: {Url}", playItem.Url);
            return null;
        }

        var downloadedFilePath = GetCacheFilePath(cachedUrl);
        _logger.Information("Successfully downloaded and cached track: {Url}, Path: {CachedPath}", playItem.Url, downloadedFilePath);
        // On iOS, MediaElement may need the file path directly instead of file:// URI
        if (DeviceInfo.Platform == DevicePlatform.iOS)
        {
            return downloadedFilePath;
        }
        return new Uri(downloadedFilePath).AbsoluteUri;
    }

    private async Task<string?> DownloadAndCacheTrackAsync(PlayItem playItem)
    {
        try
        {
            var bytes = await downloadService.DownloadAsync(playItem.Url);

            if (bytes != null && bytes.Length > 0)
            {
                await storageService.SaveFile(_cacheRoot, GetCacheFileName(playItem.Url), bytes);
                _logger.Debug("Successfully downloaded and saved track: {Url}, Size: {Size} bytes", playItem.Url, bytes.Length);
                return playItem.Url;
            }

            _logger.Warning("Download returned null or empty bytes for: {Url}, attempting URL refresh", playItem.Url);
            return await RefreshUrlAndRetryDownloadAsync(playItem);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception while downloading track: {Url}", playItem.Url);
            
            // Try refreshing URL and retrying
            try
            {
                return await RefreshUrlAndRetryDownloadAsync(playItem);
            }
            catch (Exception refreshEx)
            {
                _logger.Error(refreshEx, "Exception while refreshing URL for track: {Url}", playItem.Url);
                return null;
            }
        }
    }

    private async Task<string?> RefreshUrlAndRetryDownloadAsync(PlayItem playItem)
    {
        var trackMetadata = playItem.Metadata;
        var refreshedUrl = await _urlRefreshService.RefreshUrlAsync(trackMetadata);

        if (refreshedUrl == null || refreshedUrl == playItem.Url)
            return null;

        await mediaService.UpdateTrackUrlAsync(trackMetadata, refreshedUrl);
        _logger.Warning($"Refreshed URL from {playItem.Url} to {refreshedUrl} for {playItem}");

        var bytes = await downloadService.DownloadAsync(refreshedUrl);
        if (bytes == null) return null;

        await storageService.SaveFile(_cacheRoot, GetCacheFileName(refreshedUrl), bytes);
        _logger.Warning($"Downloaded using updated URL {refreshedUrl} for {playItem}");
        return refreshedUrl;
    }


    public async Task CleanUpAsync()
    {
        var schedules = await _alarmScheduleService.GetAllSchedulesAsync(
            false, false, _cancellationTokenSource.Token);

        var filePathsToDelete = await GetUnusedCacheFilesAsync(schedules);
        await DeleteFilesAsync(filePathsToDelete);
    }

    private async Task<HashSet<string>> GetUnusedCacheFilesAsync(List<AlarmSchedule> schedules)
    {
        var filePathsToDelete = new HashSet<string>(await storageService.GetAllFiles(_cacheRoot));

        foreach (var schedule in schedules)
        {
            var playlist = await mediaPlayService.NextTracks(schedule.Id);
            var filePaths = playlist.Select(x => GetCacheFilePath(x.Url)).ToList();

            foreach (var filePath in filePaths)
            {
                filePathsToDelete.Remove(filePath);
            }
        }

        return filePathsToDelete;
    }

    private Task DeleteFilesAsync(HashSet<string> filePathsToDelete)
    {
        foreach (var filePath in filePathsToDelete)
        {
            try
            {
                storageService.DeleteFile(filePath);
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to delete file: {filePath}");
            }
        }

        return Task.CompletedTask;
    }

    public async Task DeleteScheduleCacheAsync(int scheduleId)
    {
        try
        {
            var playlist = await mediaPlayService.NextTracks(scheduleId);
            var filePathsToDelete = new HashSet<string>();

            foreach (var playItem in playlist)
            {
                var cacheFilePath = GetCacheFilePath(playItem.Url);
                if (await storageService.FileExists(cacheFilePath))
                {
                    filePathsToDelete.Add(cacheFilePath);
                }
            }

            await DeleteFilesAsync(filePathsToDelete);
            _logger.Information($"Deleted {filePathsToDelete.Count} cache files for schedule {scheduleId}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error deleting cache files for schedule {scheduleId}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // Cancel and dispose cancellation token source
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            _logger.Warning(ex, "Error during cancellation token source disposal");
        }
        
        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // storageService, downloadService, mediaPlayService, networkStatusService, 
        // mediaService, urlRefreshService, and IServiceScopeFactory are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}