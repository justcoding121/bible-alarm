using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.Network;
using Bible.Alarm.Common.Interfaces.Storage;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class PlaybackService : IPlaybackService
{
    private readonly ILogger _logger;

    private readonly IMediaElementAudioService _mediaElementService;
    private readonly IPlaylistService _playlistService;
    private readonly IMediaCacheService _cacheService;
    private readonly IStorageService _storageService;
    private readonly INetworkStatusService _networkStatusService;
    private readonly IDownloadService _downloadService;

    private readonly SemaphoreSlim _lock = new(1);

    private bool _isPlaying;
    private long _currentScheduleId;
    private Dictionary<string, NotificationDetail> _currentlyPlaying;
    private string _firstChapter;
    private bool _isPrepared;
    private bool _isWatching;
    private Task _watchTask;

    public PlaybackService(
        ILogger logger,
        IMediaElementAudioService mediaElementService,
        IPlaylistService playlistService,
        IMediaCacheService cacheService,
        IStorageService storageService,
        INetworkStatusService networkStatusService,
        IDownloadService downloadService)
    {
        _logger = logger;
        _mediaElementService = mediaElementService;
        _playlistService = playlistService;
        _cacheService = cacheService;
        _storageService = storageService;
        _networkStatusService = networkStatusService;
        _downloadService = downloadService;

        // Subscribe to MediaElement events
        _mediaElementService.MediaEnded += OnMediaEnded;
        _mediaElementService.MediaFailed += OnMediaFailed;
    }

    public bool IsPlaying => _isPlaying;
    public bool IsPrepared => _isPrepared;
    public long CurrentlyPlayingScheduleId => _currentScheduleId;
    public int CurrentTrackIndex { get; set; }
    public TimeSpan CurrentTrackPosition { get; set; }

    public async Task PrepareRelevantPlaylist()
    {
        try
        {
            _logger.Information("Preparing relevant playlist...");
            var lastPlayed = await _playlistService.GetRelavantScheduleToPlay();
            await Prepare(lastPlayed);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error preparing relevant playlist");
            throw;
        }
    }

    public async Task PrepareRelavantPlaylist()
    {
        await PrepareRelevantPlaylist();
    }

    public async Task Play()
    {
        try
        {
            if (!IsPrepared) throw new Exception("Cannot play without preparing.");

            await _mediaElementService.Play();
            _isPlaying = true;

            // Start watching and saving progress
            await WatchAndSaveProgress();

            _logger.Information("Playback started");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during playback");
            throw;
        }
    }

    public async Task Pause()
    {
        try
        {
            await _mediaElementService.Pause();
            _isPlaying = false;

            // Stop watching and saving progress
            await StopWatching();

            _logger.Information("Playback paused");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error pausing playback");
            throw;
        }
    }

    public async Task PlayPrevious()
    {
        try
        {
            if (CurrentTrackIndex > 0)
            {
                CurrentTrackIndex--;
                await LoadAndPlayCurrentTrack();
                await Play();
                _logger.Information($"Playing previous track {CurrentTrackIndex + 1}");
            }
            else
            {
                _logger.Information("Already at first track");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error playing previous track");
            throw;
        }
    }

    public async Task PlayNext()
    {
        try
        {
            if (_currentlyPlaying != null && CurrentTrackIndex < _currentlyPlaying.Count - 1)
            {
                // Mark current track as finished before moving to next
                if (CurrentTrackIndex >= 0)
                {
                    var currentTrack = _currentlyPlaying.ElementAt(CurrentTrackIndex);
                    var playDetail = currentTrack.Value;
                    await _playlistService.MarkTrackAsFinished(playDetail);
                    await _playlistService.SaveLastPlayed(_currentScheduleId);
                }

                CurrentTrackIndex++;
                await LoadAndPlayCurrentTrack();
                await Play();
                _logger.Information($"Playing next track {CurrentTrackIndex + 1}");
            }
            else
            {
                _logger.Information("Already at last track");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error playing next track");
            throw;
        }
    }

    public async Task PrepareAndPlay(int scheduleId, bool isImmediate)
    {
        try
        {
            await Dismiss();
            Reset();
            await PreparePlay(scheduleId, isImmediate, false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error preparing and playing schedule {scheduleId}");
            throw;
        }
    }

    public async Task Dismiss()
    {
        try
        {
            if (_isPlaying)
            {
                await _mediaElementService.Stop();
                _isPlaying = false;
            }

            await StopWatching();
            _logger.Information("Playback dismissed");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error dismissing playback");
        }
    }

    private async Task Prepare(int scheduleId)
    {
        Reset();
        await PreparePlay(scheduleId, true, true);
    }

    private void Reset()
    {
        _currentScheduleId = -1;
        _firstChapter = null;
        _currentlyPlaying = null;
        CurrentTrackIndex = -1;
        CurrentTrackPosition = TimeSpan.Zero;
        _isPrepared = false;
    }

    private async Task PreparePlay(int scheduleId, bool isImmediatePlayRequest, bool prepareOnly)
    {
        try
        {
            WeakReferenceMessenger.Default.Send(new ClearToastsMessage(null));

            _currentScheduleId = scheduleId;

            var nextTracks = await _playlistService.NextTracks(scheduleId);

            var downloadedTracks = new Dictionary<int, FileInfo>();
            var streamingTracks = new Dictionary<int, string>();
            var playDetailMap = new Dictionary<int, NotificationDetail>();

            var i = 0;
            foreach (var item in nextTracks)
            {
                playDetailMap[i] = item.PlayDetail;

                if (await _cacheService.Exists(item.Url))
                    downloadedTracks.Add(i, new FileInfo(_cacheService.GetCacheFilePath(item.Url)));
                else
                    streamingTracks.Add(i, item.Url);

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

            WeakReferenceMessenger.Default.Send(new ShowMediaProgressModalMessage(null));
            WeakReferenceMessenger.Default.Send(new MediaProgressMessage(new Tuple<int, int>(preparedTracks, totalTracks)));

            // Process downloaded tracks
            var downloadedMediaItems = new Dictionary<int, string>();
            foreach (var track in downloadedTracks)
                try
                {
                    var filePath = track.Value.FullName;
                    downloadedMediaItems.Add(track.Key, filePath);
                    WeakReferenceMessenger.Default.Send(new MediaProgressMessage(
                        new Tuple<int, int>(++preparedTracks, totalTracks)));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error processing downloaded file: {track.Value.FullName}");
                }

            // Process streaming tracks
            var streamableMediaItems = new Dictionary<int, string>();
            foreach (var track in streamingTracks)
                try
                {
                    var playDetail = playDetailMap[track.Key];
                    var url = track.Value;

                    // Try to get alternative URL if needed
                    if (playDetail.IsBibleReading)
                    {
                        var altUrl = await _cacheService.GetBibleChapterUrl(playDetail.LanguageCode,
                            playDetail.PublicationCode, playDetail.BookNumber, playDetail.ChapterNumber,
                            playDetail.LookUpPath);

                        if (await _downloadService.FileExists(altUrl)) url = altUrl;
                    }
                    else
                    {
                        var altUrl =
                            await _cacheService.GetMusicTrackUrl(playDetail.LanguageCode, playDetail.LookUpPath);
                        if (await _downloadService.FileExists(altUrl)) url = altUrl;
                    }

                    streamableMediaItems.Add(track.Key, url);
                    WeakReferenceMessenger.Default.Send(new MediaProgressMessage(
                        new Tuple<int, int>(++preparedTracks, totalTracks)));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error processing streaming track: {track.Value}");
                }

            // Merge all tracks
            var mergedMediaItems = new Dictionary<int, string>();
            foreach (var item in downloadedMediaItems) mergedMediaItems.Add(item.Key, item.Value);
            foreach (var item in streamableMediaItems) mergedMediaItems.Add(item.Key, item.Value);

            WeakReferenceMessenger.Default.Send(new HideMediaProgressModalMessage(null));

            _currentlyPlaying = [];

            i = 0;
            foreach (var track in mergedMediaItems.OrderBy(x => x.Key))
            {
                if (track.Key != i) break;

                _currentlyPlaying.Add(track.Value, playDetailMap[track.Key]);
                i++;
            }

            if (!_currentlyPlaying.Any())
            {
                await HandleInternetDown(isImmediatePlayRequest, prepareOnly);
            }
            else
            {
                _firstChapter = _currentlyPlaying.FirstOrDefault(x => x.Value.IsBibleReading).Key;
                _isPrepared = true;

                // Initialize track index to start from the first track
                CurrentTrackIndex = 0;

                if (prepareOnly)
                {
                    // Just prepare, don't play
                    await LoadAndPlayCurrentTrack();
                }
                else
                {
                    await LoadAndPlayCurrentTrack();
                    if (isImmediatePlayRequest) await Play();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error in PreparePlay for schedule {scheduleId}");
            throw;
        }
    }

    private async Task HandleInternetDown(bool isImmediate, bool prepareOnly)
    {
        try
        {
            if (!isImmediate)
            {
                var file = new FileInfo(Path.Combine(_storageService.StorageRoot,
                    "cool-alarm-tone-notification-sound.mp3"));
                if (file.Exists)
                {
                    await _mediaElementService.SetSource(file.FullName);
                    if (!prepareOnly) await Play();
                }
            }
            else
            {
                WeakReferenceMessenger.Default.Send(new ShowToastMessage(
                    "An error happened while downloading files. Your internet may be down."));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling internet down");
        }
    }

    private async Task LoadAndPlayCurrentTrack()
    {
        try
        {
            if (_currentlyPlaying == null || !_currentlyPlaying.Any())
            {
                _logger.Warning("No tracks available to play");
                return;
            }

            var currentTrack = _currentlyPlaying.ElementAt(CurrentTrackIndex);
            var trackUrl = currentTrack.Key;
            var playDetail = currentTrack.Value;

            await _mediaElementService.SetSource(trackUrl);
            _logger.Information($"Loaded track {CurrentTrackIndex + 1}: {trackUrl}");

            // Handle resume from previous position
            if (playDetail.FinishedDuration.TotalSeconds > 0
                && _firstChapter != null
                && trackUrl == _firstChapter)
            {
                await _mediaElementService.SeekTo(playDetail.FinishedDuration);
                _firstChapter = null;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error loading track {CurrentTrackIndex + 1}");
            throw;
        }
    }

    private async void OnMediaEnded(object sender, EventArgs e)
    {
        try
        {
            _logger.Information("Media ended");
            _isPlaying = false;
            await StopWatching();

            // Mark current track as finished
            if (_currentlyPlaying != null && CurrentTrackIndex >= 0 && CurrentTrackIndex < _currentlyPlaying.Count)
            {
                var currentTrack = _currentlyPlaying.ElementAt(CurrentTrackIndex);
                var playDetail = currentTrack.Value;

                // Mark track as finished
                await _playlistService.MarkTrackAsFinished(playDetail);
                await _playlistService.SaveLastPlayed(_currentScheduleId);
            }

            // Check if there are more tracks to play
            if (_currentlyPlaying != null && CurrentTrackIndex < _currentlyPlaying.Count - 1)
            {
                // Move to next track
                CurrentTrackIndex++;
                _logger.Information($"Moving to next track: {CurrentTrackIndex + 1}");

                await LoadAndPlayCurrentTrack();
                await Play();
            }
            else
            {
                // No more tracks - restart the playlist
                _logger.Information("Playlist completed, restarting...");
                WeakReferenceMessenger.Default.Send(new HideAlarmModalMessage(null));

                var scheduleId = _currentScheduleId;
                await Dismiss();
                Reset();

                // Restart the playlist
                await PrepareAndPlay(scheduleId, true);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling media ended");
        }
    }

    private async void OnMediaFailed(object sender, EventArgs e)
    {
        try
        {
            _logger.Error("Media playback failed");
            _isPlaying = false;
            await StopWatching();
            WeakReferenceMessenger.Default.Send(new HideAlarmModalMessage(null));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling media failed");
        }
    }

    private async Task StopWatching()
    {
        await _lock.WaitAsync();
        try
        {
            if (_isWatching)
            {
                _isWatching = false;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task WatchAndSaveProgress()
    {
        await _lock.WaitAsync();
        try
        {
            if (_isWatching) return;

            while (_watchTask != null) await Task.Delay(100);

            _isWatching = true;

            _watchTask = Task.Run(async () =>
            {
                while (_isWatching)
                {
                    var acquired = await _lock.WaitAsync(1);

                    try
                    {
                        if (IsPlaying && _mediaElementService.IsPlaying)
                            if (_currentlyPlaying != null && CurrentTrackIndex >= 0 &&
                                CurrentTrackIndex < _currentlyPlaying.Count)
                            {
                                var currentTrack = _currentlyPlaying.ElementAt(CurrentTrackIndex);
                                var playDetail = currentTrack.Value;

                                if (playDetail.FinishedDuration.TotalSeconds > 0
                                    && _firstChapter != null
                                    && currentTrack.Key == _firstChapter)
                                {
                                    await _mediaElementService.SeekTo(playDetail.FinishedDuration);
                                    _firstChapter = null;
                                }
                                else if (_mediaElementService.CurrentTrackPosition.TotalSeconds > 0)
                                {
                                    if (currentTrack.Key == _firstChapter) _firstChapter = null;

                                    CurrentTrackPosition = _mediaElementService.CurrentTrackPosition;
                                    playDetail.FinishedDuration = _mediaElementService.CurrentTrackPosition;
                                    await _playlistService.MarkTrackAsPlayed(playDetail);
                                    await _playlistService.SaveLastPlayed(_currentScheduleId);
                                }
                            }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error updating finished track duration");
                    }
                    finally
                    {
                        if (acquired) _lock.Release();
                    }

                    await Task.Delay(1000);
                }

                _watchTask = null;
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        try
        {
            _mediaElementService.MediaEnded -= OnMediaEnded;
            _mediaElementService.MediaFailed -= OnMediaFailed;
            // Note: _playlistService, _cacheService, _storageService, _networkStatusService, 
            // and _mediaElementService are singletons and should not be disposed here
            // as they are managed by the DI container
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disposing PlaybackService");
        }
    }
}