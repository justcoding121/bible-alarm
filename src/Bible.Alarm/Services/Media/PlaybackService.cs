#nullable enable
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Actions.Schedule;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
namespace Bible.Alarm.Services.Media;

public sealed class PlaybackService : IPlaybackService, IRecipient<NextButtonPressedMessage>, IRecipient<PreviousButtonPressedMessage>, IRecipient<PlayButtonPressedMessage>, IRecipient<PauseButtonPressedMessage>, IRecipient<TogglePlayPauseMessage>, IRecipient<SeekForwardButtonPressedMessage>, IRecipient<SeekBackwardButtonPressedMessage>, IDisposable
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
    private readonly IMediaCacheService mediaCacheService;

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
    private readonly PlaybackIndefiniteResolver indefiniteResolver;
    private readonly PlaybackPlaylistExtender playlistExtender;
    private readonly TrackOnDemandPreparer trackOnDemandPreparer;
    private readonly PlaybackSessionContextInitializer sessionContextInitializer;
    private readonly PlaybackModeResolver modeResolver;
    private readonly PlaybackResetExecutor resetExecutor;
    private readonly PlaybackMediaEventAdapter mediaEventAdapter;
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
        IMediaCacheService mediaCacheService,
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
        this.mediaCacheService = mediaCacheService;
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
        indefiniteResolver = new PlaybackIndefiniteResolver(playlistService, logger);
        playlistExtender = new PlaybackPlaylistExtender(indefiniteResolver, logger);
        trackOnDemandPreparer = new TrackOnDemandPreparer(preparePlaybackService, mediaCacheService, logger);
        sessionContextInitializer = new PlaybackSessionContextInitializer(playlistService);
        modeResolver = new PlaybackModeResolver(alarmScheduleService, logger);
        resetExecutor = new PlaybackResetExecutor(progressTracker, audioPlayer, stateManager, dispatcher, logger);
        mediaEventAdapter = new PlaybackMediaEventAdapter(
            eventHandler, progressTracker, logger,
            () => stateManager.Playlist,
            () => stateManager.CurrentTrackIndex,
            idx => stateManager.CurrentTrackIndex = idx,
            () => stateManager.CurrentScheduleId,
            () => stateManager.IsIndefinitePlayback,
            () => TryAppendNextTrackAsync(),
            startFromBeginning => PlayCurrentTrackAsync(startFromBeginning),
            skipMarkAsPlayed => StopAsyncInternal(skipMarkAsPlayed, false),
            () => HandlePlaybackFailureAsync(),
            () => stateManager.ManualNavigationPending);

        progressTracker.SetSaveProgressCallback(() => progressTracker.SaveProgressAsync(
            stateManager.Playlist, stateManager.CurrentTrackIndex));

        WeakReferenceMessenger.Default.Register<NextButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<PreviousButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<PlayButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<PauseButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<TogglePlayPauseMessage>(this);
        WeakReferenceMessenger.Default.Register<SeekForwardButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<SeekBackwardButtonPressedMessage>(this);

        this.audioPlayer.MediaEnded += mediaEventAdapter.OnMediaEnded;
        this.audioPlayer.MediaFailed += mediaEventAdapter.OnMediaFailed;
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

            var lastPlayedAtUtc = DateTime.UtcNow;
            _ = Task.Run(async () =>
            {
                try
                {
                    await alarmScheduleService.UpdateScheduleByIdAsync(scheduleId, s => s.LastPlayedAtUtc = lastPlayedAtUtc);
                    dispatcher.Dispatch(new UpdateScheduleLastPlayedAction(scheduleId, lastPlayedAtUtc));
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to update LastPlayedAtUtc for schedule {ScheduleId}", scheduleId);
                }
            });

            stateManager.PreparationCancellationTokenSource?.Dispose();
            stateManager.PreparationCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = stateManager.PreparationCancellationTokenSource.Token;

            // Capture playback mode once for this session.
            stateManager.IsIndefinitePlayback = await modeResolver.IsIndefinitePlaybackAsync(scheduleId, cancellationToken);

            stateManager.Playlist = await initializer.PrepareTracksAsync(scheduleId, cancellationToken);

            if (stateManager.Playlist is null)
            {
                await PlaybackPreparationFailureHandler.HandleAsync(
                    scheduleId, isAlarm, "Track preparation cancelled or failed for schedule {ScheduleId}",
                    dispatcher, logger, TryPlayFallbackAlarmSoundAsync);
                return;
            }

            if (stateManager.Playlist.Count == 0)
            {
                await PlaybackPreparationFailureHandler.HandleAsync(
                    scheduleId, isAlarm, "No tracks prepared for schedule {ScheduleId}",
                    dispatcher, logger, TryPlayFallbackAlarmSoundAsync);
                return;
            }

            stateManager.CurrentTrackIndex = 0;
            stateManager.ManuallyVisitedTrackIndices.Clear();

            await sessionContextInitializer.InitializeAsync(
                stateManager.Playlist,
                p => stateManager.SessionMusicPlayItem = p,
                m => stateManager.AnchorBibleMetadata = m,
                m => stateManager.PreAnchorBibleMetadata = m);

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
        if (stateManager.IsPreparingOrPlaying(audioPlayer))
        {
            await progressTracker.SaveProgressAsync(stateManager.Playlist, stateManager.CurrentTrackIndex);
        }

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
            stopPlaybackAsync: () => StopAsyncInternal(skipMarkAsPlayed: true),
            handlePlaybackFailureAsync: () => HandlePlaybackFailureAsync());
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
            startFromBeginning => PlayCurrentTrackAsync(startFromBeginning),
            handlePlaybackFailureAsync: () => HandlePlaybackFailureAsync());
    }

    public void Receive(NextButtonPressedMessage message)
    {
        stateManager.ManualNavigationPending = true;
        systemControlsHandler.HandleNextButton(async () =>
        {
            try
            {
                await PlayNextAsync();
            }
            finally
            {
                ScheduleClearManualNavigationFlag();
            }
        });
    }

    public void Receive(PreviousButtonPressedMessage message)
    {
        stateManager.ManualNavigationPending = true;
        systemControlsHandler.HandlePreviousButton(async () =>
        {
            try
            {
                await PlayPreviousAsync();
            }
            finally
            {
                ScheduleClearManualNavigationFlag();
            }
        });
    }

    /// <summary>
    /// Clears the ManualNavigationPending flag after a delay. Both PlayNextAsync and MediaEnded run on the
    /// main thread, so MediaEnded is always dequeued AFTER PlayNextAsync completes. An immediate clear in
    /// finally would let the queued MediaEnded (from the old dummy track) see the flag as false and advance
    /// a second time. The delay keeps the flag true long enough for any queued MediaEnded to be suppressed.
    /// </summary>
    private void ScheduleClearManualNavigationFlag()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000);
            stateManager.ManualNavigationPending = false;
        });
    }

    public void Receive(PlayButtonPressedMessage message)
    {
        systemControlsHandler.HandlePlayButton(PlayOrStartDefaultAsync);
    }

    public void Receive(PauseButtonPressedMessage message)
    {
        systemControlsHandler.HandlePauseButton(() => PauseAsync());
    }

    public void Receive(TogglePlayPauseMessage message)
    {
        systemControlsHandler.HandleTogglePlayPause(async () =>
        {
            if (playbackState.Value.Status == PlayStatus.Playing)
            {
                await PauseAsync();
            }
            else
            {
                await PlayOrStartDefaultAsync();
            }
        });
    }

    private async Task PlayOrStartDefaultAsync()
    {
        if (!stateManager.IsPreparingOrPlaying(audioPlayer) &&
            (stateManager.Playlist == null || stateManager.Playlist.Count == 0 || !stateManager.CurrentScheduleId.HasValue))
        {
            var defaultScheduleId = PlaybackDefaultScheduleResolver.Resolve(playbackState, logger);
            if (defaultScheduleId.HasValue && defaultScheduleId.Value > 0)
            {
                logger.Information("Play pressed with no active playback - starting default schedule {ScheduleId}", defaultScheduleId.Value);
                await PrepareAndPlayAsync(defaultScheduleId.Value, isAlarm: false);
                return;
            }
        }

        await PlayAsync();
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

    private async Task ResetAsync() => await resetExecutor.ResetAsync();

    public async Task ResetAndRetryAsync(int scheduleId)
    {
        logger.Information("ResetAndRetryAsync - resetting and retrying schedule {ScheduleId}", scheduleId);
        await resetExecutor.ResetStateForRetryAsync();
        await PrepareAndPlayAsync(scheduleId, isAlarm: false);
    }

    private async Task PlayCurrentTrackAsync(bool startFromBeginning = false)
    {
        try
        {
            if (stateManager.Playlist == null || stateManager.CurrentTrackIndex < 0 || stateManager.CurrentTrackIndex >= stateManager.Playlist.Count)
            {
                logger.Warning("Cannot play track: playlist is null or track index {TrackIndex} is out of range (playlist count: {PlaylistCount})",
                    stateManager.CurrentTrackIndex,
                    stateManager.Playlist?.Count ?? 0);
                return;
            }

            var track = stateManager.Playlist[stateManager.CurrentTrackIndex];

            // Ensure the track has a playable URI (cached file or CDN URL for streaming).
            var token = stateManager.PreparationCancellationTokenSource?.Token ?? CancellationToken.None;
            var prepared = await trackOnDemandPreparer.EnsureTrackPreparedAsync(track, token);
            if (!prepared)
            {
                await HandlePlaybackFailureAsync();
                return;
            }

            var success = await trackPlaybackHandler.PlayTrackAsync(
            track,
            stateManager.CurrentTrackIndex,
            startFromBeginning,
            stateManager.CurrentScheduleId,
            () => stateManager.IsPreparingOrPlaying(audioPlayer),
            () => stateManager.Playlist,
            isPreparing => stateManager.IsPreparingTrack = isPreparing,
            stateManager.PlayedBibleTrackKeys);

            if (!success)
            {
                // Track playback failed - handle failure
                await HandlePlaybackFailureAsync();
                return;
            }

            // Fire-and-forget: pre-download the next track in background for caching + artwork.
            _ = Task.Run(async () =>
            {
                try
                {
                    await trackOnDemandPreparer.PreDownloadNextTrackAsync(
                        stateManager.Playlist!,
                        stateManager.CurrentTrackIndex,
                        stateManager.PreparationCancellationTokenSource?.Token ?? CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "Background pre-download of next track failed (non-critical)");
                }
            });
        }
        finally
        {
            dispatcher.Dispatch(new PlaybackTrackTransitionEndedAction());
        }
    }

    private IFetchProgress CreateSectionFetchProgressReporter()
    {
        var token = stateManager.PreparationCancellationTokenSource?.Token ?? CancellationToken.None;
        return new SectionFetchProgressReporter(token);
    }

    private Task<bool> TryAppendNextTrackAsync(bool reportSectionFetchProgress = true)
    {
        var playlist = stateManager.Playlist;
        if (playlist == null || playlist.Count == 0)
        {
            return Task.FromResult(false);
        }

        var sectionProgress = reportSectionFetchProgress ? CreateSectionFetchProgressReporter() : null;
        var token = stateManager.PreparationCancellationTokenSource?.Token ?? CancellationToken.None;

        return playlistExtender.TryAppendNextTrackAsync(
            playlist,
            stateManager.CurrentTrackIndex,
            stateManager.SessionMusicPlayItem,
            stateManager.AnchorBibleMetadata,
            stateManager.PreAnchorBibleMetadata,
            sectionProgress,
            token);
    }

    private Task<bool> TryPrependPreviousTrackAsync()
    {
        var playlist = stateManager.Playlist;
        if (playlist == null || playlist.Count == 0)
        {
            return Task.FromResult(false);
        }

        var sectionProgress = CreateSectionFetchProgressReporter();
        return playlistExtender.TryPrependPreviousTrackAsync(
            playlist,
            stateManager.CurrentTrackIndex,
            stateManager.CurrentScheduleId,
            stateManager.SessionMusicPlayItem,
            stateManager.AnchorBibleMetadata,
            stateManager.PreAnchorBibleMetadata,
            sectionProgress);
    }

    private async Task HandlePlaybackFailureAsync()
    {
        try
        {
            await failureHandler.HandlePlaybackFailureAsync(
                stateManager.IsAlarm,
                stateManager.CurrentScheduleId,
                () => ResetAsync(),
                playlist => stateManager.Playlist = playlist,
                idx => stateManager.CurrentTrackIndex = idx,
                startFromBeginning => PlayCurrentTrackAsync(startFromBeginning));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error in HandlePlaybackFailureAsync - ensuring error UI is shown");
            try
            {
                dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = "Media playback failed. Please try again."
                });
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Media playback failed. Please try again."));
                await resetExecutor.ResetAsync();
            }
            catch (Exception innerEx)
            {
                logger.Error(innerEx, "Fallback error handling also failed");
            }
        }
    }

    private Task TryPlayFallbackAlarmSoundAsync(int scheduleId, bool keepErrorMessage = false) =>
        failureHandler.TryPlayFallbackWhenPrepareFailedAsync(
            scheduleId,
            keepErrorMessage,
            playlist => stateManager.Playlist = playlist,
            idx => stateManager.CurrentTrackIndex = idx,
            () => stateManager.ManuallyVisitedTrackIndices.Clear(),
            (playlist, idx) => navigationManager.NotifyNavigationChanged(playlist, idx),
            PlayCurrentTrackAsync);

    public void Dispose()
    {
        progressTracker.Dispose();

        audioPlayer.MediaEnded -= mediaEventAdapter.OnMediaEnded;
        audioPlayer.MediaFailed -= mediaEventAdapter.OnMediaFailed;

        // Unregister from messages
        WeakReferenceMessenger.Default.Unregister<NextButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PreviousButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PlayButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PauseButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<TogglePlayPauseMessage>(this);
        WeakReferenceMessenger.Default.Unregister<SeekForwardButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<SeekBackwardButtonPressedMessage>(this);

        // All injected services (_audioPlayer, _preparePlaybackService, _playlistService, 
        // _fallbackAlarmSoundService, _dispatcher, _notificationService) are singletons, 
        // so don't dispose them
    }
}

