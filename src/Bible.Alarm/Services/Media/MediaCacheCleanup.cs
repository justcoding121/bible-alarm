#nullable enable

using System.Collections.Concurrent;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

internal sealed record DeleteScheduleCacheArgs(
    ILogger Logger,
    IStorageService StorageService,
    IPlaylistService MediaPlayService,
    IAlarmScheduleService AlarmScheduleService,
    ConcurrentDictionary<string, Task<string?>> InProgressDownloads,
    Func<int, string> GetScheduleCacheFolder,
    Func<string, string> GetCacheFileName,
    int ScheduleId,
    CancellationToken CancellationToken);

internal static class MediaCacheCleanup
{
    private static async Task<HashSet<string>> GetCacheKeepFileNamesAsync(
        IPlaylistService mediaPlayService,
        AlarmSchedule schedule,
        Func<string, string> getCacheFileName)
    {
        var playItems = await mediaPlayService.NextTracks(schedule.Id);

        // Indefinite playback: we may have downloaded next/previous tracks opportunistically.
        // Keep a small lookaround window (prev + next) so cleanup doesn't delete them.
        if (schedule.NumberOfTracksToPlay <= 0 && playItems.Count > 0)
        {
            var anchorMetadata = playItems[playItems.Count - 1].Metadata;
            try
            {
                playItems.Add(await mediaPlayService.GetNextPlayItemAsync(anchorMetadata));
            }
            catch (Exception)
            {
                // Ignore - keep at least what we have.
            }

            try
            {
                playItems.Add(await mediaPlayService.GetPreviousPlayItemAsync(anchorMetadata));
            }
            catch (Exception)
            {
                // Ignore - keep at least what we have.
            }
        }

        var keepFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var playItem in playItems)
        {
            keepFileNames.Add(getCacheFileName(playItem.Metadata.LookUpPath));
        }

