#nullable enable
using System.Collections.Concurrent;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using System.Net.Http;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
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
    IAlarmScheduleService alarmScheduleService,
    ILanguageContentService languageContentService,
    IUrlConstructionService urlConstructionService)
    : IMediaCacheService, IDisposable
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    // Use StorageRoot instead of CacheRoot to ensure media cache is in a permanent location
    // that the OS won't delete. We manage the cache ourselves.
    private readonly string cacheRoot = Path.Combine(storageService.StorageRoot, AppConstants.FilePaths.MediaCacheDirectoryName);
    
    /// <summary>
    /// Gets the cache folder path for a specific schedule.
    /// </summary>
    private string GetScheduleCacheFolder(int scheduleId) => Path.Combine(cacheRoot, scheduleId.ToString());

    private static readonly ConcurrentDictionary<long, SemaphoreSlim> lockStore = new();
    
    /// <summary>
    /// Tracks in-progress downloads by cache key (scheduleId + lookupPath) to prevent duplicate downloads.
    /// When a download is in progress, subsequent requests for the same file will await the existing task.
    /// Uses lookup path instead of CDN URL so cache files are stable even when CDN URLs change.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Task<string?>> inProgressDownloads = new();

    /// <summary>
    /// Gets the cache file name from a lookup path (API query string).
    /// Uses lookup path instead of CDN URL so cache files are stable even when CDN URLs change.
    /// This enables offline playback by building lookup paths from schedule config.
    /// </summary>
    public string GetCacheFileName(string lookUpPath)
    {
        return MediaCacheFileNaming.GetCacheFileName(lookUpPath);
    }

    /// <summary>
    /// Gets the cache file path for a lookup path within a specific schedule's folder.
    /// </summary>
    public string GetCacheFilePath(string lookUpPath, int scheduleId) => 
        Path.Combine(GetScheduleCacheFolder(scheduleId), GetCacheFileName(lookUpPath));

    /// <summary>
    /// Checks if a cached file exists for a lookup path in a schedule's cache folder.
    /// </summary>
    public async Task<bool> ExistsAsync(string lookUpPath, int scheduleId)
    {
        var cachePath = GetCacheFilePath(lookUpPath, scheduleId);
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
                // Do NOT check internet upfront - ProcessPlaylistAsync skips cached files.
                // Only downloads when ExistsAsync returns false; DownloadAndCacheTrackAsync will fail if offline.

                var playlist = await mediaPlayService.NextTracks(alarmScheduleId);
                downloaded = await ProcessPlaylistAsync(playlist, alarmScheduleId);
            }
            catch (Exception e)
            {
                logger.Error(e, "An exception happened when downloading media files for caching.");
            }
        }, 500);

        return downloaded;
    }

    private async Task<bool> ProcessPlaylistAsync(List<PlayItem> playlist, int scheduleId)
    {
        var downloaded = false;

        foreach (var playItem in playlist)
        {
            // Use lookup path (stable) instead of CDN URL (dynamic) for cache filename
            var lookUpPath = playItem.Metadata.LookUpPath;
            
            // Check if file exists using lookup path
            if (await ExistsAsync(lookUpPath, scheduleId))
            {
                // File exists and lookup path matches - skip download
                logger.Debug("Skipping download - cached file exists for lookup path: {LookUpPath}, URL: {Url}", 
                    lookUpPath, playItem.Url);
                continue;
            }

            downloaded = true;

            if (!await networkStatusService.IsInternetAvailable())
            {
                logger.Warning("No internet - skipping download for track: LookUpPath={LookUpPath}", lookUpPath);
                continue;
            }

            // Download with individual error handling - don't let one failure stop others
            try
            {
                var cachedUrl = await DownloadAndCacheTrackWithRefetchOn404Async(playItem, scheduleId);
                if (cachedUrl == null)
                {
                    // Download failed - log but continue with next track
                    logger.Warning("Failed to download track: {Url} (lookup path: {LookUpPath}) for schedule {ScheduleId}. Continuing with next track.", 
                        playItem.Url, lookUpPath, scheduleId);
                }
            }
            catch (Exception ex)
            {
                // Individual download failure - log but continue processing other tracks
                logger.Error(ex, "Exception downloading track: {Url} (lookup path: {LookUpPath}) for schedule {ScheduleId}. Continuing with next track.", 
                    playItem.Url, lookUpPath, scheduleId);
            }
        }

        return downloaded;
    }

    public async Task<string?> ResolveTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var scheduleId = (int)playItem.Metadata.ScheduleId;
        if (scheduleId <= 0)
        {
            logger.Warning("Invalid schedule ID in PlayItem metadata: {ScheduleId}", scheduleId);
            return null;
        }

        var lookUpPath = playItem.Metadata.LookUpPath;

        // Check if the file is already cached locally
        if (await ExistsAsync(lookUpPath, scheduleId))
        {
            var cachedFilePath = GetCacheFilePath(lookUpPath, scheduleId);
            logger.Debug("Using cached file for track: LookUpPath={LookUpPath}, URL={Url}, Path={CachedPath}",
                lookUpPath, playItem.Url, cachedFilePath);
            // On iOS, MediaElement needs the file path directly instead of file:// URI
            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                return cachedFilePath;
            }
            return new Uri(cachedFilePath).AbsoluteUri;
        }

        // Not cached -- stream from CDN if internet is available
        if (!await networkStatusService.IsInternetAvailable())
        {
            logger.Warning("Track not cached and no internet. Cannot play track: LookUpPath={LookUpPath}, URL={Url}",
                lookUpPath, playItem.Url);
            return null;
        }

        logger.Information("Streaming track from CDN (not cached): LookUpPath={LookUpPath}, URL={Url}", lookUpPath, playItem.Url);
        return playItem.Url;
    }

    public async Task<bool> CacheTrackAsync(PlayItem playItem, int scheduleId, CancellationToken cancellationToken = default)
    {
        var lookUpPath = playItem.Metadata.LookUpPath;

        // Already cached -- nothing to do.
        if (await ExistsAsync(lookUpPath, scheduleId))
        {
            return true;
        }

        if (!await networkStatusService.IsInternetAvailable())
        {
            logger.Debug("No internet - skipping background cache for track: LookUpPath={LookUpPath}", lookUpPath);
            return false;
        }

        try
        {
            var result = await DownloadAndCacheTrackWithRefetchOn404Async(playItem, scheduleId, cancellationToken);
            return result != null;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Background cache failed for track: LookUpPath={LookUpPath}, URL={Url}", lookUpPath, playItem.Url);
            return false;
        }
    }

    /// <summary>
    /// True only when the exception indicates the server reported the resource as invalid (e.g. 404/410).
    /// Excludes network failures, timeouts, and I/O errors.
    /// </summary>
    private static bool IsInvalidFileServerError(Exception ex)
    {
        if (ex is not HttpRequestException)
        {
            return false;
        }
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("404", StringComparison.Ordinal) || msg.Contains("410", StringComparison.Ordinal);
    }

    private async Task<string?> RefetchSectionOrPubAndGetNewUrlAsync(TrackMetadata metadata, CancellationToken cancellationToken)
    {
        var pub = metadata.PublicationCode ?? string.Empty;
        var lang = metadata.LanguageCode ?? string.Empty;
        var section = metadata.SectionCode;
        var track = metadata.TrackCode ?? string.Empty;

        if (string.IsNullOrEmpty(pub) || string.IsNullOrEmpty(lang) || string.IsNullOrEmpty(track))
        {
            logger.Warning("Refetch skipped: missing metadata PublicationCode, LanguageCode, or TrackCode");
            return null;
        }

        bool ok;
        if (!string.IsNullOrWhiteSpace(section))
        {
            ok = await languageContentService.FetchSectionTracksAsync(pub, section.Trim(), lang, cancellationToken);
        }
        else
        {
            ok = await languageContentService.FetchPublicationTracksAsync(pub, lang, cancellationToken);
        }

        if (!ok)
        {
            logger.Warning("Refetch failed for pub={PublicationCode}, lang={LanguageCode}, section={SectionCode}", pub, lang, section ?? "(flat)");
            return null;
        }

        var urls = await urlConstructionService.ConstructTrackUrlsAsync(pub, lang, section, track);
        return urls.Count > 0 ? urls[0] : null;
    }

    private async Task<string?> DownloadAndCacheTrackWithRefetchOn404Async(PlayItem playItem, int scheduleId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await DownloadAndCacheTrackAsync(playItem, scheduleId, cancellationToken);
        }
        catch (HttpRequestException ex) when (IsInvalidFileServerError(ex))
        {
            logger.Information(ex, "CDN returned not found for track; refetching section/pub: LookUpPath={LookUpPath}", playItem.Metadata.LookUpPath);
            var newUrl = await RefetchSectionOrPubAndGetNewUrlAsync(playItem.Metadata, cancellationToken);
            if (string.IsNullOrEmpty(newUrl))
            {
                logger.Warning("Refetch did not yield a new URL; failing so UI can show error");
                throw;
            }
            playItem.Url = newUrl;
            return await DownloadAndCacheTrackAsync(playItem, scheduleId, cancellationToken);
        }
    }

    private async Task<string?> DownloadAndCacheTrackAsync(PlayItem playItem, int scheduleId, CancellationToken cancellationToken = default)
    {
        return await MediaCacheDownloadCoordinator.DownloadAndCacheTrackWithProgressAsync(
            logger,
            downloadService,
            storageService,
            urlRefreshService,
            mediaService,
            inProgressDownloads,
            GetScheduleCacheFolder,
            GetCacheFileName,
            playItem,
            scheduleId,
            null,
            cancellationToken);
    }


    public async Task CleanUpAsync()
    {
        var schedules = await alarmScheduleService.GetAllSchedulesAsync(
            false, false, cancellationTokenSource.Token);

        var filePathsToDelete = await MediaCacheCleanup.GetUnusedCacheFilesAsync(
            storageService,
            mediaPlayService,
            GetScheduleCacheFolder,
            GetCacheFileName,
            cancellationTokenSource.Token,
            schedules);
        await DeleteFilesAsync(filePathsToDelete);

        // Clean up orphaned folders (folders without corresponding schedules in database)
        await MediaCacheCleanup.CleanUpOrphanedFoldersAsync(
            logger,
            storageService,
            cacheRoot,
            schedules,
            DeleteFilesAsync);
    }

    private Task DeleteFilesAsync(HashSet<string> filePathsToDelete)
    {
        return MediaCacheCleanup.DeleteFilesAsync(
            logger,
            storageService,
            inProgressDownloads,
            GetCacheFileName,
            filePathsToDelete);
    }

    public async Task DeleteScheduleCacheAsync(int scheduleId)
    {
        await MediaCacheCleanup.DeleteScheduleCacheAsync(
            logger,
            storageService,
            mediaPlayService,
            alarmScheduleService,
            inProgressDownloads,
            GetScheduleCacheFolder,
            GetCacheFileName,
            cancellationTokenSource.Token,
            scheduleId);
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
