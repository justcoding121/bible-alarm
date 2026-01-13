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
    
    /// <summary>
    /// Gets the cache folder path for a specific schedule.
    /// </summary>
    private string GetScheduleCacheFolder(int scheduleId) => Path.Combine(cacheRoot, scheduleId.ToString());

    private static readonly ConcurrentDictionary<long, SemaphoreSlim> lockStore = new();

    public string GetCacheFileName(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be null or empty", nameof(url));
        }
        
        // Check if URL looks valid before attempting to parse
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            logger.Warning("Invalid URL format (missing scheme): {Url}", url);
            throw new UriFormatException($"Invalid URL format: {url}");
        }
        
        var uri = new Uri(url);

        var plainTextBytes = Encoding.UTF8.GetBytes(uri.PathAndQuery);
        var base64Name = Convert.ToBase64String(plainTextBytes);
        
        // Preserve the original file extension from the URL to ensure proper metadata extraction
        // TagLib and other libraries use file extension to determine the file format
        var extension = GetFileExtensionFromUrl(uri);
        
        return base64Name + extension;
    }
    
    private static string GetFileExtensionFromUrl(Uri uri)
    {
        // Get extension from the URL path (without query string)
        var path = uri.AbsolutePath;
        var extension = Path.GetExtension(path);
        
        // If we got a valid extension, use it; otherwise fall back to default
        if (!string.IsNullOrEmpty(extension) && 
            (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
             extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
             extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase) ||
             extension.Equals(".aac", StringComparison.OrdinalIgnoreCase)))
        {
            return extension.ToLowerInvariant();
        }
        
        // Default to mp3 for backwards compatibility
        return AppConstants.Media.MediaFileExtension;
    }

    public string GetCacheFilePath(string url) => Path.Combine(cacheRoot, GetCacheFileName(url));
    
    /// <summary>
    /// Gets the cache file path for a URL within a specific schedule's folder.
    /// </summary>
    private string GetCacheFilePath(string url, int scheduleId) => Path.Combine(GetScheduleCacheFolder(scheduleId), GetCacheFileName(url));

    public async Task<bool> ExistsAsync(string url)
    {
        var cachePath = Path.Combine(cacheRoot, GetCacheFileName(url));
        return await storageService.FileExists(cachePath);
    }
    
    /// <summary>
    /// Checks if a file exists in a schedule's cache folder.
    /// </summary>
    private async Task<bool> ExistsAsync(string url, int scheduleId)
    {
        var cachePath = GetCacheFilePath(url, scheduleId);
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
            if (await ExistsAsync(playItem.Url, scheduleId))
            {
                continue;
            }

            downloaded = true;

            var cachedUrl = await DownloadAndCacheTrackAsync(playItem, scheduleId);
            if (cachedUrl == null)
            {
                break;
            }
        }

        return downloaded;
    }

    public async Task<string?> GetOrDownloadTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default)
    {
        return await GetOrDownloadTrackUriWithProgressAsync(playItem, null, cancellationToken);
    }

    public async Task<string?> GetOrDownloadTrackUriWithProgressAsync(PlayItem playItem, Action<long, long?>? progressCallback, CancellationToken cancellationToken = default)
    {
        // Check for cancellation
        cancellationToken.ThrowIfCancellationRequested();
        
        // Get schedule ID from playItem metadata
        var scheduleId = (int)playItem.Metadata.ScheduleId;
        if (scheduleId <= 0)
        {
            logger.Warning("Invalid schedule ID in PlayItem metadata: {ScheduleId}", scheduleId);
            return null;
        }

        // Check if file exists in schedule's cache folder
        if (await ExistsAsync(playItem.Url, scheduleId))
        {
            var cachedFilePath = GetCacheFilePath(playItem.Url, scheduleId);
            logger.Debug("Using cached file for track: {Url}, Path: {CachedPath}", playItem.Url, cachedFilePath);
            // Report as complete for cached files
            progressCallback?.Invoke(1, 1);
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

        // Download and cache the file with progress reporting
        logger.Information("Downloading track (not in cache): {Url}", playItem.Url);
        var cachedUrl = await DownloadAndCacheTrackWithProgressAsync(playItem, scheduleId, progressCallback, cancellationToken);
        if (cachedUrl == null)
        {
            logger.Error("Failed to download and cache track: {Url}", playItem.Url);
            return null;
        }

        var downloadedFilePath = GetCacheFilePath(cachedUrl, scheduleId);
        logger.Information("Successfully downloaded and cached track: {Url}, Path: {CachedPath}", playItem.Url, downloadedFilePath);
        // On iOS, MediaElement may need the file path directly instead of file:// URI
        if (DeviceInfo.Platform == DevicePlatform.iOS)
        {
            return downloadedFilePath;
        }
        return new Uri(downloadedFilePath).AbsoluteUri;
    }

    private async Task<string?> DownloadAndCacheTrackAsync(PlayItem playItem, int scheduleId, CancellationToken cancellationToken = default)
    {
        return await DownloadAndCacheTrackWithProgressAsync(playItem, scheduleId, null, cancellationToken);
    }

    private async Task<string?> DownloadAndCacheTrackWithProgressAsync(PlayItem playItem, int scheduleId, Action<long, long?>? progressCallback, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check for cancellation before downloading
            cancellationToken.ThrowIfCancellationRequested();

            var bytes = await downloadService.DownloadWithProgressAsync(playItem.Url, progressCallback, cancellationToken);

            if (bytes != null && bytes.Length > 0)
            {
                var scheduleCacheFolder = GetScheduleCacheFolder(scheduleId);
                await storageService.SaveFile(scheduleCacheFolder, GetCacheFileName(playItem.Url), bytes);
                logger.Debug("Successfully downloaded and saved track: {Url}, Size: {Size} bytes, ScheduleId: {ScheduleId}", 
                    playItem.Url, bytes.Length, scheduleId);
                return playItem.Url;
            }

            logger.Warning("Download returned null or empty bytes for: {Url}, attempting URL refresh", playItem.Url);
            return await RefreshUrlAndRetryDownloadAsync(playItem, scheduleId, cancellationToken);
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
                return await RefreshUrlAndRetryDownloadAsync(playItem, scheduleId, cancellationToken);
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

    private async Task<string?> RefreshUrlAndRetryDownloadAsync(PlayItem playItem, int scheduleId, CancellationToken cancellationToken = default)
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

        var scheduleCacheFolder = GetScheduleCacheFolder(scheduleId);
        await storageService.SaveFile(scheduleCacheFolder, GetCacheFileName(refreshedUrl), bytes);
        logger.Warning("Downloaded using updated URL {RefreshedUrl} for {PlayItem}, ScheduleId: {ScheduleId}", refreshedUrl, playItem, scheduleId);
        return refreshedUrl;
    }


    public async Task CleanUpAsync()
    {
        var schedules = await alarmScheduleService.GetAllSchedulesAsync(
            false, false, cancellationTokenSource.Token);

        var filePathsToDelete = await GetUnusedCacheFilesAsync(schedules);
        await DeleteFilesAsync(filePathsToDelete);

        // Clean up orphaned folders (folders without corresponding schedules in database)
        await CleanUpOrphanedFoldersAsync(schedules);
    }

    private async Task<HashSet<string>> GetUnusedCacheFilesAsync(List<AlarmSchedule> schedules)
    {
        var filePathsToDelete = new HashSet<string>();
        var scheduleIdsToKeep = new HashSet<int>(schedules.Select(s => s.Id));
        
        // For each schedule, check for unused files in its folder
        foreach (var schedule in schedules)
        {
            var scheduleCacheFolder = GetScheduleCacheFolder(schedule.Id);
            
            // Check if folder exists
            if (!await storageService.DirectoryExists(scheduleCacheFolder))
            {
                continue;
            }
            
            var playlist = await mediaPlayService.NextTracks(schedule.Id);
            var allFiles = await storageService.GetAllFiles(scheduleCacheFolder);
            
            foreach (var filePath in allFiles)
            {
                var fileName = Path.GetFileName(filePath);
                bool shouldKeep = false;
                
                // Check if this file matches any URL in the current playlist
                foreach (var playItem in playlist)
                {
                    var expectedFileName = GetCacheFileName(playItem.Url);
                    if (fileName.Equals(expectedFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        shouldKeep = true;
                        break;
                    }
                }
                
                if (!shouldKeep)
                {
                    filePathsToDelete.Add(filePath);
                }
            }
        }
        
        return filePathsToDelete;
    }

    /// <summary>
    /// Cleans up orphaned cache folders that don't have corresponding schedules in the database.
    /// </summary>
    private async Task CleanUpOrphanedFoldersAsync(List<AlarmSchedule> schedules)
    {
        try
        {
            // Check if cache root directory exists
            if (!await storageService.DirectoryExists(cacheRoot))
            {
                return;
            }

            // Get all schedule IDs that exist in the database
            var validScheduleIds = new HashSet<int>(schedules.Select(s => s.Id));

            // Enumerate all directories in the cache root
            string[] allDirectories;
            try
            {
                allDirectories = Directory.GetDirectories(cacheRoot);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to enumerate directories in cache root: {CacheRoot}", cacheRoot);
                return;
            }

            var orphanedFolders = new List<string>();

            foreach (var directoryPath in allDirectories)
            {
                var directoryName = Path.GetFileName(directoryPath);
                
                // Try to parse directory name as schedule ID
                if (!int.TryParse(directoryName, out var scheduleId))
                {
                    // Directory name is not a valid schedule ID - could be orphaned or invalid
                    logger.Warning("Found directory with invalid schedule ID name: {DirectoryName} in {CacheRoot}", 
                        directoryName, cacheRoot);
                    orphanedFolders.Add(directoryPath);
                    continue;
                }

                // Check if this schedule ID exists in the database
                if (!validScheduleIds.Contains(scheduleId))
                {
                    // This folder belongs to a schedule that no longer exists
                    orphanedFolders.Add(directoryPath);
                    logger.Debug("Found orphaned cache folder for deleted schedule {ScheduleId}: {FolderPath}", 
                        scheduleId, directoryPath);
                }
            }

            // Delete orphaned folders
            if (orphanedFolders.Count > 0)
            {
                logger.Information("Cleaning up {Count} orphaned cache folder(s)", orphanedFolders.Count);
                
                foreach (var folderPath in orphanedFolders)
                {
                    try
                    {
                        // Delete all files in the folder first
                        var filesInFolder = await storageService.GetAllFiles(folderPath);
                        await DeleteFilesAsync(new HashSet<string>(filesInFolder));
                        
                        // Delete the folder itself
                        await storageService.DeleteDirectory(folderPath);
                        
                        logger.Information("Deleted orphaned cache folder: {FolderPath} ({FileCount} files)", 
                            folderPath, filesInFolder.Count);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Failed to delete orphaned cache folder: {FolderPath}", folderPath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error cleaning up orphaned cache folders");
        }
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
            var scheduleCacheFolder = GetScheduleCacheFolder(scheduleId);
            
            // Check if schedule still exists in database
            var scheduleExists = await alarmScheduleService.GetScheduleByIdAsync(
                scheduleId, false, false, cancellationTokenSource.Token) != null;
            
            if (!scheduleExists)
            {
                // Schedule was deleted - delete entire folder
                if (await storageService.DirectoryExists(scheduleCacheFolder))
                {
                    var filesToDelete = await storageService.GetAllFiles(scheduleCacheFolder);
                    await DeleteFilesAsync(new HashSet<string>(filesToDelete));
                    await storageService.DeleteDirectory(scheduleCacheFolder);
                    logger.Information("Deleted entire cache folder for deleted schedule {ScheduleId} ({Count} files)", 
                        scheduleId, filesToDelete.Count);
                }
                return;
            }
            
            // Schedule exists - update cache by deleting files that don't match new configuration
            // Get the new schedule's playlist to determine which files should be kept
            var newPlaylist = await mediaPlayService.NextTracks(scheduleId);
            
            // Get all files in the schedule's cache folder
            if (!await storageService.DirectoryExists(scheduleCacheFolder))
            {
                logger.Debug("Cache folder does not exist for schedule {ScheduleId}, nothing to clean up", scheduleId);
                return;
            }
            
            var allFiles = await storageService.GetAllFiles(scheduleCacheFolder);
            
            // Delete files that don't match the new schedule's URLs
            var filePathsToDelete = new HashSet<string>();
            foreach (var filePath in allFiles)
            {
                var fileName = Path.GetFileName(filePath);
                
                // Check all URLs to see if this file matches any of them
                bool shouldKeep = false;
                foreach (var playItem in newPlaylist)
                {
                    var expectedFileName = GetCacheFileName(playItem.Url);
                    if (fileName.Equals(expectedFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        shouldKeep = true;
                        break;
                    }
                }
                
                if (!shouldKeep)
                {
                    filePathsToDelete.Add(filePath);
                }
            }

            await DeleteFilesAsync(filePathsToDelete);
            logger.Information("Deleted {Count} cache files for schedule {ScheduleId} (kept {KeptCount} files)", 
                filePathsToDelete.Count, scheduleId, allFiles.Count - filePathsToDelete.Count);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error deleting cache files for schedule {ScheduleId}", scheduleId);
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
