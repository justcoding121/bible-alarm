﻿using Bible.Alarm.Contracts.Network;
using Bible.Alarm.Services.Contracts;
using MediaManager;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Services
{
    public class MediaCacheService : IMediaCacheService
    {
        private static Logger logger => LogManager.GetCurrentClassLogger();

        private readonly string cacheRoot;

        private IStorageService storageService;
        private IDownloadService downloadService;
        private IPlaylistService mediaPlayService;
        private ScheduleDbContext scheduleDbContext;
        private INetworkStatusService networkStatusService;
        private IMediaManager mediaManager;

        private MediaService mediaService;

        private static ConcurrentDictionary<long, SemaphoreSlim> lockStore =
                    new ConcurrentDictionary<long, SemaphoreSlim>();

        public MediaCacheService(IStorageService storageService,
            IDownloadService downloadService, IPlaylistService mediaPlayService,
            ScheduleDbContext dbContext, MediaService mediaService,
            INetworkStatusService networkStatusService,
            IMediaManager mediaManager)
        {
            this.storageService = storageService;
            this.downloadService = downloadService;
            this.mediaPlayService = mediaPlayService;
            this.scheduleDbContext = dbContext;
            this.mediaService = mediaService;
            this.networkStatusService = networkStatusService;
            this.mediaManager = mediaManager;

            cacheRoot = Path.Combine(storageService.StorageRoot, "MediaCache");
        }

        public string GetCacheFileName(string url)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(url);
            return Convert.ToBase64String(plainTextBytes) + ".mp3";
        }

        public string GetCacheFilePath(string url)
        {
            return Path.Combine(cacheRoot, GetCacheFileName(url));
        }

        public async Task<bool> Exists(string url)
        {
            var cachePath = Path.Combine(cacheRoot, GetCacheFileName(url));
            return await storageService.FileExists(cachePath);
        }

        public async Task SetupAlarmCache(long alarmScheduleId)
        {
            var @lock = lockStore.GetOrAdd(alarmScheduleId, new SemaphoreSlim(1));

            if (await @lock.WaitAsync(500))
            {
                try
                {
                    if (!await networkStatusService.IsInternetAvailable())
                    {
                        return;
                    }

                    var playlist = await mediaPlayService.NextTracks(alarmScheduleId, TimeSpan.FromHours(1));

                    foreach (var playItem in playlist)
                    {
                        //do not download while playing
                        if (mediaManager.IsPlaying()
                            || mediaManager.IsBuffering()
                            || mediaManager.IsPrepared())
                        {
                            break;
                        }

                        if (!await Exists(playItem.Url))
                        {
                            logger.Info($"Cache miss. Downloading {playItem.ToString()}");

                            byte[] bytes = null;

                            bytes = await downloadService.DownloadAsync(playItem.Url);

                            if (bytes != null)
                            {
                                await storageService.SaveFile(cacheRoot, GetCacheFileName(playItem.Url), bytes);
                            }
                            else
                            {
                                logger.Info($"Download failed. Attempting to refresh URL for {playItem.ToString()}");

                                var playDetail = playItem.PlayDetail;

                                string url;

                                if (playDetail.PlayType == Models.PlayType.Bible)
                                {
                                    url = await getBibleChapterUrl(playDetail.LanguageCode, playDetail.LookUpPath);
                                    if (url != null && url != playItem.Url)
                                    {
                                        await mediaService.UpdateBibleTrackUrl(playDetail.LanguageCode, playDetail.PublicationCode, playDetail.BookNumber, playDetail.ChapterNumber, url);
                                        logger.Info($"Updated URL to {url} for {playItem.ToString()}");
                                    }
                                    else
                                    {
                                        //url haven't changed, just that download failed.
                                        break;
                                    }
                                }
                                else
                                {
                                    logger.Info($"Url changed for {playItem.ToString()}");

                                    url = await getMusicTrackUrl(playDetail.LanguageCode, playDetail.LookUpPath);

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

                                        logger.Info($"Updated URL to {url} for {playItem.ToString()}");
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
                                    await storageService.SaveFile(cacheRoot, GetCacheFileName(url), bytes);
                                    logger.Info($"Downloaed using updated URL {url} for {playItem.ToString()}");
                                    continue;
                                }

                                break;
                            }

                        }
                        else
                        {
                            logger.Info($"Cache hit. Skipped downloading {playItem.ToString()}");
                        }
                    }

                }
                //TODO ignore network errors from getting logged
                catch (Exception e)
                {
                    logger.Error(e, "An exception happened when downloading media files for caching.");
                }
                finally
                {
                    @lock.Release();
                }
            }

        }

        private static string[] urls = new string[] { "https://api.hag27.com/GETPUBMEDIALINKS",
                                                      "https://apps.jw.org/GETPUBMEDIALINKS"};

        private async Task<string> getBibleChapterUrl(string languageCode, string lookUpPath)
        {
            try
            {
                var harvestLink1 = $"{urls[0]}{lookUpPath}";
                var harvestLink2 = $"{urls[1]}{lookUpPath}";

                var @bytes = await downloadService.DownloadAsync(harvestLink1, harvestLink2);
                string jsonString = Encoding.Default.GetString(@bytes);
                dynamic model = JsonConvert.DeserializeObject<dynamic>(jsonString);

                return model.files[languageCode].MP3[0].file.url;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> getMusicTrackUrl(string languageCode, string lookUpPath)
        {
            try
            {
                var harvestLink1 = $"{urls[0]}{lookUpPath}";
                var harvestLink2 = $"{urls[1]}{lookUpPath}";

                var @bytes = await downloadService.DownloadAsync(harvestLink1, harvestLink2);
                string jsonString = Encoding.Default.GetString(@bytes);
                dynamic model = JsonConvert.DeserializeObject<dynamic>(jsonString);

                return model.files[languageCode == null ? "E" : languageCode].MP3[0].file.url;
            }
            catch
            {
                return null;
            }

        }

        public async Task CleanUp()
        {

            var schedules = await scheduleDbContext
                .AlarmSchedules
                .AsNoTracking()
                .ToListAsync();

            var files = (await storageService.GetAllFiles(cacheRoot)).ToDictionary(x => x, null);

            foreach (var schedule in schedules)
            {
                //skip if alarm will be fired soon or already fired
                if (schedule.NextFireDate(DateTime.Now.AddMinutes(-5)) <= DateTimeOffset.Now.AddMinutes(5)
                    || mediaManager.IsPlaying() || mediaManager.IsBuffering() || mediaManager.IsPrepared())
                {
                    continue;
                }

                var playlist = await mediaPlayService.NextTracks(schedule.Id, TimeSpan.FromHours(1));
                var fileNames = playlist.Select(x => GetCacheFilePath(x.Url)).ToList();

                fileNames.ForEach(x =>
                {
                    if (files.ContainsKey(x))
                    {
                        files.Remove(x);
                    }
                });
            }

            files.Select(x => x.Key).ToList().ForEach(x =>
            {
                try {
                    storageService.DeleteFile(x);
                    logger.Info($"Deleted file {x}");
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to download file {x}");
                }
            });
        }
    }
}