        return keepFileNames;
    }

    internal static async Task<HashSet<string>> GetUnusedCacheFilesAsync(
        IStorageService storageService,
        IPlaylistService mediaPlayService,
        Func<int, string> getScheduleCacheFolder,
        Func<string, string> getCacheFileName,
        List<AlarmSchedule> schedules,
        CancellationToken cancellationToken)
    {
        var filePathsToDelete = new HashSet<string>(StringComparer.Ordinal);

        // For each schedule, check for unused files in its folder
        foreach (var schedule in schedules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scheduleCacheFolder = getScheduleCacheFolder(schedule.Id);

            if (!await storageService.DirectoryExists(scheduleCacheFolder))
            {
                continue;
            }

            var keepFileNames = await GetCacheKeepFileNamesAsync(mediaPlayService, schedule, getCacheFileName);
            var allFiles = await storageService.GetAllFiles(scheduleCacheFolder);

            foreach (var filePath in allFiles)
            {
                var fileName = Path.GetFileName(filePath);
                var shouldKeep = keepFileNames.Contains(fileName);

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
            if (!await storageService.DirectoryExists(cacheRoot))
            {
                return;
            }

            var validScheduleIds = new HashSet<int>(schedules.Select(s => s.Id));

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
                        await deleteFilesAsync(new HashSet<string>(filesInFolder, StringComparer.Ordinal));

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
                        key.StartsWith(schedulePrefix, StringComparison.Ordinal) &&
                        getCacheFileName(key[schedulePrefix.Length..]) == fileName);

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

    private static async Task DeleteEntireScheduleCacheFolderAsync(DeleteScheduleCacheArgs args, string scheduleCacheFolder, int scheduleId)
    {
        if (!await args.StorageService.DirectoryExists(scheduleCacheFolder))
            return;

        var filesToDelete = await args.StorageService.GetAllFiles(scheduleCacheFolder);
        await DeleteFilesAsync(args.Logger, args.StorageService, args.InProgressDownloads, args.GetCacheFileName, new HashSet<string>(filesToDelete, StringComparer.Ordinal));
        await args.StorageService.DeleteDirectory(scheduleCacheFolder);
        args.Logger.Information("Deleted entire cache folder for deleted schedule {ScheduleId} ({Count} files)",
            scheduleId,
            filesToDelete.Count);
    }

    private static async Task AppendIndefinitePlaylistNeighborsAsync(
        IPlaylistService mediaPlayService,
        AlarmSchedule schedule,
        List<PlayItem> newPlaylist)
    {
        if (schedule.NumberOfTracksToPlay > 0 || newPlaylist.Count == 0)
            return;

        var anchorMetadata = newPlaylist[newPlaylist.Count - 1].Metadata;
        try
        {
            newPlaylist.Add(await mediaPlayService.GetNextPlayItemAsync(anchorMetadata));
        }
        catch (Exception)
        {
            // Ignore
        }

        try
        {
            newPlaylist.Add(await mediaPlayService.GetPreviousPlayItemAsync(anchorMetadata));
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    private static HashSet<string> CollectCacheFilePathsOutsideKeepSet(IEnumerable<string> allFiles, HashSet<string> keepFileNames)
    {
        var filePathsToDelete = new HashSet<string>(StringComparer.Ordinal);
        foreach (var filePath in allFiles)
        {
            var fileName = Path.GetFileName(filePath);
            if (!keepFileNames.Contains(fileName))
                filePathsToDelete.Add(filePath);
        }

        return filePathsToDelete;
    }

    internal static async Task DeleteScheduleCacheAsync(DeleteScheduleCacheArgs args)
    {
        var scheduleId = args.ScheduleId;
        if (scheduleId <= 0)
        {
            args.Logger.Warning("Skipping cache deletion for invalid schedule ID: {ScheduleId}", scheduleId);
            return;
        }

        try
        {
            var scheduleCacheFolder = args.GetScheduleCacheFolder(scheduleId);

            // Check if schedule still exists in database
            var schedule = await args.AlarmScheduleService.GetScheduleByIdAsync(
                scheduleId,
                false,
                false,
                args.CancellationToken);

            if (schedule == null)
            {
                await DeleteEntireScheduleCacheFolderAsync(args, scheduleCacheFolder, scheduleId);
                return;
            }

            // Schedule exists - update cache by deleting files that don't match new configuration
            // Get the new schedule's playlist to determine which files should be kept
            // IMPORTANT: If API fails, don't delete any cache files to preserve existing cache
            List<PlayItem> newPlaylist;
            try
            {
                newPlaylist = await args.MediaPlayService.NextTracks(scheduleId);
            }
            catch (Exception ex)
            {
                args.Logger.Error(ex, "Failed to get playlist for schedule {ScheduleId} - API call failed. Not deleting any cache files to preserve existing cache.", scheduleId);
                // Don't delete any cache if API fails
                return;
            }

            await AppendIndefinitePlaylistNeighborsAsync(args.MediaPlayService, schedule, newPlaylist);

            var keepFileNames = new HashSet<string>(
                newPlaylist.Select(pi => args.GetCacheFileName(pi.Metadata.LookUpPath)),
                StringComparer.OrdinalIgnoreCase);

            if (!await args.StorageService.DirectoryExists(scheduleCacheFolder))
            {
                args.Logger.Debug("Cache folder does not exist for schedule {ScheduleId}, nothing to clean up", scheduleId);
                return;
            }

            var allFiles = await args.StorageService.GetAllFiles(scheduleCacheFolder);

            var filePathsToDelete = CollectCacheFilePathsOutsideKeepSet(allFiles, keepFileNames);

            await DeleteFilesAsync(args.Logger, args.StorageService, args.InProgressDownloads, args.GetCacheFileName, filePathsToDelete);
            args.Logger.Information("Deleted {Count} cache files for schedule {ScheduleId} (kept {KeptCount} files)",
                filePathsToDelete.Count,
                scheduleId,
                allFiles.Count - filePathsToDelete.Count);
        }
        catch (Exception ex)
        {
            args.Logger.Error(ex, "Error deleting cache files for schedule {ScheduleId}", scheduleId);
        }
    }
}

