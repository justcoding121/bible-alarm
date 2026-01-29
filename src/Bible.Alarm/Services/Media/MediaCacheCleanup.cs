#nullable enable

using System.Collections.Concurrent;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

internal static class MediaCacheCleanup
{
    internal static async Task<HashSet<string>> GetUnusedCacheFilesAsync(
        IStorageService storageService,
        IPlaylistService mediaPlayService,
        Func<int, string> getScheduleCacheFolder,
        Func<string, string> getCacheFileName,
        CancellationToken cancellationToken,
        List<AlarmSchedule> schedules)
    {
        var filePathsToDelete = new HashSet<string>();

        // For each schedule, check for unused files in its folder
        foreach (var schedule in schedules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scheduleCacheFolder = getScheduleCacheFolder(schedule.Id);

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
                    var expectedFileName = getCacheFileName(playItem.Metadata.LookUpPath);
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

    internal static async Task CleanUpOrphanedFoldersAsync(
        ILogger logger,
        IStorageService storageService,
        string cacheRoot,
        IReadOnlyCollection<AlarmSchedule> schedules,
        Func<HashSet<string>, Task> deleteFilesAsync)
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
                        directoryName,
                        cacheRoot);
                    orphanedFolders.Add(directoryPath);
                    continue;
                }

                // Check if this schedule ID exists in the database
                if (!validScheduleIds.Contains(scheduleId))
                {
                    // This folder belongs to a schedule that no longer exists
                    orphanedFolders.Add(directoryPath);
                    logger.Debug("Found orphaned cache folder for deleted schedule {ScheduleId}: {FolderPath}",
                        scheduleId,
                        directoryPath);
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
                        await deleteFilesAsync(new HashSet<string>(filesInFolder));

                        // Delete the folder itself
                        await storageService.DeleteDirectory(folderPath);

                        logger.Information("Deleted orphaned cache folder: {FolderPath} ({FileCount} files)",
                            folderPath,
                            filesInFolder.Count);
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

    internal static Task DeleteFilesAsync(
        ILogger logger,
        IStorageService storageService,
        ConcurrentDictionary<string, Task<string?>> inProgressDownloads,
        Func<string, string> getCacheFileName,
        HashSet<string> filePathsToDelete)
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
                    var schedulePrefix = $"{scheduleId}:";
                    var isBeingDownloaded = inProgressDownloads.Keys.Any(key =>
                        key.StartsWith(schedulePrefix) &&
                        getCacheFileName(key.Substring(schedulePrefix.Length)) == fileName);

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
                logger.Error(e, "Failed to delete file: {FilePath}", filePath);
            }
        }

        return Task.CompletedTask;
    }

    internal static async Task DeleteScheduleCacheAsync(
        ILogger logger,
        IStorageService storageService,
        IPlaylistService mediaPlayService,
        IAlarmScheduleService alarmScheduleService,
        ConcurrentDictionary<string, Task<string?>> inProgressDownloads,
        Func<int, string> getScheduleCacheFolder,
        Func<string, string> getCacheFileName,
        CancellationToken cancellationToken,
        int scheduleId)
    {
        if (scheduleId <= 0)
        {
            logger.Warning("Skipping cache deletion for invalid schedule ID: {ScheduleId}", scheduleId);
            return;
        }

        try
        {
            var scheduleCacheFolder = getScheduleCacheFolder(scheduleId);

            // Check if schedule still exists in database
            var scheduleExists = await alarmScheduleService.GetScheduleByIdAsync(
                scheduleId,
                false,
                false,
                cancellationToken) != null;

            if (!scheduleExists)
            {
                // Schedule was deleted - delete entire folder
                if (await storageService.DirectoryExists(scheduleCacheFolder))
                {
                    var filesToDelete = await storageService.GetAllFiles(scheduleCacheFolder);
                    await DeleteFilesAsync(logger, storageService, inProgressDownloads, getCacheFileName, new HashSet<string>(filesToDelete));
                    await storageService.DeleteDirectory(scheduleCacheFolder);
                    logger.Information("Deleted entire cache folder for deleted schedule {ScheduleId} ({Count} files)",
                        scheduleId,
                        filesToDelete.Count);
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
                    var expectedFileName = getCacheFileName(playItem.Metadata.LookUpPath);
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

            await DeleteFilesAsync(logger, storageService, inProgressDownloads, getCacheFileName, filePathsToDelete);
            logger.Information("Deleted {Count} cache files for schedule {ScheduleId} (kept {KeptCount} files)",
                filePathsToDelete.Count,
                scheduleId,
                allFiles.Count - filePathsToDelete.Count);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error deleting cache files for schedule {ScheduleId}", scheduleId);
        }
    }
}

