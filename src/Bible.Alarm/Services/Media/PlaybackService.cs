#nullable enable
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
namespace Bible.Alarm.Services.Media;

public sealed class PlaybackService : IPlaybackService, IRecipient<NextButtonPressedMessage>, IRecipient<PreviousButtonPressedMessage>, IRecipient<PlayButtonPressedMessage>, IRecipient<PauseButtonPressedMessage>, IRecipient<SeekForwardButtonPressedMessage>, IRecipient<SeekBackwardButtonPressedMessage>, IDisposable
{
    private readonly ILogger logger;
    private readonly IAudioPlayer audioPlayer;
    private readonly IPreparePlaybackService preparePlaybackService;
    private readonly IPlaylistService playlistService;
    private readonly IAlarmScheduleService alarmScheduleService;
    private readonly IDispatcher dispatcher;
    private readonly IDisplayMetadataService displayMetadataService;
    private readonly IFallbackAlarmSoundService fallbackAlarmSoundService;
    private readonly INotificationService notificationService;

    private readonly PlaybackStateManager stateManager;
    private readonly PlaybackNavigationManager navigationManager;
    private readonly ProgressTracker progressTracker;
    private readonly PlaybackOperationHandler operationHandler;
    private readonly TrackPreparationHandler trackPreparationHandler;
    private readonly PlaybackEventHandler eventHandler;
    private readonly PlaybackFailureHandler failureHandler;
    private readonly PlaybackInitializer initializer;
    private readonly PlaybackNavigationHandler navigationHandler;
    private readonly PlaybackStopHandler stopHandler;
    private readonly TrackPlaybackHandler trackPlaybackHandler;
    private readonly SystemControlsHandler systemControlsHandler;
    private readonly TrackMarker trackMarker;
    private readonly IState<PlaybackState> playbackState;

    public PlaybackService(
        ILogger logger,
        IAudioPlayer audioPlayer,
        IPreparePlaybackService preparePlaybackService,
        IPlaylistService playlistService,
        IAlarmScheduleService alarmScheduleService,
        IFallbackAlarmSoundService fallbackAlarmSoundService,
        IDispatcher dispatcher,
        INotificationService notificationService,
        IDisplayMetadataService displayMetadataService,
        IState<PlaybackState> playbackState
        )
    {
        this.logger = logger;
        this.audioPlayer = audioPlayer;
        this.preparePlaybackService = preparePlaybackService;
        this.playlistService = playlistService;
        this.alarmScheduleService = alarmScheduleService;
        this.dispatcher = dispatcher;
        this.displayMetadataService = displayMetadataService;
        this.fallbackAlarmSoundService = fallbackAlarmSoundService;
        this.notificationService = notificationService;
        this.playbackState = playbackState;

        stateManager = new PlaybackStateManager(logger);
        navigationManager = new PlaybackNavigationManager(dispatcher, logger);
        progressTracker = new ProgressTracker(playlistService, audioPlayer, logger);
        operationHandler = new PlaybackOperationHandler(audioPlayer, dispatcher, logger, progressTracker);
        trackPreparationHandler = new TrackPreparationHandler(audioPlayer, playlistService, logger);
        eventHandler = new PlaybackEventHandler(playlistService, dispatcher, logger, navigationManager);
        failureHandler = new PlaybackFailureHandler(fallbackAlarmSoundService, notificationService, dispatcher, logger);
        initializer = new PlaybackInitializer(preparePlaybackService, dispatcher, logger);
        navigationHandler = new PlaybackNavigationHandler(audioPlayer, dispatcher, logger, progressTracker, navigationManager);
        stopHandler = new PlaybackStopHandler(audioPlayer, playlistService, dispatcher, logger, progressTracker);
        trackPlaybackHandler = new TrackPlaybackHandler(audioPlayer, logger, trackPreparationHandler, progressTracker);
        systemControlsHandler = new SystemControlsHandler(logger);
        trackMarker = new TrackMarker(playlistService, logger);

        progressTracker.SetSaveProgressCallback(() => progressTracker.SaveProgressAsync(
            stateManager.Playlist, stateManager.CurrentTrackIndex));

        WeakReferenceMessenger.Default.Register<NextButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<PreviousButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<PlayButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<PauseButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<SeekForwardButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<SeekBackwardButtonPressedMessage>(this);

        this.audioPlayer.MediaEnded += OnMediaEnded;
        this.audioPlayer.MediaFailed += OnMediaFailed;
    }

