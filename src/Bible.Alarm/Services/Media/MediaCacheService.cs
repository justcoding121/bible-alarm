using System.Collections.Concurrent;
using System.Text;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Database;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class MediaCacheService(
    ILogger logger,
    IStorageService storageService,
    IDownloadService downloadService,
    IPlaylistService mediaPlayService,
    IServiceScopeFactory scopeFactory,
    MediaService mediaService,
    INetworkStatusService networkStatusService)
    : IMediaCacheService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

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

    public async Task<bool> Exists(string url)
    {
        var cachePath = Path.Combine(_cacheRoot, GetCacheFileName(url));
        return await storageService.FileExists(cachePath);
    }

    public async Task<bool> SetupAlarmCache(int alarmScheduleId)
    {
        var downloaded = false;

        var @lock = LockStore.GetOrAdd(alarmScheduleId, new SemaphoreSlim(1));

        if (await @lock.WaitAsync(500))
            try
            {
                if (!await networkStatusService.IsInternetAvailable()) return downloaded;

                var playlist = await mediaPlayService.NextTracks(alarmScheduleId);

                foreach (var playItem in playlist)
                {
 
                    if (!await Exists(playItem.Url))
                    {
                        downloaded = true;

                        byte[] bytes = null;

                        bytes = await downloadService.DownloadAsync(playItem.Url);

                        if (bytes != null)
                        {
                            await storageService.SaveFile(_cacheRoot, GetCacheFileName(playItem.Url), bytes);
                        }
                        else
                        {
                            var playDetail = playItem.PlayDetail;

                            string url;

                            if (playDetail.PlayType == PlayType.Bible)
                            {
                                url = await GetBibleChapterUrl(playDetail.LanguageCode, playDetail.PublicationCode,
                                    playDetail.BookNumber, playDetail.ChapterNumber, playDetail.LookUpPath);
                                if (url != null && url != playItem.Url)
                                {
                                    await mediaService.UpdateBibleTrackUrl(playDetail.LanguageCode,
                                        playDetail.PublicationCode, playDetail.BookNumber, playDetail.ChapterNumber,
                                        url);
                                    _logger.Warning($"Updated URL to {url} for {playItem}");
                                }
                                else
                                {
                                    //url haven't changed, just that download failed.
                                    break;
                                }
                            }
                            else
                            {
                                url = await GetMusicTrackUrl(playDetail.LanguageCode, playDetail.LookUpPath);

                                if (url != null && url != playItem.Url)
                                {
                                    if (playDetail.LanguageCode == null)
                                        await mediaService.UpdateMelodyTrackUrl(playDetail.PublicationCode,
                                            playDetail.TrackNumber, url);
                                    else
                                        await mediaService.UpdateVocalTrackUrl(playDetail.LanguageCode,
                                            playDetail.PublicationCode, playDetail.TrackNumber, url);

                                    _logger.Warning($"Updated URL to {url} for {playItem}");
                                }
                                else
                                {
                                    //url haven't changed, just that download failed.
                                    break;
                                }
                            }

                            if (url != null) bytes = await downloadService.DownloadAsync(url);

                            if (bytes != null)
                            {
                                await storageService.SaveFile(_cacheRoot, GetCacheFileName(url), bytes);
                                _logger.Warning($"Downloaded using updated URL {url} for {playItem}");
                                continue;
                            }

                            break;
                        }
                    }
                }
            }
            // Log all exceptions during media download for debugging purposes
            catch (Exception e)
            {
                _logger.Error(e, "An exception happened when downloading media files for caching.");
            }
            finally
            {
                try
                {
                    @lock.Release();
                }
                catch (ObjectDisposedException e)
                {
                    _logger.Error(e, "MediaCacheService: @lock disposed error.");
                }
            }

        return downloaded;
    }

    private static readonly string[] JwOrgUrls =
    [
        UrlHelper.JwOrgIndexServiceBaseUrl,
        AppConstants.ApiEndpoints.JwOrgAlternativeIndexServiceUrl
    ];

    public async Task<string> GetBibleChapterUrl(string languageCode, string pubCode, int bookNumber, int chapter,
        string lookUpPath)
    {
        try
        {
            byte[] tes;

            var harvestLink1 = $"{JwOrgUrls[0]}{lookUpPath}";
            var harvestLink2 = $"{JwOrgUrls[1]}{lookUpPath}";
            tes = await downloadService.DownloadAsync(harvestLink1, harvestLink2);
            var jsonString = Encoding.Default.GetString(tes);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            return root.GetProperty("files").GetProperty(languageCode).GetProperty("MP3")[0].GetProperty("file").GetProperty("url").GetString();
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> GetMusicTrackUrl(string languageCode, string lookUpPath)
    {
        try
        {
            var harvestLink1 = $"{JwOrgUrls[0]}{lookUpPath}";
            var harvestLink2 = $"{JwOrgUrls[1]}{lookUpPath}";

            var tes = await downloadService.DownloadAsync(harvestLink1, harvestLink2);
            var jsonString = Encoding.Default.GetString(tes);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var lc = languageCode ?? AppConstants.Media.DefaultLanguageCode;

            //patch for bad data
            if (lc == AppConstants.Media.LanguageCodePatchFrom) lc = AppConstants.Media.LanguageCodePatchTo;

            return root.GetProperty("files").GetProperty(lc).GetProperty("MP3")[0].GetProperty("file").GetProperty("url").GetString();
        }
        catch
        {
            return null;
        }
    }

    public async Task CleanUp()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
        var schedules = await dbContext
            .AlarmSchedules
            .AsNoTracking()
            .ToListAsync();

        var filePathsToDelete = new HashSet<string>(await storageService.GetAllFiles(_cacheRoot));

        foreach (var schedule in schedules)
        {
            var playlist = await mediaPlayService.NextTracks(schedule.Id);
            var filePaths = playlist.Select(x => GetCacheFilePath(x.Url)).ToList();



            filePaths.ForEach(x =>
            {
                if (filePathsToDelete.Contains(x)) filePathsToDelete.Remove(x);
            });
        }

        filePathsToDelete.ToList().ForEach(x =>
        {
  
            try
            {
                storageService.DeleteFile(x);
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to delete file: {x}");
            }
        });
    }

    public void Dispose()
    {
        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // storageService, downloadService, mediaPlayService, networkStatusService, 
        // and mediaService are singletons and should not be disposed here
        // as they are managed by the DI container
    }
}