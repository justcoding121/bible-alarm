#nullable enable

using System.Collections.Concurrent;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media;

internal static class MediaCacheDownloadCoordinator
{
    internal static async Task<string?> DownloadAndCacheTrackWithProgressAsync(
        ILogger logger,
        IDownloadService downloadService,
        IStorageService storageService,
        IMediaUrlRefreshService urlRefreshService,
        IMediaService mediaService,
        ConcurrentDictionary<string, Task<string?>> inProgressDownloads,
        Func<int, string> getScheduleCacheFolder,
        Func<string, string> getCacheFileName,
        PlayItem playItem,
        int scheduleId,
        Action<long, long?>? progressCallback,
        CancellationToken cancellationToken = default)
    {
        // Use lookup path (stable) instead of CDN URL (dynamic) for cache key
        var lookUpPath = playItem.Metadata.LookUpPath;
        // Create a unique key for this download (scheduleId + lookupPath)
        var downloadKey = $"{scheduleId}:{lookUpPath}";

        // Check if there's already a download in progress for this file
        if (inProgressDownloads.TryGetValue(downloadKey, out var existingTask))
        {
            logger.Debug(
                "Download already in progress for LookUpPath={LookUpPath}, URL={Url}, ScheduleId={ScheduleId}. Waiting for existing download to complete.",
                lookUpPath,
                playItem.Url,
                scheduleId);
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
                logger.Warning(
                    ex,
                    "Existing download failed for LookUpPath={LookUpPath}, URL={Url}, ScheduleId={ScheduleId}",
                    lookUpPath,
                    playItem.Url,
                    scheduleId);
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
                logger.Debug(
                    "Another thread started download for LookUpPath={LookUpPath}, URL={Url}, ScheduleId={ScheduleId}. Waiting for that download.",
                    lookUpPath,
                    playItem.Url,
                    scheduleId);
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
                    logger.Warning(
                        ex,
                        "Concurrent download failed for LookUpPath={LookUpPath}, URL={Url}, ScheduleId={ScheduleId}",
                        lookUpPath,
                        playItem.Url,
                        scheduleId);
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
                var scheduleCacheFolder = getScheduleCacheFolder(scheduleId);
                await storageService.SaveFile(scheduleCacheFolder, getCacheFileName(lookUpPath), bytes);
                logger.Debug(
                    "Successfully downloaded and saved track: LookUpPath={LookUpPath}, URL={Url}, Size={Size} bytes, ScheduleId={ScheduleId}",
                    lookUpPath,
                    playItem.Url,
                    bytes.Length,
                    scheduleId);
                downloadTaskSource.SetResult(playItem.Url);
                return playItem.Url;
            }

            logger.Warning(
                "Download returned null or empty bytes for: LookUpPath={LookUpPath}, URL={Url}, attempting URL refresh",
                lookUpPath,
                playItem.Url);
            var refreshedUrl = await RefreshUrlAndRetryDownloadAsync(
                logger,
                downloadService,
                storageService,
                urlRefreshService,
                mediaService,
                getScheduleCacheFolder,
                getCacheFileName,
                playItem,
                scheduleId,
                cancellationToken);
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
                var refreshedUrl = await RefreshUrlAndRetryDownloadAsync(
                    logger,
                    downloadService,
                    storageService,
                    urlRefreshService,
                    mediaService,
                    getScheduleCacheFolder,
                    getCacheFileName,
                    playItem,
                    scheduleId,
                    cancellationToken);
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

    private static async Task<string?> RefreshUrlAndRetryDownloadAsync(
        ILogger logger,
        IDownloadService downloadService,
        IStorageService storageService,
        IMediaUrlRefreshService urlRefreshService,
        IMediaService mediaService,
        Func<int, string> getScheduleCacheFolder,
        Func<string, string> getCacheFileName,
        PlayItem playItem,
        int scheduleId,
        CancellationToken cancellationToken = default)
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
        logger.Warning("Refreshed URL from {OldUrl} to {NewUrl} for {PlayItem}", playItem.Url, refreshedUrl, playItem);

        var bytes = await downloadService.DownloadAsync(refreshedUrl, cancellationToken: cancellationToken);
        if (bytes == null)
        {
            return null;
        }

        // Use lookup path (stable) instead of CDN URL (dynamic) for cache filename
        var lookUpPath = playItem.Metadata.LookUpPath;
        var scheduleCacheFolder = getScheduleCacheFolder(scheduleId);
        await storageService.SaveFile(scheduleCacheFolder, getCacheFileName(lookUpPath), bytes);
        logger.Warning(
            "Downloaded using updated URL {RefreshedUrl} (lookup path: {LookUpPath}) for {PlayItem}, ScheduleId: {ScheduleId}",
            refreshedUrl,
            lookUpPath,
            playItem,
            scheduleId);
        return refreshedUrl;
    }
}

