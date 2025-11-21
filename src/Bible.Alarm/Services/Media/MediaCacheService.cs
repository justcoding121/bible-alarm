using System.Collections.Concurrent;
using System.Text;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Database;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class MediaCacheService(
    ILogger logger,
    IStorageService storageService,
    IDownloadService downloadService,
    IPlaylistService mediaPlayService,
    IServiceScopeFactory scopeFactory,
    MediaService mediaService,
    INetworkStatusService networkStatusService,
    IMediaUrlRefreshService urlRefreshService)
    : IMediaCacheService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IMediaUrlRefreshService _urlRefreshService = urlRefreshService;

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

            var success = await DownloadAndCacheTrackAsync(playItem);
            if (!success) break;
        }

        return downloaded;
    }

    private async Task<bool> DownloadAndCacheTrackAsync(PlayItem playItem)
    {
        var bytes = await downloadService.DownloadAsync(playItem.Url);

        if (bytes != null)
        {
            await storageService.SaveFile(_cacheRoot, GetCacheFileName(playItem.Url), bytes);
            return true;
        }

        return await RefreshUrlAndRetryDownloadAsync(playItem);
    }

    private async Task<bool> RefreshUrlAndRetryDownloadAsync(PlayItem playItem)
    {
        var trackMetadata = playItem.Metadata;
        var refreshedUrl = await _urlRefreshService.RefreshUrlAsync(trackMetadata);

        if (refreshedUrl == null || refreshedUrl == playItem.Url)
            return false;

        await UpdateTrackUrlInDatabaseAsync(trackMetadata, refreshedUrl);
        _logger.Warning($"Refreshed URL from {playItem.Url} to {refreshedUrl} for {playItem}");

        var bytes = await downloadService.DownloadAsync(refreshedUrl);
        if (bytes == null) return false;

        await storageService.SaveFile(_cacheRoot, GetCacheFileName(refreshedUrl), bytes);
        _logger.Warning($"Downloaded using updated URL {refreshedUrl} for {playItem}");
        return true;
    }

    private async Task UpdateTrackUrlInDatabaseAsync(TrackMetadata trackMetadata, string url)
    {
        if (trackMetadata.PlayType == PlayType.Bible)
        {
            await mediaService.UpdateBibleTrackUrl(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode,
                trackMetadata.BookNumber,
                trackMetadata.ChapterNumber,
                url);
        }
        else
        {
            if (trackMetadata.LanguageCode == null)
            {
                await mediaService.UpdateMelodyTrackUrl(
                    trackMetadata.PublicationCode,
                    trackMetadata.TrackNumber,
                    url);
            }
            else
            {
                await mediaService.UpdateVocalTrackUrl(
                    trackMetadata.LanguageCode,
                    trackMetadata.PublicationCode,
                    trackMetadata.TrackNumber,
                    url);
            }
        }
    }


    public async Task CleanUpAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
        var schedules = await dbContext
            .AlarmSchedules
            .AsNoTracking()
            .ToListAsync();

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

    public void Dispose()
    {
        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // storageService, downloadService, mediaPlayService, networkStatusService, 
        // and mediaService are singletons and should not be disposed here
        // as they are managed by the DI container
    }
}