    public async Task PrepareAndPlayAsync(int scheduleId, bool isAlarm)
    {
        if (stateManager.IsPreparingOrPlaying(audioPlayer) && stateManager.CurrentScheduleId.HasValue && stateManager.CurrentScheduleId.Value != scheduleId)
        {
            logger.Information("Stopping existing playback of schedule {CurrentScheduleId} before starting schedule {ScheduleId}",
                stateManager.CurrentScheduleId.Value, scheduleId);
            await trackMarker.MarkTrackAsPlayedAsync(stateManager.Playlist, stateManager.CurrentTrackIndex);
            await StopAsyncInternal(skipMarkAsPlayed: true, skipSaveLastPlayed: true);
        }

        if (stateManager.IsPreparingOrPlaying(audioPlayer))
        {
            logger.Warning("Cannot prepare and play schedule {ScheduleId} - already preparing or playing schedule {CurrentScheduleId}. Status: {Status}",
                scheduleId,
                stateManager.CurrentScheduleId,
                audioPlayer.Status);
            return;
        }

        try
        {
            stateManager.CurrentScheduleId = scheduleId;
            stateManager.IsAlarm = isAlarm;

            stateManager.PreparationCancellationTokenSource?.Dispose();
            stateManager.PreparationCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = stateManager.PreparationCancellationTokenSource.Token;

            // Capture playback mode once for this session.
            stateManager.IsIndefinitePlayback = await IsIndefinitePlaybackAsync(scheduleId, cancellationToken);

            stateManager.Playlist = await initializer.PrepareTracksAsync(scheduleId, cancellationToken);

            if (stateManager.Playlist is null)
            {
                logger.Information("Track preparation cancelled or failed for schedule {ScheduleId}", scheduleId);

                // Keep modal open and show error with retry option for both alarm and non-alarm
                // For alarms, show message about playing default alarm sound
                var errorMessage = isAlarm
                    ? "Download failed. Playing default alarm sound."
                    : "Media download failed. Check your internet connection.";

                dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = errorMessage
                });
                dispatcher.Dispatch(new PlaybackStatusChangedAction(PlayStatus.Failed));

                if (isAlarm)
                {
                    await TryPlayFallbackAlarmSoundAsync(scheduleId, keepErrorMessage: true);
                }

                return;
            }

