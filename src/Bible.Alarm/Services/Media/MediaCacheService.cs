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
        if (string.IsNullOrWhiteSpace(lookUpPath))
        {
            throw new ArgumentException("LookUpPath cannot be null or empty", nameof(lookUpPath));
        }
        
        // Hash the lookup path to create a safe filename
        var plainTextBytes = Encoding.UTF8.GetBytes(lookUpPath);
        var base64Name = Convert.ToBase64String(plainTextBytes)
            .Replace('/', '_')  // Replace / with _ for filesystem safety
            .Replace('+', '-')  // Replace + with - for filesystem safety
            .Replace("=", "");  // Remove padding
        
        // Determine file extension from lookup path
        var extension = GetFileExtensionFromLookUpPath(lookUpPath);
        
        return base64Name + extension;
    }
    
    /// <summary>
    /// Gets the file extension from a lookup path based on fileformat parameter or default to .mp3
    /// </summary>
    private static string GetFileExtensionFromLookUpPath(string lookUpPath)
    {
        // Check for fileformat parameter in lookup path
        if (lookUpPath.Contains("fileformat=MP4", StringComparison.OrdinalIgnoreCase))
        {
            return ".mp4";
        }
        if (lookUpPath.Contains("fileformat=MP3", StringComparison.OrdinalIgnoreCase))
        {
            return ".mp3";
        }
        if (lookUpPath.Contains("fileformat=M4A", StringComparison.OrdinalIgnoreCase))
        {
            return ".m4a";
        }
        if (lookUpPath.Contains("fileformat=AAC", StringComparison.OrdinalIgnoreCase))
        {
            return ".aac";
        }
        
        // Default to mp3 for backwards compatibility
        return AppConstants.Media.MediaFileExtension;
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

            // Download with individual error handling - don't let one failure stop others
            try
            {
                var cachedUrl = await DownloadAndCacheTrackAsync(playItem, scheduleId);
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

        // Use lookup path (stable) instead of CDN URL (dynamic) for cache filename
        var lookUpPath = playItem.Metadata.LookUpPath;

        // Check if file exists in schedule's cache folder using lookup path
        if (await ExistsAsync(lookUpPath, scheduleId))
        {
            var cachedFilePath = GetCacheFilePath(lookUpPath, scheduleId);
            logger.Debug("Using cached file for track: LookUpPath={LookUpPath}, URL={Url}, Path={CachedPath}", 
                lookUpPath, playItem.Url, cachedFilePath);
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
            logger.Warning("No internet connection available. Cannot download track: LookUpPath={LookUpPath}, URL={Url}", 
                lookUpPath, playItem.Url);
            return null;
        }

        // Download and cache the file with progress reporting
        logger.Information("Downloading track (not in cache): LookUpPath={LookUpPath}, URL={Url}", lookUpPath, playItem.Url);
        var cachedUrl = await DownloadAndCacheTrackWithProgressAsync(playItem, scheduleId, progressCallback, cancellationToken);
        if (cachedUrl == null)
        {
            logger.Error("Failed to download and cache track: LookUpPath={LookUpPath}, URL={Url}", lookUpPath, playItem.Url);
            return null;
        }

        var downloadedFilePath = GetCacheFilePath(lookUpPath, scheduleId);
        logger.Information("Successfully downloaded and cached track: LookUpPath={LookUpPath}, URL={Url}, Path={CachedPath}", 
            lookUpPath, playItem.Url, downloadedFilePath);
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
        // Use lookup path (stable) instead of CDN URL (dynamic) for cache key
        var lookUpPath = playItem.Metadata.LookUpPath;
        // Create a unique key for this download (scheduleId + lookupPath)
        var downloadKey = $"{scheduleId}:{lookUpPath}";
        
        // Check if there's already a download in progress for this file
        if (inProgressDownloads.TryGetValue(downloadKey, out var existingTask))
        {
            logger.Debug("Download already in progress for LookUpPath={LookUpPath}, URL={Url}, ScheduleId={ScheduleId}. Waiting for existing download to complete.", 
                lookUpPath, playItem.Url, scheduleId);
            try
            {
                // Wait for the existing download to complete and return its result
                return await existingTask;
            }
            catch (OperationCanceledException)
            {
                // The existing download was cancelled, let the caller handle it
                throw;
            }
            catch (Exception ex)
            {
                // The existing download failed, log but don't rethrow since caller may want to try again
                logger.Warning(ex, "Existing download failed for LookUpPath={LookUpPath}, URL={Url}, ScheduleId={ScheduleId}", 
                    lookUpPath, playItem.Url, scheduleId);
                return null;
            }
        }
        
        // Create the download task
        var downloadTaskSource = new TaskCompletionSource<string?>();
        var downloadTask = downloadTaskSource.Task;
        
        // Try to add our task to the dictionary - if another thread beat us, use their task
        if (!inProgressDownloads.TryAdd(downloadKey, downloadTask))
        {
            // Another thread just started the download, wait for their result
            if (inProgressDownloads.TryGetValue(downloadKey, out existingTask))
            {
                logger.Debug("Another thread started download for LookUpPath={LookUpPath}, URL={Url}, ScheduleId={ScheduleId}. Waiting for that download.", 
                    lookUpPath, playItem.Url, scheduleId);
                try
                {
                    return await existingTask;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Concurrent download failed for LookUpPath={LookUpPath}, URL={Url}, ScheduleId={ScheduleId}", 
                        lookUpPath, playItem.Url, scheduleId);
                    return null;
                }
            }
        }
        
        try
        {
            // Check for cancellation before downloading
            cancellationToken.ThrowIfCancellationRequested();

            var bytes = await downloadService.DownloadWithProgressAsync(playItem.Url, progressCallback, cancellationToken);

            if (bytes != null && bytes.Length > 0)
            {
                // Use lookup path (stable) instead of CDN URL (dynamic) for cache filename
                var scheduleCacheFolder = GetScheduleCacheFolder(scheduleId);
                await storageService.SaveFile(scheduleCacheFolder, GetCacheFileName(lookUpPath), bytes);
                logger.Debug("Successfully downloaded and saved track: LookUpPath={LookUpPath}, URL={Url}, Size={Size} bytes, ScheduleId={ScheduleId}", 
                    lookUpPath, playItem.Url, bytes.Length, scheduleId);
                downloadTaskSource.SetResult(playItem.Url);
                return playItem.Url;
            }

            logger.Warning("Download returned null or empty bytes for: LookUpPath={LookUpPath}, URL={Url}, attempting URL refresh", 
                lookUpPath, playItem.Url);
            var refreshedUrl = await RefreshUrlAndRetryDownloadAsync(playItem, scheduleId, cancellationToken);
            downloadTaskSource.SetResult(refreshedUrl);
            return refreshedUrl;
        }
        catch (OperationCanceledException)
        {
            logger.Information("Download cancelled for track: LookUpPath={LookUpPath}, URL={Url}", lookUpPath, playItem.Url);
            downloadTaskSource.SetCanceled(cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Exception while downloading track: LookUpPath={LookUpPath}, URL={Url}", lookUpPath, playItem.Url);

            // Try refreshing URL and retrying
            try
            {
                var refreshedUrl = await RefreshUrlAndRetryDownloadAsync(playItem, scheduleId, cancellationToken);
                downloadTaskSource.SetResult(refreshedUrl);
                return refreshedUrl;
            }
            catch (OperationCanceledException)
            {
                logger.Information("URL refresh cancelled for track: {Url}", playItem.Url);
                downloadTaskSource.SetCanceled(cancellationToken);
                throw;
            }
            catch (Exception refreshEx)
            {
                logger.Error(refreshEx, "Exception while refreshing URL for track: {Url}", playItem.Url);
                downloadTaskSource.SetResult(null);
                return null;
            }
        }
        finally
        {
            // Always remove from the dictionary when done (success or failure)
            inProgressDownloads.TryRemove(downloadKey, out _);
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

        // Use lookup path (stable) instead of CDN URL (dynamic) for cache filename
        var lookUpPath = playItem.Metadata.LookUpPath;
        var scheduleCacheFolder = GetScheduleCacheFolder(scheduleId);
        await storageService.SaveFile(scheduleCacheFolder, GetCacheFileName(lookUpPath), bytes);
        logger.Warning("Downloaded using updated URL {RefreshedUrl} (lookup path: {LookUpPath}) for {PlayItem}, ScheduleId: {ScheduleId}", 
            refreshedUrl, lookUpPath, playItem, scheduleId);
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
                
                // Check if this file matches any lookup path in the current playlist
                foreach (var playItem in playlist)
                {
                    var expectedFileName = GetCacheFileName(playItem.Metadata.LookUpPath);
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
                // Skip deletion if this file is currently being downloaded
                // Extract schedule ID and check if any download is in progress for this file
                var fileName = Path.GetFileName(filePath);
                var parentDir = Path.GetFileName(Path.GetDirectoryName(filePath) ?? "");
                
                if (int.TryParse(parentDir, out var scheduleId))
                {
                    // Check if any in-progress download matches this file
                    // Key format is "scheduleId:lookUpPath", so we need to check if the filename matches
                    var isBeingDownloaded = inProgressDownloads.Keys
                        .Any(key => key.StartsWith($"{scheduleId}:") && 
                                    GetCacheFileName(key.Substring($"{scheduleId}:".Length)) == fileName);
                    
                    if (isBeingDownloaded)
                    {
                        logger.Debug("Skipping deletion of file being downloaded: {FilePath}", filePath);
                        continue;
                    }
                }
                
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
            // IMPORTANT: If API fails, don't delete any cache files to preserve existing cache
            List<PlayItem> newPlaylist;
            try
            {
                newPlaylist = await mediaPlayService.NextTracks(scheduleId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to get playlist for schedule {ScheduleId} - API call failed. Not deleting any cache files to preserve existing cache.", scheduleId);
                return; // Don't delete any cache if API fails
            }
            
            // Get all files in the schedule's cache folder
            if (!await storageService.DirectoryExists(scheduleCacheFolder))
            {
                logger.Debug("Cache folder does not exist for schedule {ScheduleId}, nothing to clean up", scheduleId);
                return;
            }
            
            var allFiles = await storageService.GetAllFiles(scheduleCacheFolder);
            
            // Delete files that don't match the new schedule's lookup paths
            var filePathsToDelete = new HashSet<string>();
            foreach (var filePath in allFiles)
            {
                var fileName = Path.GetFileName(filePath);
                
                // Check all lookup paths to see if this file matches any of them
                bool shouldKeep = false;
                foreach (var playItem in newPlaylist)
                {
                    var expectedFileName = GetCacheFileName(playItem.Metadata.LookUpPath);
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
