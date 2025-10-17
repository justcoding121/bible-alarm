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
using Microsoft.Maui.Devices;
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
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;

        private readonly IMediaManager _mediaManager;
        private IPlaylistService _playlistService;
        private IMediaCacheService _cacheService;
        private IStorageService _storageService;
        private INetworkStatusService _networkStatusService;
        private IDownloadService _downloadService;
        private static IMediaExtractor MediaExtractor => CrossMediaManager.Current.Extractor;

        private SemaphoreSlim _lock = new SemaphoreSlim(1);

        public PlaybackService(
            IMediaManager mediaManager,
            IPlaylistService playlistService,
            IMediaCacheService cacheService,
            IStorageService storageService,
            INetworkStatusService networkStatusService,
            IDownloadService downloadService)
        {
            _mediaManager = mediaManager;
            _playlistService = playlistService;
            _cacheService = cacheService;
            _storageService = storageService;
            _networkStatusService = networkStatusService;
            _downloadService = downloadService;

            _mediaManager.MediaItemFinished += MarkTrackAsFinished;
            _mediaManager.StateChanged += StateChanged;
        }

        private bool _isPlaying = false;

        private long _currentScheduleId;
        private Dictionary<IMediaItem, NotificationDetail> _currentlyPlaying;
        private IMediaItem _firstChapter;
        public bool IsPlaying => _isPlaying;
        public bool IsPrepared => _mediaManager.Queue.Count > 0;

        public long CurrentlyPlayingScheduleId => _currentScheduleId;

        public int CurrentTrackIndex { get; set; }
        public TimeSpan CurrentTrackPosition { get; set; }

        private async Task Prepare(long scheduleId)
        {
            Reset();
            await PreparePlay(scheduleId, true, true);
        }

        public async Task Play()
        {
            if (!IsPrepared)
            {
                throw new Exception("Cannot play without preparing.");
            }

            await _mediaManager.Play();
        }

        public async Task PrepareAndPlay(long scheduleId, bool isImmediatePlayRequest)
        {
            await Dismiss();

            Reset();
            await PreparePlay(scheduleId, isImmediatePlayRequest, false);
        }


        public async Task PrepareRelavantPlaylist()
        {
            var lastPlayed = await _playlistService.GetRelavantScheduleToPlay();
            await Prepare(lastPlayed);
        }

        public async Task Dismiss()
        {
            try
            {
                if (_mediaManager.IsPlaying())
                {
                    await _mediaManager.Stop();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error happened when stopping playback.");
            }
        }

        private void Reset()
        {
            _currentScheduleId = -1;
            _firstChapter = null;
            _currentlyPlaying = null;
            CurrentTrackIndex = -1;
            CurrentTrackPosition = default;
        }

        private async Task PreparePlay(long scheduleId, bool isImmediatePlayRequest, bool prepareOnly)
        {
            Messenger<object>.Publish(MvvmMessages.ClearToasts);

            _currentScheduleId = scheduleId;

            var nextTracks = await _playlistService.NextTracks(scheduleId);

            var downloadedTracks = new Advanced.Algorithms.DataStructures.Foundation.OrderedDictionary<int, FileInfo>();
            var streamingTracks = new Advanced.Algorithms.DataStructures.Foundation.OrderedDictionary<int, string>();

            var playDetailMap = new Dictionary<int, NotificationDetail>();

            var i = 0;
            foreach (var item in nextTracks)
            {
                playDetailMap[i] = item.PlayDetail;

                if (await _cacheService.Exists(item.Url))
                {
                    downloadedTracks.Add(i, new FileInfo(_cacheService.GetCacheFilePath(item.Url)));
                }
                else
                {
                    streamingTracks.Add(i, item.Url);
                }

                i++;
            }

            if (downloadedTracks.Count != nextTracks.Count
                && isImmediatePlayRequest
                && !await _networkStatusService.IsInternetAvailable())
            {
                await HandleInternetDown(isImmediatePlayRequest, prepareOnly);
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

                        if (CurrentDevice.RuntimePlatform == DevicePlatform.WinUI.ToString())
                        {
                            item = new MediaItem(x.Value.FullName);
                            //TODO: Fix this
                        }
                        else
                        {
                            item = await MediaExtractor.CreateMediaItemEx(x.Value);
                        }

                        item?.SetDisplay(playDetailMap[x.Key]);
                        Messenger<object>.Publish(MvvmMessages.MediaProgress, new Tuple<int, int>(++preparedTracks, totalTracks));
                        return item;

                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"An error happened when playing file: {x.Value.FullName}.");
                        return null;
                    }
                });

            }))).ToList();

            if ((downloadedTracks.Count == 0 || !downloadedTracks.ContainsKey(0))
                && !await _networkStatusService.IsInternetAvailable())
            {
                Messenger<object>.Publish(MvvmMessages.HideMediaProgressModal);
                await HandleInternetDown(isImmediatePlayRequest, prepareOnly);
                return;
            }

            var streamableMediaItems = (await Task.WhenAll(streamingTracks.Select(x =>
            {
                return Task.Run(async () =>
                {
                    var playDetail = playDetailMap[x.Key];

                    try
                    {
                        var item = await MediaExtractor.CreateMediaItemEx(x.Value);
                        item?.SetDisplay(playDetail);
                        Messenger<object>.Publish(MvvmMessages.MediaProgress, new Tuple<int, int>(++preparedTracks, totalTracks));
                        return item;
                    }
                    catch (Exception e)
                    {
                        Logger.Warn(e, $"An error happened when streaming file: {x.Value}. Trying to use latest source URL.");

                        try
                        {
                            if (playDetail.IsBibleReading)
                            {
                                var url = await _cacheService.GetBibleChapterUrl(playDetail.LanguageCode,
                                                 playDetail.PublicationCode, playDetail.BookNumber, playDetail.ChapterNumber,
                                                 playDetail.LookUpPath);

                                if (await _downloadService.FileExists(url))
                                {
                                    var item = await MediaExtractor.CreateMediaItemEx(url);
                                    item?.SetDisplay(playDetail);
                                    Messenger<object>.Publish(MvvmMessages.MediaProgress, new Tuple<int, int>(++preparedTracks, totalTracks));
                                    return item;
                                }
                            }
                            else
                            {
                                var url = await _cacheService.GetMusicTrackUrl(playDetail.LanguageCode, playDetail.LookUpPath);

                                if (await _downloadService.FileExists(url))
                                {
                                    var item = await MediaExtractor.CreateMediaItemEx(url);
                                    item?.SetDisplay(playDetail);
                                    Messenger<object>.Publish(MvvmMessages.MediaProgress, new Tuple<int, int>(++preparedTracks, totalTracks));
                                    return item;
                                }
                            }

                            Logger.Error($"Could'nt download the streaming file: {x.Value}.");
                            return null;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, $"An error happened when streaming file: {x.Value}.");
                            return null;
                        }
                    }

                });

            }))).ToList();

            var mergedMediaItems = new Advanced.Algorithms.DataStructures.Foundation.OrderedDictionary<int, IMediaItem>();

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

            _currentlyPlaying = new Dictionary<IMediaItem, NotificationDetail>();

            i = 0;
            foreach (var track in mergedMediaItems)
            {
                if (track.Key != i)
                {
                    break;
                }

                _currentlyPlaying.Add(track.Value, playDetailMap[track.Key]);
                i++;
            }

            if (!_currentlyPlaying.Any())
            {
                await HandleInternetDown(isImmediatePlayRequest, prepareOnly);
                return;
            }
            else
            {
                _firstChapter = _currentlyPlaying.FirstOrDefault(x => x.Value.IsBibleReading).Key;
                _mediaManager.RepeatMode = RepeatMode.Off;

                var list = mergedMediaItems.Select(x => x.Value).ToList();
                if (prepareOnly)
                {
                    await (_mediaManager as MediaManagerBase).PrepareQueueForPlayback(list);
                }
                else
                {
                    await _mediaManager.PlayEx(list);
                }
            }
        }

        private async Task HandleInternetDown(bool isImmediate, bool prepareOnly)
        {
            try
            {
                if (!isImmediate)
                {
                    var file = new FileInfo(Path.Combine(_storageService.StorageRoot, "cool-alarm-tone-notification-sound.mp3"));
                    _mediaManager.RepeatMode = RepeatMode.All;
                    if (prepareOnly)
                    {
                        var mediaItem = await MediaExtractor.CreateMediaItem(file);
                        await (_mediaManager as MediaManagerBase).PrepareQueueForPlayback(mediaItem);
                    }
                    else
                    {
                        await _mediaManager.PlayEx(file);
                    }
                }
                else
                {
                    Messenger<object>.Publish(MvvmMessages.ShowToast, "An error happened while downloading files. Your internet may be down.");
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when handling internet down.");
            }
        }

        private async void StateChanged(object sender, StateChangedEventArgs e)
        {
            try
            {
                var mediaItem = _mediaManager?.Queue?.Current;

                if (mediaItem == null)
                {
                    return;
                }

                if (_currentlyPlaying != null && _currentlyPlaying.ContainsKey(mediaItem))
                {
                    var track = _currentlyPlaying[mediaItem];

                    switch (e.State)
                    {
                        case MediaPlayerState.Playing:
                            _isPlaying = true;
                            await WatchAndSaveProgress();
                            Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);

                            if (track.FinishedDuration.TotalSeconds > 0
                                && _firstChapter != null
                                && mediaItem == _firstChapter)
                            {
                                await _mediaManager.SeekTo(track.FinishedDuration);
                                _firstChapter = null;
                            }
                            break;
                        case MediaPlayerState.Stopped:
                            await Stopped();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "An error happenned when handling playback state changed event.");
            }

            async Task Stopped()
            {
                _isPlaying = false;
                await StopWatching();
                Messenger<object>.Publish(MvvmMessages.HideAlarmModal);
            }
        }

        private async void MarkTrackAsFinished(object sender, MediaItemEventArgs e)
        {

            try
            {
                if (_currentlyPlaying.ContainsKey(e.MediaItem))
                {
                    var track = _currentlyPlaying[e.MediaItem];

                    if (track.IsLastTrack)
                    {
                        await _playlistService.MarkTrackAsFinished(track);
                        await Dismiss();

                        var scheduleId = _currentScheduleId;
                        Reset();

                        if (CurrentDevice.RuntimePlatform == DevicePlatform.Android.ToString())
                        {
                            await PrepareRelavantPlaylist();
                            await Play();
                        }
                        else
                        {
                            await PrepareAndPlay(scheduleId, true);
                        }

                        await Task.Delay(500);
                        _mediaManager.Notification.UpdateNotification();

                        await Dismiss();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "An error happened when marking track as finished.");
            }

        }

        private bool _isWatching = false;

        private async Task StopWatching()
        {
            await _lock.WaitAsync();

            try
            {
                if (_isWatching)
                {
                    _isWatching = false;
                    return;
                }

            }
            finally
            {
                _lock.Release();
            }
        }

        private Task _watchtask;
        private async Task WatchAndSaveProgress()
        {
            await _lock.WaitAsync();

            try
            {
                if (_isWatching)
                {
                    return;
                }

                while (_watchtask != null)
                {
                    await Task.Delay(100);
                }

                _isWatching = true;

                _watchtask = Task.Run(async () =>
                {
                    while (_isWatching)
                    {
                        var acquired = await _lock.WaitAsync(1);

                        try
                        {
                            if (IsPlaying && _mediaManager.IsPlaying())
                            {
                                var mediaItem = _mediaManager.Queue?.Current;

                                if (mediaItem != null && _currentlyPlaying != null)
                                {
                                    if (_currentlyPlaying.ContainsKey(mediaItem))
                                    {
                                        var track = _currentlyPlaying[mediaItem];

                                        if (track.FinishedDuration.TotalSeconds > 0
                                            && _firstChapter != null
                                            && mediaItem == _firstChapter)
                                        {
                                            await _mediaManager.SeekTo(track.FinishedDuration);
                                            if (CurrentDevice.RuntimePlatform == DevicePlatform.iOS.ToString())
                                            {
                                                _mediaManager.Notification.UpdateNotification();
                                            }

                                            _firstChapter = null;
                                        }
                                        else if (_mediaManager.Position.TotalSeconds > 0)
                                        {
                                            if (mediaItem == _firstChapter)
                                            {
                                                _firstChapter = null;
                                            }

                                            CurrentTrackIndex = _mediaManager.Queue.IndexOf(mediaItem);
                                            CurrentTrackPosition = _mediaManager.Position;

                                            track.FinishedDuration = _mediaManager.Position;
                                            await _playlistService.MarkTrackAsPlayed(track);
                                            await _playlistService.SaveLastPlayed(_currentScheduleId);
                                            if (CurrentDevice.RuntimePlatform == DevicePlatform.iOS.ToString())
                                            {
                                                _mediaManager.Notification.UpdateNotification();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "An error happened when updating finished track duration.");
                        }
                        finally
                        {
                            if (acquired)
                            {
                                _lock.Release();
                            }
                        }

                        await Task.Delay(1000);
                    }

                    _watchtask = null;
                });
            }
            finally
            {
                _lock.Release();
            }
        }


        private bool _disposed;

        private void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _mediaManager.MediaItemFinished -= MarkTrackAsFinished;

                _playlistService.Dispose();
                _cacheService.Dispose();
                _storageService.Dispose();
                _networkStatusService.Dispose();
                _mediaManager.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        ~PlaybackService()
        {
            Dispose();
        }
    }
}