            if (stateManager.Playlist.Count == 0)
            {
                logger.Warning("No tracks prepared for schedule {ScheduleId}", scheduleId);

                // Keep modal open and show error with retry option for both alarm and non-alarm
                // For alarms, show message about playing default alarm sound
                var errorMessage = isAlarm
                    ? "Download failed. Playing default alarm sound."
                    : "Media download failed. Check your internet connection.";

                dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = errorMessage
                });
                dispatcher.Dispatch(new PlaybackStatusChangedAction(PlayStatus.Failed));

                if (isAlarm)
                {
                    await TryPlayFallbackAlarmSoundAsync(scheduleId, keepErrorMessage: true);
                }

                return;
            }

            stateManager.CurrentTrackIndex = 0;
            stateManager.ManuallyVisitedTrackIndices.Clear();

            await InitializeSessionNavigationContextAsync();

            navigationManager.NotifyNavigationChanged(stateManager.Playlist, stateManager.CurrentTrackIndex);

            // Playback operations (PlayCurrentTrackAsync) should run on main thread since they interact with MediaElement
            await PlayCurrentTrackAsync();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error preparing and playing schedule {ScheduleId}", scheduleId);
            await ResetAsync();
            throw;
        }
    }

    public async Task PlayAsync()
    {
        await operationHandler.PlayAsync(
            stateManager.Playlist,
            stateManager.CurrentTrackIndex,
            () => PlayCurrentTrackAsync());
    }

    public async Task PauseAsync()
    {
        await operationHandler.PauseAsync(
            stateManager.CurrentScheduleId,
            stateManager.CurrentTrackIndex,
            stateManager.IsPreparingOrPlaying(audioPlayer));
    }

    public async Task PlayNextAsync()
    {
        // Any manual next/prev interaction makes the playback session indefinite.
        stateManager.IsIndefinitePlayback = true;
        await navigationHandler.PlayNextAsync(
            stateManager.Playlist,
            () => stateManager.CurrentTrackIndex,
            idx => stateManager.CurrentTrackIndex = idx,
            stateManager.CurrentScheduleId,
            stateManager.IsIndefinitePlayback,
            () => TryAppendNextTrackAsync(),
            stateManager.ManuallyVisitedTrackIndices,
            idx => trackMarker.MarkTrackAsPlayedAsync(stateManager.Playlist, idx),
            startFromBeginning => PlayCurrentTrackAsync(startFromBeginning),
            stopPlaybackAsync: () => StopAsyncInternal(skipMarkAsPlayed: true));
    }

    public async Task PlayPreviousAsync()
    {
        // Any manual next/prev interaction makes the playback session indefinite.
        stateManager.IsIndefinitePlayback = true;
        await navigationHandler.PlayPreviousAsync(
            stateManager.Playlist,
            () => stateManager.CurrentTrackIndex,
            idx => stateManager.CurrentTrackIndex = idx,
            stateManager.CurrentScheduleId,
            stateManager.IsIndefinitePlayback,
            () => TryPrependPreviousTrackAsync(),
            stateManager.ManuallyVisitedTrackIndices,
            idx => trackMarker.MarkTrackAsPlayedAsync(stateManager.Playlist, idx),
            startFromBeginning => PlayCurrentTrackAsync(startFromBeginning));
    }

    private async Task InitializeSessionNavigationContextAsync()
    {
        var playlist = stateManager.Playlist;
        if (playlist == null || playlist.Count == 0)
        {
            stateManager.AnchorBibleMetadata = null;
            stateManager.PreAnchorBibleMetadata = null;
            stateManager.SessionMusicPlayItem = null;
            return;
        }

        stateManager.SessionMusicPlayItem = playlist.FirstOrDefault(t => t.PlayItem.Metadata.PlayType == PlayType.Music)?.PlayItem;
        stateManager.AnchorBibleMetadata = playlist.FirstOrDefault(t => t.PlayItem.Metadata.PlayType == PlayType.Bible)?.PlayItem.Metadata;

        if (stateManager.AnchorBibleMetadata != null)
        {
            try
            {
                // Pre-anchor is the Bible track immediately before the anchor (wrap-around enabled).
                var preAnchor = await playlistService.GetPreviousPlayItemAsync(stateManager.AnchorBibleMetadata);
                stateManager.PreAnchorBibleMetadata = preAnchor.Metadata;
            }
            catch
            {
                stateManager.PreAnchorBibleMetadata = null;
            }
        }
        else
        {
            stateManager.PreAnchorBibleMetadata = null;
        }
    }

    public void Receive(NextButtonPressedMessage message)
    {
        systemControlsHandler.HandleNextButton(() => PlayNextAsync());
    }

    public void Receive(PreviousButtonPressedMessage message)
    {
        systemControlsHandler.HandlePreviousButton(() => PlayPreviousAsync());
    }

    public void Receive(PlayButtonPressedMessage message)
    {
        systemControlsHandler.HandlePlayButton(async () =>
        {
            if (!stateManager.IsPreparingOrPlaying(audioPlayer) &&
                (stateManager.Playlist == null || stateManager.Playlist.Count == 0 || !stateManager.CurrentScheduleId.HasValue))
            {
                var defaultScheduleId = playbackState.Value.DefaultScheduleId;
                if (defaultScheduleId.HasValue && defaultScheduleId.Value > 0)
                {
                    logger.Information("Play button pressed with no active playback - starting default schedule {ScheduleId}", defaultScheduleId.Value);
                    await PrepareAndPlayAsync(defaultScheduleId.Value, isAlarm: false);
                    return;
                }
            }

            await PlayAsync();
        });
    }

    public void Receive(PauseButtonPressedMessage message)
    {
        systemControlsHandler.HandlePauseButton(() => PauseAsync());
    }

    public void Receive(SeekForwardButtonPressedMessage message)
    {
        systemControlsHandler.HandleSeekForwardButton(() => SeekForwardAsync());
    }

    public void Receive(SeekBackwardButtonPressedMessage message)
    {
        systemControlsHandler.HandleSeekBackwardButton(() => SeekBackwardAsync());
    }

    public async Task SeekForwardAsync()
    {
        await operationHandler.SeekForwardAsync(stateManager.IsPreparingOrPlaying(audioPlayer));
    }

    public async Task SeekBackwardAsync()
    {
        await operationHandler.SeekBackwardAsync(stateManager.IsPreparingOrPlaying(audioPlayer));
    }

    public async Task SeekToAsync(TimeSpan position)
    {
        await operationHandler.SeekToAsync(position, stateManager.IsPreparingOrPlaying(audioPlayer));
    }

    public async Task StopAsync() => await StopAsyncInternal(skipMarkAsPlayed: false);

    private async Task StopAsyncInternal(bool skipMarkAsPlayed, bool skipSaveLastPlayed = false)
    {
        var scheduleIdToSave = stateManager.CurrentScheduleId;

        TrackMetadata? trackMetadataToMark = null;
        if (!skipMarkAsPlayed && stateManager.Playlist != null && stateManager.CurrentTrackIndex >= 0 && stateManager.CurrentTrackIndex < stateManager.Playlist.Count)
        {
            trackMetadataToMark = stateManager.Playlist[stateManager.CurrentTrackIndex].PlayItem.Metadata;
        }

        await stopHandler.StopAsync(
            scheduleIdToSave,
            trackMetadataToMark,
            skipMarkAsPlayed,
            skipSaveLastPlayed,
            stateManager.PreparationCancellationTokenSource,
            () => stateManager.Reset(),
            () => progressTracker.Stop());
    }

    private async Task ResetAsync()
    {
        progressTracker.Stop();
        await audioPlayer.ResetAsync();

        stateManager.Reset();

        dispatcher.Dispatch(new PlaybackStoppedAction());

#if ANDROID || IOS
        dispatcher.Dispatch(new SetCarPlayScreenAction());
        logger.Debug("SetCarPlayScreenAction dispatched after playback reset");
#endif

        logger.Debug("Playback reset completed. Status: {Status}, ScheduleId: {ScheduleId}",
            audioPlayer.Status,
            stateManager.CurrentScheduleId);
    }

    public async Task ResetAndRetryAsync(int scheduleId)
    {
        logger.Information("ResetAndRetryAsync - resetting and retrying schedule {ScheduleId}", scheduleId);

        // Clear error message first
        dispatcher.Dispatch(new PlaybackErrorAction { ErrorMessage = null });

        // Reset internal state without dispatching PlaybackStoppedAction (keeps modal open)
        progressTracker.Stop();
        await audioPlayer.ResetAsync();
        stateManager.Reset();

        // Immediately prepare and play again with isAlarm=false (regular playback) - modal will update automatically as state changes
        await PrepareAndPlayAsync(scheduleId, isAlarm: false);
    }

    private async Task PlayCurrentTrackAsync(bool startFromBeginning = false)
    {
        if (stateManager.Playlist == null || stateManager.CurrentTrackIndex < 0 || stateManager.CurrentTrackIndex >= stateManager.Playlist.Count)
        {
            logger.Warning("Cannot play track: playlist is null or track index {TrackIndex} is out of range (playlist count: {PlaylistCount})",
                stateManager.CurrentTrackIndex,
                stateManager.Playlist?.Count ?? 0);
            return;
        }

        var track = stateManager.Playlist[stateManager.CurrentTrackIndex];

        // Ensure the track is downloaded/prepared before attempting to play.
        var prepared = await EnsureTrackPreparedAsync(track);
        if (!prepared)
        {
            await HandlePlaybackFailureAsync();
            return;
        }

        // Determine if this is the first or last track
        var isFirstTrack = stateManager.CurrentTrackIndex == 0;
        var isLastTrack = !stateManager.IsIndefinitePlayback &&
                          stateManager.Playlist != null &&
                          stateManager.CurrentTrackIndex == stateManager.Playlist.Count - 1;

        var success = await trackPlaybackHandler.PlayTrackAsync(
            track,
            stateManager.CurrentTrackIndex,
            isFirstTrack,
            isLastTrack,
            startFromBeginning,
            stateManager.CurrentScheduleId,
            () => stateManager.IsPreparingOrPlaying(audioPlayer),
            () => stateManager.Playlist,
            isPreparing => stateManager.IsPreparingTrack = isPreparing);

        if (!success)
        {
            // Track playback failed - handle failure
            await HandlePlaybackFailureAsync();
            return;
        }

        // Background: always try to download the next track while this track is playing.
        _ = Task.Run(async () =>
        {
            try
            {
                await PreDownloadNextTrackAsync();
            }
            catch
            {
                // Ignore background failures; on-demand download will still happen when needed.
            }
        });
    }

    private async Task<bool> IsIndefinitePlaybackAsync(int scheduleId, CancellationToken cancellationToken)
    {
        try
        {
            var schedule = await alarmScheduleService.GetScheduleByIdAsync(
                scheduleId,
                includeMusic: false,
                includeBiblePublication: false,
                cancellationToken);

            return schedule != null && schedule.NumberOfTracksToPlay <= 0;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to load schedule {ScheduleId} to determine indefinite playback; defaulting to finite", scheduleId);
            return false;
        }
    }

    private async Task<bool> EnsureTrackPreparedAsync(AudioPlayerTrack track)
    {
        if (!string.IsNullOrEmpty(track.Uri))
        {
            return true;
        }

        var token = stateManager.PreparationCancellationTokenSource?.Token ?? CancellationToken.None;

        try
        {
            // Show progress in the alarm modal while downloading a single track on-demand.
            SendSingleTrackPreparationProgress(loadedTracks: 0, totalTracks: 1, bytesDownloaded: 0, totalBytes: null);

            var prepared = await preparePlaybackService.PrepareSingleTrackWithProgressAsync(
                track.PlayItem,
                (bytesDownloaded, totalBytes) =>
                {
                    SendSingleTrackPreparationProgress(loadedTracks: 0, totalTracks: 1, bytesDownloaded: bytesDownloaded, totalBytes: totalBytes);
                },
                token);

            if (prepared == null || string.IsNullOrEmpty(prepared.Uri))
            {
                return false;
            }

            track.Uri = prepared.Uri;

            // Mark preparation complete (1/1)
            SendSingleTrackPreparationProgress(loadedTracks: 1, totalTracks: 1, bytesDownloaded: 1, totalBytes: 1);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to prepare track on-demand: {Url}", track.PlayItem?.Url ?? "Unknown");
            return false;
        }
    }

    private static void SendSingleTrackPreparationProgress(int loadedTracks, int totalTracks, long bytesDownloaded, long? totalBytes)
    {
        WeakReferenceMessenger.Default.Send(new PlaybackPreparationProgressMessage
        {
            LoadedTracks = loadedTracks,
            TotalTracks = totalTracks,
            CurrentTrackProgress = 0.0,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes,
            TotalBytesDownloaded = bytesDownloaded,
            TotalBytesExpected = totalBytes
        });
    }

    private async Task PreDownloadNextTrackAsync()
    {
        var playlist = stateManager.Playlist;
        if (playlist == null || playlist.Count == 0)
        {
            return;
        }

        var currentIndex = stateManager.CurrentTrackIndex;
        var nextIndex = currentIndex + 1;
        if (nextIndex < 0 || nextIndex >= playlist.Count)
        {
            // Indefinite playback: pre-extend and pre-download the next track if possible.
            if (stateManager.IsIndefinitePlayback)
            {
                var appended = await TryAppendNextTrackAsync();
                if (!appended)
                {
                    return;
                }

                nextIndex = currentIndex + 1;
                if (nextIndex < 0 || nextIndex >= playlist.Count)
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }

        var nextTrack = playlist[nextIndex];
        if (!string.IsNullOrEmpty(nextTrack.Uri))
        {
            return;
        }

        var token = stateManager.PreparationCancellationTokenSource?.Token ?? CancellationToken.None;
        try
        {
            var prepared = await preparePlaybackService.PrepareSingleTrackAsync(nextTrack.PlayItem, token);
            if (prepared != null && !string.IsNullOrEmpty(prepared.Uri))
            {
                nextTrack.Uri = prepared.Uri;
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore.
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Background pre-download for next track failed");
        }
    }

    private async void OnMediaEnded(object? sender, EventArgs e)
    {
        try
        {
            progressTracker.Stop();

            await eventHandler.HandleMediaEndedAsync(
                stateManager.Playlist,
                () => stateManager.CurrentTrackIndex,
                idx => stateManager.CurrentTrackIndex = idx,
                stateManager.CurrentScheduleId,
                stateManager.IsIndefinitePlayback,
                () => TryAppendNextTrackAsync(),
                startFromBeginning => PlayCurrentTrackAsync(startFromBeginning),
                skipMarkAsPlayed => StopAsyncInternal(skipMarkAsPlayed, false));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling media ended event");
        }
    }

    private async void OnMediaFailed(object? sender, EventArgs e)
    {
        try
        {
            var trackUri = stateManager.Playlist?[stateManager.CurrentTrackIndex]?.Uri ?? "Unknown";
            var trackUrl = stateManager.Playlist?[stateManager.CurrentTrackIndex]?.PlayItem?.Url ?? "Unknown";

            await eventHandler.HandleMediaFailedAsync(
                stateManager.Playlist,
                () => stateManager.CurrentTrackIndex,
                idx => stateManager.CurrentTrackIndex = idx,
                stateManager.CurrentScheduleId,
                stateManager.IsIndefinitePlayback,
                () => TryAppendNextTrackAsync(),
                trackUri,
                trackUrl,
                startFromBeginning => PlayCurrentTrackAsync(startFromBeginning),
                () => HandlePlaybackFailureAsync());
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling media failed event");
        }
    }

    private async Task<bool> TryAppendNextTrackAsync()
    {
        var playlist = stateManager.Playlist;
        if (playlist == null || playlist.Count == 0)
        {
            return false;
        }

        var currentIndex = stateManager.CurrentTrackIndex;
        if (currentIndex < 0 || currentIndex >= playlist.Count)
        {
            return false;
        }

        try
        {
            var currentMetadata = playlist[currentIndex].PlayItem.Metadata;
            var nextPlayItem = await ResolveNextPlayItemForSessionAsync(currentMetadata);

            // Append placeholder; Uri will be downloaded on-demand (or in background).
            var nextTrack = new AudioPlayerTrack
            {
                PlayItem = nextPlayItem,
                Uri = string.Empty
            };
            playlist.Add(nextTrack);

            // Opportunistically download the appended track in the background.
            _ = Task.Run(async () =>
            {
                try
                {
                    var token = stateManager.PreparationCancellationTokenSource?.Token ?? CancellationToken.None;
                    var prepared = await preparePlaybackService.PrepareSingleTrackAsync(nextPlayItem, token);
                    if (prepared != null && !string.IsNullOrEmpty(prepared.Uri))
                    {
                        nextTrack.Uri = prepared.Uri;
                    }
                }
                catch
                {
                    // Ignore background failures.
                }
            });

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to append next track for indefinite playback");
            return false;
        }
    }

    private async Task<bool> TryPrependPreviousTrackAsync()
    {
        var playlist = stateManager.Playlist;
        if (playlist == null || playlist.Count == 0)
        {
            return false;
        }

        var currentIndex = stateManager.CurrentTrackIndex;
        if (currentIndex < 0 || currentIndex >= playlist.Count)
        {
            return false;
        }

        try
        {
            var currentMetadata = playlist[currentIndex].PlayItem.Metadata;
            var prevPlayItem = await ResolvePreviousPlayItemForSessionAsync(currentMetadata);

            // Prepend placeholder; Uri will be downloaded on-demand (or in background).
            var prevTrack = new AudioPlayerTrack
            {
                PlayItem = prevPlayItem,
                Uri = string.Empty
            };

            playlist.Insert(0, prevTrack);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool HasMusicInjection =>
        stateManager.SessionMusicPlayItem != null &&
        stateManager.AnchorBibleMetadata != null &&
        stateManager.PreAnchorBibleMetadata != null;

    private static bool IsSameBibleTrack(TrackMetadata a, TrackMetadata b)
    {
        return a.PlayType == PlayType.Bible &&
               b.PlayType == PlayType.Bible &&
               a.LanguageCode == b.LanguageCode &&
               a.PublicationCode == b.PublicationCode &&
               a.SectionNumber == b.SectionNumber &&
               a.TrackNumber == b.TrackNumber;
    }

    private async Task<PlayItem> ResolveNextPlayItemForSessionAsync(TrackMetadata currentMetadata)
    {
        if (HasMusicInjection)
        {
            // If we are right before the anchor Bible track, inject the (single) music track.
            if (currentMetadata.PlayType == PlayType.Bible && IsSameBibleTrack(currentMetadata, stateManager.PreAnchorBibleMetadata!))
            {
                return stateManager.SessionMusicPlayItem!;
            }

            // If we're on the injected music track, next should be the anchor Bible track.
            if (currentMetadata.PlayType == PlayType.Music)
            {
                // Use the pre-anchor -> next to get the anchor in a stable way.
                return await playlistService.GetNextPlayItemAsync(stateManager.PreAnchorBibleMetadata!);
            }
        }

        return await playlistService.GetNextPlayItemAsync(currentMetadata);
    }

    private async Task<PlayItem> ResolvePreviousPlayItemForSessionAsync(TrackMetadata currentMetadata)
    {
        if (HasMusicInjection)
        {
            // If we're on the anchor Bible track, previous should be the (single) music track.
            if (currentMetadata.PlayType == PlayType.Bible && IsSameBibleTrack(currentMetadata, stateManager.AnchorBibleMetadata!))
            {
                return stateManager.SessionMusicPlayItem!;
            }

            // If we're on the music track, previous should be the Bible track right before the anchor.
            if (currentMetadata.PlayType == PlayType.Music)
            {
                return await playlistService.GetPreviousPlayItemAsync(stateManager.AnchorBibleMetadata!);
            }
        }

        return await playlistService.GetPreviousPlayItemAsync(currentMetadata);
    }


    private async Task HandlePlaybackFailureAsync()
    {
        await failureHandler.HandlePlaybackFailureAsync(
            stateManager.IsAlarm,
            stateManager.CurrentScheduleId,
            () => ResetAsync(),
            playlist => stateManager.Playlist = playlist,
            idx => stateManager.CurrentTrackIndex = idx,
            startFromBeginning => PlayCurrentTrackAsync(startFromBeginning));
    }

    private async Task TryPlayFallbackAlarmSoundAsync(int scheduleId, bool keepErrorMessage = false)
    {
        try
        {
            // Show alarm notification
            await notificationService.ShowNotificationAsync(scheduleId);

            // Try to get and play fallback alarm sound
            var fallbackTrack = await fallbackAlarmSoundService.GetFallbackAlarmTrackAsync();
            if (fallbackTrack is null)
            {
                logger.Warning("Failed to get fallback alarm track");
                // Update error message if fallback also fails
                dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = "Download failed. Check your internet connection."
                });
                return;
            }

            // Only clear error message if we're not keeping it (for alarm downloads, keep it visible)
            if (!keepErrorMessage)
            {
                dispatcher.Dispatch(new PlaybackErrorAction { ErrorMessage = null });
            }

            // Set up fallback track and play it
            stateManager.Playlist = [fallbackTrack];
            stateManager.CurrentTrackIndex = 0;
            stateManager.ManuallyVisitedTrackIndices.Clear();
            navigationManager.NotifyNavigationChanged(stateManager.Playlist, stateManager.CurrentTrackIndex);
            await PlayCurrentTrackAsync();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error playing fallback alarm sound");
            // Update error message if fallback fails
            dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = "Download failed. Check your internet connection."
            });
        }
    }

    public void Dispose()
    {
        progressTracker.Dispose();

        // Unsubscribe from AudioPlayer events
        audioPlayer.MediaEnded -= OnMediaEnded;
        audioPlayer.MediaFailed -= OnMediaFailed;

        // Unregister from messages
        WeakReferenceMessenger.Default.Unregister<NextButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PreviousButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PlayButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PauseButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<SeekForwardButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<SeekBackwardButtonPressedMessage>(this);

        // All injected services (_audioPlayer, _preparePlaybackService, _playlistService, 
        // _fallbackAlarmSoundService, _dispatcher, _notificationService) are singletons, 
        // so don't dispose them
    }
}

