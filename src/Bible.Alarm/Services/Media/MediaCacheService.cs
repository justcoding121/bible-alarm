#nullable enable
using System.Collections.Concurrent;
using System.Text;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class MediaCacheService(
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
    private readonly IServiceScopeFactory scopeFactory = scopeFactory;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    // Use StorageRoot instead of CacheRoot to ensure media cache is in a permanent location
    // that the OS won't delete. We manage the cache ourselves.
    private readonly string cacheRoot = Path.Combine(storageService.StorageRoot, AppConstants.FilePaths.MediaCacheDirectoryName);

    private static readonly ConcurrentDictionary<long, SemaphoreSlim> lockStore = new();

    public string GetCacheFileName(string url)
    {
        var uri = new Uri(url);

        var plainTextBytes = Encoding.UTF8.GetBytes(uri.PathAndQuery);
        return Convert.ToBase64String(plainTextBytes) + AppConstants.Media.MediaFileExtension;
    }

    public string GetCacheFilePath(string url) => Path.Combine(cacheRoot, GetCacheFileName(url));

    public async Task<bool> ExistsAsync(string url)
    {
        var cachePath = Path.Combine(cacheRoot, GetCacheFileName(url));
        return await storageService.FileExists(cachePath);
    }

    public async Task<bool> SetupAlarmCacheAsync(int alarmScheduleId)
    {
        if (alarmScheduleId <= 0)
        {
            logger.Warning("Skipping cache setup for invalid schedule ID: {ScheduleId}", alarmScheduleId);
            return false;
        }

        var downloaded = false;

        var @lock = lockStore.GetOrAdd(alarmScheduleId, new SemaphoreSlim(1));

        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            try
            {
                if (!await networkStatusService.IsInternetAvailable())
                {
                    return;
                }

                var playlist = await mediaPlayService.NextTracks(alarmScheduleId);
                downloaded = await ProcessPlaylistAsync(playlist);
            }
            catch (Exception e)
            {
                logger.Error(e, "An exception happened when downloading media files for caching.");
            }
        }, 500);

        return downloaded;
    }

    private async Task<bool> ProcessPlaylistAsync(List<PlayItem> playlist)
    {
        var downloaded = false;

        foreach (var playItem in playlist)
        {
            if (await ExistsAsync(playItem.Url))
            {
                continue;
            }

            downloaded = true;

            var cachedUrl = await DownloadAndCacheTrackAsync(playItem);
            if (cachedUrl == null)
            {
                break;
            }
        }

        return downloaded;
    }

    public async Task<string?> GetOrDownloadTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default)
    {
        // Check for cancellation
        cancellationToken.ThrowIfCancellationRequested();

        // Check if file exists in cache
        if (await ExistsAsync(playItem.Url))
        {
            var cachedFilePath = GetCacheFilePath(playItem.Url);
            logger.Debug("Using cached file for track: {Url}, Path: {CachedPath}", playItem.Url, cachedFilePath);
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
            logger.Warning("No internet connection available. Cannot download track: {Url}", playItem.Url);
            return null;
        }

        // Download and cache the file
        logger.Information("Downloading track (not in cache): {Url}", playItem.Url);
        var cachedUrl = await DownloadAndCacheTrackAsync(playItem, cancellationToken);
        if (cachedUrl == null)
        {
            logger.Error("Failed to download and cache track: {Url}", playItem.Url);
            return null;
        }

        var downloadedFilePath = GetCacheFilePath(cachedUrl);
        logger.Information("Successfully downloaded and cached track: {Url}, Path: {CachedPath}", playItem.Url, downloadedFilePath);
        // On iOS, MediaElement may need the file path directly instead of file:// URI
        if (DeviceInfo.Platform == DevicePlatform.iOS)
        {
            return downloadedFilePath;
        }
        return new Uri(downloadedFilePath).AbsoluteUri;
    }

    private async Task<string?> DownloadAndCacheTrackAsync(PlayItem playItem, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check for cancellation before downloading
            cancellationToken.ThrowIfCancellationRequested();

            var bytes = await downloadService.DownloadAsync(playItem.Url, cancellationToken: cancellationToken);

            if (bytes != null && bytes.Length > 0)
            {
                await storageService.SaveFile(cacheRoot, GetCacheFileName(playItem.Url), bytes);
                logger.Debug("Successfully downloaded and saved track: {Url}, Size: {Size} bytes", playItem.Url, bytes.Length);
                return playItem.Url;
            }

            logger.Warning("Download returned null or empty bytes for: {Url}, attempting URL refresh", playItem.Url);
            return await RefreshUrlAndRetryDownloadAsync(playItem, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.Information("Download cancelled for track: {Url}", playItem.Url);
            throw;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Exception while downloading track: {Url}", playItem.Url);

            // Try refreshing URL and retrying
            try
            {
                return await RefreshUrlAndRetryDownloadAsync(playItem, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                logger.Information("URL refresh cancelled for track: {Url}", playItem.Url);
                throw;
            }
            catch (Exception refreshEx)
            {
                logger.Error(refreshEx, "Exception while refreshing URL for track: {Url}", playItem.Url);
                return null;
            }
        }
    }

    private async Task<string?> RefreshUrlAndRetryDownloadAsync(PlayItem playItem, CancellationToken cancellationToken = default)
    {
        // Check for cancellation
        cancellationToken.ThrowIfCancellationRequested();

        var trackMetadata = playItem.Metadata;
        var refreshedUrl = await urlRefreshService.RefreshUrlAsync(trackMetadata);

        if (refreshedUrl == null || refreshedUrl == playItem.Url)
        {
            return null;
        }

        await mediaService.UpdateTrackUrlAsync(trackMetadata, refreshedUrl);
        logger.Warning($"Refreshed URL from {playItem.Url} to {refreshedUrl} for {playItem}");

        var bytes = await downloadService.DownloadAsync(refreshedUrl, cancellationToken: cancellationToken);
        if (bytes == null)
        {
            return null;
        }

        await storageService.SaveFile(cacheRoot, GetCacheFileName(refreshedUrl), bytes);
        logger.Warning($"Downloaded using updated URL {refreshedUrl} for {playItem}");
        return refreshedUrl;
    }


    public async Task CleanUpAsync()
    {
        var schedules = await alarmScheduleService.GetAllSchedulesAsync(
            false, false, cancellationTokenSource.Token);

        var filePathsToDelete = await GetUnusedCacheFilesAsync(schedules);
        await DeleteFilesAsync(filePathsToDelete);
    }

    private async Task<HashSet<string>> GetUnusedCacheFilesAsync(List<AlarmSchedule> schedules)
    {
        var filePathsToDelete = new HashSet<string>(await storageService.GetAllFiles(cacheRoot));

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
                logger.Error(e, $"Failed to delete file: {filePath}");
            }
        }

        return Task.CompletedTask;
    }

    public async Task DeleteScheduleCacheAsync(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            logger.Warning("Skipping cache deletion for invalid schedule ID: {ScheduleId}", scheduleId);
            return;
        }

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
            logger.Information($"Deleted {filePathsToDelete.Count} cache files for schedule {scheduleId}");
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Error deleting cache files for schedule {scheduleId}");
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, "Error during cancellation token source disposal");
        }

        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // storageService, downloadService, mediaPlayService, networkStatusService, 
        // mediaService, urlRefreshService, and IServiceScopeFactory are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}
