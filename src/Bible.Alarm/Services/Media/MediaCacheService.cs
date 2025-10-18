using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Contracts.Network;
using Bible.Alarm.Models.Enums;
using Bible.Alarm.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Services
{
    public class MediaCacheService(
        IStorageService storageService,
        IDownloadService downloadService,
        IPlaylistService mediaPlayService,
        ScheduleDbContext dbContext,
        MediaService mediaService,
        INetworkStatusService networkStatusService,
        IPlaybackService playbackService)
        : IMediaCacheService
    {
        private static readonly ILogger Logger = Log.ForContext<MediaCacheService>();


        private readonly string _cacheRoot = Path.Combine(storageService.CacheRoot, "MediaCache");

        private static ConcurrentDictionary<long, SemaphoreSlim> lockStore =
                    new ConcurrentDictionary<long, SemaphoreSlim>();

        public string GetCacheFileName(string url)
        {
            var uri = new Uri(url);
            
            var plainTextBytes = Encoding.UTF8.GetBytes(uri.PathAndQuery);
            return Convert.ToBase64String(plainTextBytes) + ".mp3";
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

        public async Task<bool> SetupAlarmCache(long alarmScheduleId)
        {
            var downloaded = false;

            var @lock = lockStore.GetOrAdd(alarmScheduleId, new SemaphoreSlim(1));

            if (await @lock.WaitAsync(500))
            {
                try
                {
                    if (!await networkStatusService.IsInternetAvailable())
                    {
                        return downloaded;
                    }

                    var playlist = await mediaPlayService.NextTracks(alarmScheduleId);

                    foreach (var playItem in playlist)
                    {
                        //do not download while playing
                        if (playbackService.IsPrepared)
                        {
                            break;
                        }

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

                                if (playDetail.PlayType == Models.PlayType.Bible)
                                {
                                    url = await GetBibleChapterUrl(playDetail.LanguageCode, playDetail.PublicationCode,
                                                    playDetail.BookNumber, playDetail.ChapterNumber, playDetail.LookUpPath);
                                    if (url != null && url != playItem.Url)
                                    {
                                        await mediaService.UpdateBibleTrackUrl(playDetail.LanguageCode, playDetail.PublicationCode, playDetail.BookNumber, playDetail.ChapterNumber, url);
                                        Logger.Warning($"Updated URL to {url} for {playItem.ToString()}");
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
                                        {
                                            await mediaService.UpdateMelodyTrackUrl(playDetail.PublicationCode, playDetail.TrackNumber, url);
                                        }
                                        else
                                        {
                                            await mediaService.UpdateVocalTrackUrl(playDetail.LanguageCode, playDetail.PublicationCode, playDetail.TrackNumber, url);
                                        }

                                        Logger.Warning($"Updated URL to {url} for {playItem.ToString()}");
                                    }
                                    else
                                    {
                                        //url haven't changed, just that download failed.
                                        break;
                                    }
                                }

                                if (url != null)
                                {
                                    bytes = await downloadService.DownloadAsync(url);
                                }

                                if (bytes != null)
                                {
                                    await storageService.SaveFile(_cacheRoot, GetCacheFileName(url), bytes);
                                    Logger.Warning($"Downloaded using updated URL {url} for {playItem.ToString()}");
                                    continue;
                                }

                                break;
                            }

                        }
                    }

                }
                //TODO ignore network errors from getting logged
                catch (Exception e)
                {
                    Logger.Error(e, "An exception happened when downloading media files for caching.");
                }
                finally
                {
                    try
                    {
                        @lock.Release();
                    }
                    catch (ObjectDisposedException e)
                    {
                        Logger.Error(e, "MediaCacheService: @lock disposed error.");
                    }
                }

            }

            return downloaded;
        }

        private static string[] jwOrgUrls = new string[] { UrlHelper.JwOrgIndexServiceBaseUrl,
                                                      "https://apps.jw.org/GETPUBMEDIALINKS"};
        public async Task<string> GetBibleChapterUrl(string languageCode, string pubCode, int bookNumber, int chapter, string lookUpPath)
        {
            try
            {
                var sourceWebsite = SourceHelper.GetSourceWebsite(pubCode);

                byte[] @bytes;

                var harvestLink1 = $"{jwOrgUrls[0]}{lookUpPath}";
                var harvestLink2 = $"{jwOrgUrls[1]}{lookUpPath}";
                @bytes = await downloadService.DownloadAsync(harvestLink1, harvestLink2);
                string jsonString = Encoding.Default.GetString(@bytes);
                dynamic model = JsonConvert.DeserializeObject<dynamic>(jsonString);

                return model["files"][languageCode]["MP3"][0]["file"]["url"];


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
                var harvestLink1 = $"{jwOrgUrls[0]}{lookUpPath}";
                var harvestLink2 = $"{jwOrgUrls[1]}{lookUpPath}";

                var @bytes = await downloadService.DownloadAsync(harvestLink1, harvestLink2);
                string jsonString = Encoding.Default.GetString(@bytes);
                dynamic model = JsonConvert.DeserializeObject<dynamic>(jsonString);

                var lc = languageCode ?? "E";

                //patch for bad data
                if (lc == "LAH")
                {
                    lc = "LAHU";
                }

                return model.files[lc].MP3[0].file.url;
            }
            catch
            {
                return null;
            }

        }

        public async Task CleanUp()
        {
            var schedules = await dbContext
                .AlarmSchedules
                .AsNoTracking()
                .ToListAsync();

            var filePathsToDelete = new HashSet<string>(await storageService.GetAllFiles(_cacheRoot));

            foreach (var schedule in schedules)
            {
                var playlist = await mediaPlayService.NextTracks(schedule.Id);
                var filePaths = playlist.Select(x => GetCacheFilePath(x.Url)).ToList();

                //do not delete anything when alarm is playing!
                if (schedule.NextFireDate(DateTime.Now.AddMinutes(-5)) <= DateTimeOffset.Now.AddMinutes(5)
                    || playbackService.IsPrepared)
                {
                    return;
                }

                filePaths.ForEach(x =>
                {
                    if (filePathsToDelete.Contains(x))
                    {
                        filePathsToDelete.Remove(x);
                    }
                });
            }

            filePathsToDelete.ToList().ForEach(x =>
            {
                if (playbackService.IsPrepared)
                    return;

                try
                {
                    storageService.DeleteFile(x);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Failed to delete file: {x}");
                }
            });

        }

        public void Dispose()
        {
            storageService.Dispose();
            downloadService.Dispose();
            mediaPlayService.Dispose();
            dbContext.Dispose();
            networkStatusService.Dispose();
            mediaService.Dispose();

        }
    }
}
