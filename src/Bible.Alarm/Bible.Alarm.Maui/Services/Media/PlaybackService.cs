using Advanced.Algorithms.DataStructures.Foundation;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Contracts.Network;
using Bible.Alarm.Models;
using Bible.Alarm.Services.Contracts;
using MediaManager;
using MediaManager.Library;
using MediaManager.Media;
using MediaManager.Playback;
using MediaManager.Player;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm.Services
{
    public class PlaybackService : IPlaybackService
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;

        private readonly IMediaManager mediaManager;
        private IPlaylistService playlistService;
        private IMediaCacheService cacheService;
        private IStorageService storageService;
        private INetworkStatusService networkStatusService;
        private IDownloadService downloadService;
        private static IMediaExtractor mediaExtractor => CrossMediaManager.Current.Extractor;

        private SemaphoreSlim @lock = new SemaphoreSlim(1);

        public PlaybackService(
            IMediaManager mediaManager,
            IPlaylistService playlistService,
            IMediaCacheService cacheService,
            IStorageService storageService,
            INetworkStatusService networkStatusService,
            IDownloadService downloadService)
        {
            this.mediaManager = mediaManager;
            this.playlistService = playlistService;
            this.cacheService = cacheService;
            this.storageService = storageService;
            this.networkStatusService = networkStatusService;
            this.downloadService = downloadService;

            this.mediaManager.MediaItemFinished += markTrackAsFinished;
            this.mediaManager.StateChanged += stateChanged;
        }

        private bool isPlaying = false;

        private long currentScheduleId;
        private Dictionary<IMediaItem, NotificationDetail> currentlyPlaying;
        private IMediaItem firstChapter;
        public bool IsPlaying => isPlaying;
        public bool IsPrepared => mediaManager.Queue.Count > 0;

        public long CurrentlyPlayingScheduleId => currentScheduleId;

        public int CurrentTrackIndex { get; set; }
        public TimeSpan CurrentTrackPosition { get; set; }

        private async Task prepare(long scheduleId)
        {
            reset();
            await preparePlay(scheduleId, true, true);
        }

        public async Task Play()
        {
            if (!IsPrepared)
            {
                throw new Exception("Cannot play without preparing.");
            }

            await this.mediaManager.Play();
        }

        public async Task PrepareAndPlay(long scheduleId, bool isImmediatePlayRequest)
        {
            await Dismiss();

            reset();
            await preparePlay(scheduleId, isImmediatePlayRequest, false);
        }


        public async Task PrepareRelavantPlaylist()
        {
            var lastPlayed = await playlistService.GetRelavantScheduleToPlay();
            await prepare(lastPlayed);
        }

        public async Task Dismiss()
        {
            try
            {
                if (this.mediaManager.IsPlaying())
                {
                    await this.mediaManager.Stop();
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Error happened when stopping playback.");
            }
        }

        private void reset()
        {
            currentScheduleId = -1;
            firstChapter = null;
            currentlyPlaying = null;
            CurrentTrackIndex = -1;
            CurrentTrackPosition = default;
        }

        private async Task preparePlay(long scheduleId, bool isImmediatePlayRequest, bool prepareOnly)
        {
            Messenger<object>.Publish(MvvmMessages.ClearToasts);

            currentScheduleId = scheduleId;

            var nextTracks = await playlistService.NextTracks(scheduleId);

            var downloadedTracks = new OrderedDictionary<int, FileInfo>();
            var streamingTracks = new OrderedDictionary<int, string>();

            var playDetailMap = new Dictionary<int, NotificationDetail>();

            var i = 0;
            foreach (var item in nextTracks)
            {
                playDetailMap[i] = item.PlayDetail;

                if (await cacheService.Exists(item.Url))
                {
                    downloadedTracks.Add(i, new FileInfo(this.cacheService.GetCacheFilePath(item.Url)));
                }
                else
                {
                    streamingTracks.Add(i, item.Url);
                }

                i++;
            }

            if (downloadedTracks.Count != nextTracks.Count
                && isImmediatePlayRequest
                && !await networkStatusService.IsInternetAvailable())
            {
                await handleInternetDown(isImmediatePlayRequest, prepareOnly);
                return;
            }

            var preparedTracks = 0;
            var totalTracks = nextTracks.Count;

            Messenger<object>.Publish(MvvmMessages.ShowMediaProgessModal);
            Messenger<object>.Publish(MvvmMessages.MediaProgress, new Tuple<int, int>(preparedTracks, totalTracks));

            var downloadedMediaItems = (await Task.WhenAll(downloadedTracks.Select(x =>
            {
                return Task.Run(async () =>
                {
                    try
                    {
                        IMediaItem item;

                        if (CurrentDevice.RuntimePlatform == Device.WinUI)
                        {
                            item = new MediaItem(x.Value.FullName);
                            //TODO: Fix this
                        }
                        else
                        {
                            item = await mediaExtractor.CreateMediaItemEx(x.Value);
                        }

                        item?.SetDisplay(playDetailMap[x.Key]);
                        Messenger<object>.Publish(MvvmMessages.MediaProgress, new Tuple<int, int>(++preparedTracks, totalTracks));
                        return item;

                    }
                    catch (Exception e)
                    {
                        logger.Error(e, $"An error happened when playing file: {x.Value.FullName}.");
                        return null;
                    }
                });

            }))).ToList();

            if ((downloadedTracks.Count == 0 || !downloadedTracks.ContainsKey(0))
                && !await networkStatusService.IsInternetAvailable())
            {
                Messenger<object>.Publish(MvvmMessages.HideMediaProgressModal);
                await handleInternetDown(isImmediatePlayRequest, prepareOnly);
                return;
            }

            var streamableMediaItems = (await Task.WhenAll(streamingTracks.Select(x =>
            {
                return Task.Run(async () =>
                {
                    var playDetail = playDetailMap[x.Key];

                    try
                    {
                        var item = await mediaExtractor.CreateMediaItemEx(x.Value);
                        item?.SetDisplay(playDetail);
                        Messenger<object>.Publish(MvvmMessages.MediaProgress, new Tuple<int, int>(++preparedTracks, totalTracks));
                        return item;
                    }
                    catch (Exception e)
                    {
                        logger.Warn(e, $"An error happened when streaming file: {x.Value}. Trying to use latest source URL.");

                        try
                        {
                            if (playDetail.IsBibleReading)
                            {
                                var url = await cacheService.GetBibleChapterUrl(playDetail.LanguageCode,
                                                 playDetail.PublicationCode, playDetail.BookNumber, playDetail.ChapterNumber,
                                                 playDetail.LookUpPath);

                                if (await downloadService.FileExists(url))
                                {
                                    var item = await mediaExtractor.CreateMediaItemEx(url);
                                    item?.SetDisplay(playDetail);
                                    Messenger<object>.Publish(MvvmMessages.MediaProgress, new Tuple<int, int>(++preparedTracks, totalTracks));
                                    return item;
                                }
                            }
                            else
                            {
                                var url = await cacheService.GetMusicTrackUrl(playDetail.LanguageCode, playDetail.LookUpPath);

                                if (await downloadService.FileExists(url))
                                {
                                    var item = await mediaExtractor.CreateMediaItemEx(url);
                                    item?.SetDisplay(playDetail);
                                    Messenger<object>.Publish(MvvmMessages.MediaProgress, new Tuple<int, int>(++preparedTracks, totalTracks));
                                    return item;
                                }
                            }

                            logger.Error($"Could'nt download the streaming file: {x.Value}.");
                            return null;
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"An error happened when streaming file: {x.Value}.");
                            return null;
                        }
                    }

                });

            }))).ToList();

            var mergedMediaItems = new OrderedDictionary<int, IMediaItem>();

            i = 0;
            foreach (var item in downloadedTracks)
            {
                mergedMediaItems.Add(item.Key, downloadedMediaItems[i]);
                i++;
            }

            i = 0;
            foreach (var item in streamingTracks)
            {
                if (streamableMediaItems[i] != null)
                {
                    mergedMediaItems.Add(item.Key, streamableMediaItems[i]);
                }

                i++;
            }

            Messenger<object>.Publish(MvvmMessages.HideMediaProgressModal, null);

            currentlyPlaying = new Dictionary<IMediaItem, NotificationDetail>();

            i = 0;
            foreach (var track in mergedMediaItems)
            {
                if (track.Key != i)
                {
                    break;
                }

                currentlyPlaying.Add(track.Value, playDetailMap[track.Key]);
                i++;
            }

            if (!currentlyPlaying.Any())
            {
                await handleInternetDown(isImmediatePlayRequest, prepareOnly);
                return;
            }
            else
            {
                firstChapter = currentlyPlaying.FirstOrDefault(x => x.Value.IsBibleReading).Key;
                this.mediaManager.RepeatMode = RepeatMode.Off;

                var list = mergedMediaItems.Select(x => x.Value).ToList();
                if (prepareOnly)
                {
                    await (mediaManager as MediaManagerBase).PrepareQueueForPlayback(list);
                }
                else
                {
                    await this.mediaManager.PlayEx(list);
                }
            }
        }

        private async Task handleInternetDown(bool isImmediate, bool prepareOnly)
        {
            try
            {
                if (!isImmediate)
                {
                    var file = new FileInfo(Path.Combine(this.storageService.StorageRoot, "cool-alarm-tone-notification-sound.mp3"));
                    this.mediaManager.RepeatMode = RepeatMode.All;
                    if (prepareOnly)
                    {
                        var mediaItem = await mediaExtractor.CreateMediaItem(file);
                        await (this.mediaManager as MediaManagerBase).PrepareQueueForPlayback(mediaItem);
                    }
                    else
                    {
                        await this.mediaManager.PlayEx(file);
                    }
                }
                else
                {
                    Messenger<object>.Publish(MvvmMessages.ShowToast, "An error happened while downloading files. Your internet may be down.");
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "An error happened when handling internet down.");
            }
        }

        private async void stateChanged(object sender, StateChangedEventArgs e)
        {
            try
            {
                var mediaItem = this.mediaManager?.Queue?.Current;

                if (mediaItem == null)
                {
                    return;
                }

                if (currentlyPlaying != null && currentlyPlaying.ContainsKey(mediaItem))
                {
                    var track = currentlyPlaying[mediaItem];

                    switch (e.State)
                    {
                        case MediaPlayerState.Playing:
                            isPlaying = true;
                            await watchAndSaveProgress();
                            Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);

                            if (track.FinishedDuration.TotalSeconds > 0
                                && firstChapter != null
                                && mediaItem == firstChapter)
                            {
                                await this.mediaManager.SeekTo(track.FinishedDuration);
                                firstChapter = null;
                            }
                            break;
                        case MediaPlayerState.Stopped:
                            await stopped();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error happenned when handling playback state changed event.");
            }

            async Task stopped()
            {
                isPlaying = false;
                await stopWatching();
                Messenger<object>.Publish(MvvmMessages.HideAlarmModal);
            }
        }

        private async void markTrackAsFinished(object sender, MediaItemEventArgs e)
        {

            try
            {
                if (currentlyPlaying.ContainsKey(e.MediaItem))
                {
                    var track = currentlyPlaying[e.MediaItem];

                    if (track.IsLastTrack)
                    {
                        await playlistService.MarkTrackAsFinished(track);
                        await Dismiss();

                        var scheduleId = currentScheduleId;
                        reset();

                        if (CurrentDevice.RuntimePlatform == Device.Android)
                        {
                            await PrepareRelavantPlaylist();
                            await Play();
                        }
                        else
                        {
                            await PrepareAndPlay(scheduleId, true);
                        }

                        await Task.Delay(500);
                        mediaManager.Notification.UpdateNotification();

                        await Dismiss();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error happened when marking track as finished.");
            }

        }

        private bool isWatching = false;

        private async Task stopWatching()
        {
            await @lock.WaitAsync();

            try
            {
                if (isWatching)
                {
                    isWatching = false;
                    return;
                }

            }
            finally
            {
                @lock.Release();
            }
        }

        private Task watchtask;
        private async Task watchAndSaveProgress()
        {
            await @lock.WaitAsync();

            try
            {
                if (isWatching)
                {
                    return;
                }

                while (watchtask != null)
                {
                    await Task.Delay(100);
                }

                isWatching = true;

                watchtask = Task.Run(async () =>
                {
                    while (isWatching)
                    {
                        var acquired = await @lock.WaitAsync(1);

                        try
                        {
                            if (IsPlaying && this.mediaManager.IsPlaying())
                            {
                                var mediaItem = this.mediaManager.Queue?.Current;

                                if (mediaItem != null && currentlyPlaying != null)
                                {
                                    if (currentlyPlaying.ContainsKey(mediaItem))
                                    {
                                        var track = currentlyPlaying[mediaItem];

                                        if (track.FinishedDuration.TotalSeconds > 0
                                            && firstChapter != null
                                            && mediaItem == firstChapter)
                                        {
                                            await this.mediaManager.SeekTo(track.FinishedDuration);
                                            if (CurrentDevice.RuntimePlatform == Device.iOS)
                                            {
                                                mediaManager.Notification.UpdateNotification();
                                            }

                                            firstChapter = null;
                                        }
                                        else if (mediaManager.Position.TotalSeconds > 0)
                                        {
                                            if (mediaItem == firstChapter)
                                            {
                                                firstChapter = null;
                                            }

                                            CurrentTrackIndex = mediaManager.Queue.IndexOf(mediaItem);
                                            CurrentTrackPosition = mediaManager.Position;

                                            track.FinishedDuration = mediaManager.Position;
                                            await this.playlistService.MarkTrackAsPlayed(track);
                                            await this.playlistService.SaveLastPlayed(currentScheduleId);
                                            if (CurrentDevice.RuntimePlatform == Device.iOS)
                                            {
                                                mediaManager.Notification.UpdateNotification();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            logger.Error(e, "An error happened when updating finished track duration.");
                        }
                        finally
                        {
                            if (acquired)
                            {
                                @lock.Release();
                            }
                        }

                        await Task.Delay(1000);
                    }

                    watchtask = null;
                });
            }
            finally
            {
                @lock.Release();
            }
        }


        private bool disposed;

        private void dispose()
        {
            if (!disposed)
            {
                disposed = true;

                this.mediaManager.MediaItemFinished -= markTrackAsFinished;

                this.playlistService.Dispose();
                this.cacheService.Dispose();
                this.storageService.Dispose();
                this.networkStatusService.Dispose();
                this.mediaManager.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        ~PlaybackService()
        {
            dispose();
        }
    }
}
