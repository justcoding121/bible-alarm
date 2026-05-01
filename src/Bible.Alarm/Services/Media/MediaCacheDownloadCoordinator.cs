#nullable enable

using System.Collections.Concurrent;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media;

internal sealed record DownloadAndCacheTrackWithProgressArgs(
    ILogger Logger,
    IDownloadService DownloadService,
    IStorageService StorageService,
    IMediaUrlRefreshService UrlRefreshService,
    IMediaService MediaService,
    ConcurrentDictionary<string, Task<string?>> InProgressDownloads,
    Func<int, string> GetScheduleCacheFolder,
    Func<string, string> GetCacheFileName,
    PlayItem PlayItem,
    int ScheduleId,
    Action<long, long?>? ProgressCallback,
    CancellationToken CancellationToken = default);

internal static class MediaCacheDownloadCoordinator
{
    internal static async Task<string?> DownloadAndCacheTrackWithProgressAsync(DownloadAndCacheTrackWithProgressArgs args)
    {
        var logger = args.Logger;
        var playItem = args.PlayItem;
        var scheduleId = args.ScheduleId;
        var cancellationToken = args.CancellationToken;
        var inProgressDownloads = args.InProgressDownloads;
        var getScheduleCacheFolder = args.GetScheduleCacheFolder;
        var getCacheFileName = args.GetCacheFileName;

        var lookUpPath = playItem.Metadata.LookUpPath;
        var downloadKey = $"{scheduleId}:{lookUpPath}";

        if (inProgressDownloads.TryGetValue(downloadKey, out var existingTask))
        {
            logger.Debug(
                "Download already in progress for LookUpPath={LookUpPath}, URL={Url}, ScheduleId={ScheduleId}. Waiting for existing download to complete.",
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
                    "Existing download failed for LookUpPath={LookUpPath}, URL={Url}, ScheduleId={ScheduleId}",
                    lookUpPath,
                    playItem.Url,
                    scheduleId);
                return null;
            }
        }

        var downloadTaskSource = new TaskCompletionSource<string?>();
        var downloadTask = downloadTaskSource.Task;

        if (!inProgressDownloads.TryAdd(downloadKey, downloadTask)
            && inProgressDownloads.TryGetValue(downloadKey, out existingTask))
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

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bytes = await args.DownloadService.DownloadWithProgressAsync(playItem.Url, args.ProgressCallback, cancellationToken);

            if (bytes != null && bytes.Length > 0)
            {
                var scheduleCacheFolder = getScheduleCacheFolder(scheduleId);
                await args.StorageService.SaveFile(scheduleCacheFolder, getCacheFileName(lookUpPath), bytes);
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
                "Download returned null or empty bytes for: LookUpPath={LookUpPath}, URL={Url}. No URL refresh - failing.",
                lookUpPath,
                playItem.Url);
            downloadTaskSource.SetResult(null);
            return null;
        }
        catch (OperationCanceledException)
        {
            downloadTaskSource.SetCanceled(cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            downloadTaskSource.SetException(ex);
            throw;
        }
        finally
        {
            inProgressDownloads.TryRemove(downloadKey, out _);
        }
    }
}
