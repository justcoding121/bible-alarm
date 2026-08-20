#nullable enable
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
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
using Microsoft.Maui.Devices;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
namespace Bible.Alarm.Services.Media;

public sealed partial class PlaybackService : IPlaybackService, IRecipient<NextButtonPressedMessage>, IRecipient<PreviousButtonPressedMessage>, IRecipient<PlayButtonPressedMessage>, IRecipient<PauseButtonPressedMessage>, IRecipient<TogglePlayPauseMessage>, IRecipient<SeekForwardButtonPressedMessage>, IRecipient<SeekBackwardButtonPressedMessage>
{
    private readonly ILogger logger;
    private readonly IAudioPlayer audioPlayer;
    private readonly IAlarmScheduleService alarmScheduleService;
    private readonly IDispatcher dispatcher;
    private readonly INotificationService notificationService;
    private readonly IDefaultDeviceRingtoneService defaultDeviceRingtoneService;

    private readonly SemaphoreSlim stopLock = new(1, 1);
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
        IAlarmScheduleService alarmScheduleService,
        IDispatcher dispatcher,
        IState<PlaybackState> playbackState,
        PlaybackServiceInjectionContext injection)
    {
        this.logger = logger;
        this.audioPlayer = audioPlayer;
        this.alarmScheduleService = alarmScheduleService;
        this.dispatcher = dispatcher;
        this.notificationService = injection.NotificationService;
        this.defaultDeviceRingtoneService = injection.DefaultDeviceRingtoneService;
        this.playbackState = playbackState;

        var preparePlaybackService = injection.PreparePlaybackService;
        var playlistService = injection.PlaylistService;
        var fallbackAlarmSoundService = injection.FallbackAlarmSoundService;
        var mediaCacheService = injection.MediaCacheService;
        var cdnPlaybackUrlProbe = injection.CdnPlaybackUrlProbe;
        var trackCdnUrlRefresher = injection.TrackCdnUrlRefresher;

        stateManager = new PlaybackStateManager(logger);
        navigationManager = new PlaybackNavigationManager(dispatcher);
        progressTracker = new ProgressTracker(playlistService, audioPlayer, logger);
        operationHandler = new PlaybackOperationHandler(audioPlayer, dispatcher, logger, progressTracker);
        trackPreparationHandler = new TrackPreparationHandler(audioPlayer, playlistService, logger);
        eventHandler = new PlaybackEventHandler(
            playlistService,
            dispatcher,
            logger,
            navigationManager,
            cdnPlaybackUrlProbe,
            trackCdnUrlRefresher);
        failureHandler = new PlaybackFailureHandler(fallbackAlarmSoundService, notificationService, dispatcher, logger);
        initializer = new PlaybackInitializer(preparePlaybackService, dispatcher, logger);
        navigationHandler = new PlaybackNavigationHandler(audioPlayer, dispatcher, logger, progressTracker, navigationManager);
        stopHandler = new PlaybackStopHandler(audioPlayer, playlistService, dispatcher, logger);
        trackPlaybackHandler = new TrackPlaybackHandler(audioPlayer, logger, trackPreparationHandler, progressTracker);
        systemControlsHandler = new SystemControlsHandler(logger, injection.MainThreadScheduler);
        trackMarker = new TrackMarker(playlistService, logger);
        indefiniteResolver = new PlaybackIndefiniteResolver(playlistService);
        playlistExtender = new PlaybackPlaylistExtender(indefiniteResolver, logger);
        trackOnDemandPreparer = new TrackOnDemandPreparer(preparePlaybackService, mediaCacheService, logger);
        sessionContextInitializer = new PlaybackSessionContextInitializer(playlistService);
        modeResolver = new PlaybackModeResolver(alarmScheduleService, logger);
        resetExecutor = new PlaybackResetExecutor(progressTracker, audioPlayer, stateManager, dispatcher, logger);
        mediaEventAdapter = new PlaybackMediaEventAdapter(
            eventHandler,
            progressTracker,
            logger,
            new PlaybackMediaEventAdapter.Callbacks(
                () => stateManager.Playlist,
                () => stateManager.CurrentTrackIndex,
                idx => stateManager.CurrentTrackIndex = idx,
                () => stateManager.CurrentScheduleId,
                () => stateManager.IsIndefinitePlayback,
                () => TryAppendNextTrackAsync(),
                startFromBeginning => PlayCurrentTrackAsync(startFromBeginning),
                skipMarkAsPlayed => StopAsyncInternal(skipMarkAsPlayed, false),
                () => stateManager.ManualNavigationPending,
                () => stateManager.IsAlarm,
                async (msg, playRingtone) => await ShowPlaybackErrorInModalKeepSessionAsync(msg, playRingtone),
                idx => stateManager.IsPlaybackEstablishedForTrack(idx)));

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

    public bool IsAlarmPlaybackSession => stateManager.IsAlarm;

    public async Task PrepareAndPlayAsync(int scheduleId, bool isAlarm)
    {
        defaultDeviceRingtoneService.Stop();

        await StopOtherScheduleIfNeededAsync(scheduleId);

        if (await TryResumeSameScheduleAsync(scheduleId))
        {
            return;
        }

        await ForceResetIfStillPreparingAsync(scheduleId);
        await WaitForInFlightStopAsync();

        try
        {
            await PrepareAndPlayCoreAsync(scheduleId, isAlarm);
        }
        catch (OperationCanceledException oc)
        {
            logger.Debug(oc, AppConstants.Logging.PlaybackServiceDiagnosticsLog.PrepareAndPlayAsyncCancelledForSchedule, scheduleId);
        }
        catch (Exception ex)
        {
            await ResetAsync();
            throw new InvalidOperationException($"Error preparing and playing schedule {scheduleId}.", ex);
        }
    }

    private async Task StopOtherScheduleIfNeededAsync(int scheduleId)
    {
        if (!stateManager.IsPreparingOrPlaying(audioPlayer)
            || !stateManager.CurrentScheduleId.HasValue
            || stateManager.CurrentScheduleId.Value == scheduleId)
        {
            return;
        }

        logger.Information(AppConstants.Logging.PlaybackServiceDiagnosticsLog.StoppingExistingPlaybackBeforeStarting,
            stateManager.CurrentScheduleId.Value, scheduleId);

        WeakReferenceMessenger.Default.Send(new BeginStoppingPlaybackMessage());
        await Task.Delay(50);

        await trackMarker.MarkTrackAsPlayedAsync(stateManager.Playlist, stateManager.CurrentTrackIndex);
        await StopAsyncInternal(skipMarkAsPlayed: true, skipSaveLastPlayed: true);

        await Task.Delay(500);
    }

    private async Task<bool> TryResumeSameScheduleAsync(int scheduleId)
    {
        if (!stateManager.IsPreparingOrPlaying(audioPlayer) || stateManager.CurrentScheduleId != scheduleId)
        {
            return false;
        }

        if (audioPlayer.IsActuallyPlayingOrPaused)
        {
            logger.Information(AppConstants.Logging.PlaybackServiceDiagnosticsLog.ResumingPlaybackSameSchedule, scheduleId);
            await PlayAsync();
            return true;
        }

        logger.Warning(
            AppConstants.Logging.PlaybackServiceDiagnosticsLog.StalePlayerStateWillRePrepareWithSeek,
            scheduleId, audioPlayer.Status);

        await progressTracker.SaveProgressAsync(stateManager.Playlist, stateManager.CurrentTrackIndex, forceSave: true);
        stateManager.PlayedBibleTrackKeys.Clear();
        stateManager.ManuallyVisitedTrackIndices.Clear();
        stateManager.IsPreparingTrack = false;
        progressTracker.Stop();
        await audioPlayer.ResetAsync();
        stateManager.Playlist = null;
        stateManager.CurrentTrackIndex = -1;
        return false;
    }

    private async Task ForceResetIfStillPreparingAsync(int scheduleId)
    {
        if (!stateManager.IsPreparingOrPlaying(audioPlayer))
        {
            return;
        }

        logger.Warning(AppConstants.Logging.PlaybackServiceDiagnosticsLog.StateStillPlayingAfterStopForceResetting,
            stateManager.CurrentScheduleId,
            audioPlayer.Status,
            scheduleId);

        stateManager.Reset();
        try
        {
            await audioPlayer.StopAsync();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.PlaybackServiceDiagnosticsLog.ErrorForceStoppingPlayerDuringStateRecovery);
        }
    }

    private async Task WaitForInFlightStopAsync()
    {
        bool stopCompleted = await stopLock.WaitAsync(TimeSpan.FromSeconds(10));
        if (stopCompleted)
        {
            stopLock.Release();
        }
        else
        {
            logger.Warning(AppConstants.Logging.PlaybackServiceDiagnosticsLog.PrepareAndPlayTimedOutWaitingForInFlightStop);
        }
    }

    private async Task PrepareAndPlayCoreAsync(int scheduleId, bool isAlarm)
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
                logger.Warning(ex, AppConstants.Logging.PlaybackServiceDiagnosticsLog.FailedToUpdateLastPlayedAtUtc, scheduleId);
            }
        });

        stateManager.PreparationCancellationTokenSource?.Dispose();
        stateManager.PreparationCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = stateManager.PreparationCancellationTokenSource.Token;

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

        await PlayCurrentTrackAsync(cancellationToken: cancellationToken);

        await notificationService.ClearDeliveredNotificationAsync(scheduleId);
    }

    public async Task PlayAsync()
    {
        // If ExoPlayer actually lost its state (resource reclamation while paused,
        // or status silently changed to Stopped/Ended), clear PlayedBibleTrackKeys
        // so the fallback re-preparation in PlaybackOperationHandler can seek to
        // saved progress. Without this, the track key from the original playback
        // stays in the set, isFirstEncounter returns false, and seek is skipped.
        if (!audioPlayer.IsActuallyPlayingOrPaused
            && audioPlayer.Status is PlayStatus.Paused or PlayStatus.Stopped or PlayStatus.Ended)
        {
            logger.Warning(AppConstants.Logging.PlaybackServiceDiagnosticsLog.PlayAsyncStaleStateClearingPlayedBibleTrackKeys,
                audioPlayer.Status);
            stateManager.PlayedBibleTrackKeys.Clear();
        }

        await operationHandler.PlayAsync(
            stateManager.Playlist,
            stateManager.CurrentTrackIndex,
            () => PlayCurrentTrackAsync());
    }

    public async Task PauseAsync()
    {
        if (stateManager.IsPreparingOrPlaying(audioPlayer))
        {
            await progressTracker.SaveProgressAsync(stateManager.Playlist, stateManager.CurrentTrackIndex, forceSave: true);
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
        await navigationHandler.PlayNextAsync(new PlaybackNavigationNextRequest(
            stateManager.Playlist,
            () => stateManager.CurrentTrackIndex,
            idx => stateManager.CurrentTrackIndex = idx,
            stateManager.CurrentScheduleId,
            stateManager.IsIndefinitePlayback,
            () => TryAppendNextTrackAsync(),
            stateManager.ManuallyVisitedTrackIndices,
            idx => trackMarker.MarkTrackAsPlayedAsync(stateManager.Playlist, idx),
            startFromBeginning => PlayCurrentTrackAsync(startFromBeginning),
            () => StopAsyncInternal(skipMarkAsPlayed: true),
            () => HandlePlaybackFailureAsync()));
    }

    public async Task PlayPreviousAsync()
    {
        // Any manual next/prev interaction makes the playback session indefinite.
        stateManager.IsIndefinitePlayback = true;
        await navigationHandler.PlayPreviousAsync(new PlaybackNavigationPreviousRequest(
            stateManager.Playlist,
            () => stateManager.CurrentTrackIndex,
            idx => stateManager.CurrentTrackIndex = idx,
            stateManager.CurrentScheduleId,
            stateManager.IsIndefinitePlayback,
            () => TryPrependPreviousTrackAsync(),
            stateManager.ManuallyVisitedTrackIndices,
            idx => trackMarker.MarkTrackAsPlayedAsync(stateManager.Playlist, idx),
            startFromBeginning => PlayCurrentTrackAsync(startFromBeginning),
            () => HandlePlaybackFailureAsync()));
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

    public void Receive(SeekForwardButtonPressedMessage message)
    {
        systemControlsHandler.HandleSeekForwardButton(() => SeekForwardAsync());
    }

    public void Receive(SeekBackwardButtonPressedMessage message)
    {
        systemControlsHandler.HandleSeekBackwardButton(() => SeekBackwardAsync());
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

    private async Task PlayOrStartDefaultAsync()
    {
        if (!stateManager.IsPreparingOrPlaying(audioPlayer) &&
            (stateManager.Playlist == null || stateManager.Playlist.Count == 0 || !stateManager.CurrentScheduleId.HasValue))
        {
            var defaultScheduleId = PlaybackDefaultScheduleResolver.Resolve(playbackState, logger);
            if (defaultScheduleId.HasValue && defaultScheduleId.Value > 0)
            {
                logger.Information(AppConstants.Logging.PlaybackServiceDiagnosticsLog.PlayPressedStartingDefaultSchedule, defaultScheduleId.Value);
                WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = defaultScheduleId.Value });
                await PrepareAndPlayAsync(defaultScheduleId.Value, isAlarm: false);
                return;
            }
        }

        await PlayAsync();
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

    public async Task StopAsync()
    {
        WeakReferenceMessenger.Default.Send(new PlaybackExplicitStopMessage());
        await StopAsyncInternal(skipMarkAsPlayed: false);
    }

    public async Task StopForTeardownAsync()
    {
        await StopAsyncInternal(skipMarkAsPlayed: false, skipDispatchStopped: true);
    }

    private async Task StopAsyncInternal(bool skipMarkAsPlayed, bool skipSaveLastPlayed = false, bool skipDispatchStopped = false)
    {
        if (!await stopLock.WaitAsync(0))
        {
            logger.Warning(AppConstants.Logging.PlaybackServiceDiagnosticsLog.StopAsyncInternalAnotherStopInProgressSafetyNet);
            if (!skipDispatchStopped)
            {
                dispatcher.Dispatch(new PlaybackStoppedAction());
#if ANDROID || IOS
                dispatcher.Dispatch(new SetCarPlayScreenAction());
#endif
            }
            return;
        }

        CancellationTokenSource? safetyNetCts = null;
        try
        {
            defaultDeviceRingtoneService.Stop();

            var scheduleIdToSave = stateManager.CurrentScheduleId;

            TrackMetadata? trackMetadataToMark = null;
            if (!skipMarkAsPlayed && stateManager.Playlist != null && stateManager.CurrentTrackIndex >= 0 && stateManager.CurrentTrackIndex < stateManager.Playlist.Count)
            {
                trackMetadataToMark = stateManager.Playlist[stateManager.CurrentTrackIndex].PlayItem.Metadata;
            }

            if (!skipDispatchStopped)
            {
                safetyNetCts = new CancellationTokenSource();
                _ = DispatchStoppedAfterTimeoutAsync(safetyNetCts.Token);
            }

            await stopHandler.StopAsync(new PlaybackStopRequest(
                scheduleIdToSave,
                trackMetadataToMark,
                skipMarkAsPlayed,
                skipSaveLastPlayed,
                stateManager.PreparationCancellationTokenSource,
                () => stateManager.Reset(),
                () => progressTracker.Stop(),
                skipDispatchStopped));
        }
        finally
        {
            if (safetyNetCts is not null)
            {
                await safetyNetCts.CancelAsync();
                safetyNetCts.Dispose();
            }

            stopLock.Release();
        }
    }

    private async Task DispatchStoppedAfterTimeoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        logger.Warning(AppConstants.Logging.PlaybackServiceDiagnosticsLog.StopAsyncInternalTimedOut15sSafetyNet);
        dispatcher.Dispatch(new PlaybackStoppedAction());
#if ANDROID || IOS
        dispatcher.Dispatch(new SetCarPlayScreenAction());
#endif
    }

    private async Task ResetAsync() => await resetExecutor.ResetAsync();

    public async Task ResetAndRetryAsync(int scheduleId)
    {
        logger.Information(AppConstants.Logging.PlaybackServiceDiagnosticsLog.ResetAndRetryAsyncResettingSchedule, scheduleId);
        await resetExecutor.ResetStateForRetryAsync();
        await PrepareAndPlayAsync(scheduleId, isAlarm: false);
    }

    private async Task PlayCurrentTrackAsync(bool startFromBeginning = false, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stateManager.Playlist == null || stateManager.CurrentTrackIndex < 0 || stateManager.CurrentTrackIndex >= stateManager.Playlist.Count)
            {
                logger.Warning(AppConstants.Logging.PlaybackServiceDiagnosticsLog.CannotPlayTrackIndexOutOfRange,
                    stateManager.CurrentTrackIndex,
                    stateManager.Playlist?.Count ?? 0);
                return;
            }

            var track = stateManager.Playlist[stateManager.CurrentTrackIndex];

            cancellationToken.ThrowIfCancellationRequested();

            // Ensure the track has a playable URI (cached file or CDN URL for streaming).
            var prepared = await trackOnDemandPreparer.EnsureTrackPreparedAsync(track, cancellationToken);
            if (!prepared)
            {
                await HandlePlaybackFailureAsync();
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var success = await trackPlaybackHandler.PlayTrackAsync(new PlayTrackRequest(
                track,
                stateManager.CurrentTrackIndex,
                startFromBeginning,
                stateManager.CurrentScheduleId,
                () => stateManager.IsPreparingOrPlaying(audioPlayer),
                () => stateManager.Playlist,
                isPreparing => stateManager.IsPreparingTrack = isPreparing,
                stateManager.PlayedBibleTrackKeys,
                cancellationToken));

            if (!success)
            {
                await HandlePlaybackFailureAsync();
                return;
            }

            stateManager.NotifyPlaybackEstablishedForTrack(stateManager.CurrentTrackIndex);

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
                    logger.Debug(ex, AppConstants.Logging.PlaybackServiceDiagnosticsLog.BackgroundPreDownloadNextTrackFailedNonCritical);
                }
            }, cancellationToken);
        }
        finally
        {
            dispatcher.Dispatch(new PlaybackTrackTransitionEndedAction());
        }
    }

    private SectionFetchProgressReporter CreateSectionFetchProgressReporter()
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

    /// <summary>
    /// Stops audio and shows an error in the playback modal without clearing Fluxor schedule id (Retry stays available).
    /// </summary>
    private async Task ShowPlaybackErrorInModalKeepSessionAsync(string errorMessage, bool playDeviceRingtone)
    {
        try
        {
            if (stateManager.Playlist != null && stateManager.CurrentTrackIndex >= 0 && stateManager.CurrentTrackIndex < stateManager.Playlist.Count)
            {
                await progressTracker.SaveProgressAsync(stateManager.Playlist, stateManager.CurrentTrackIndex, forceSave: true);
            }

            progressTracker.Stop();
            await audioPlayer.ResetAsync();
            dispatcher.Dispatch(new SetAutoAdvancingAction(false));
            dispatcher.Dispatch(new PlaybackStatusChangedAction(PlayStatus.Failed));
            dispatcher.Dispatch(new PlaybackErrorAction { ErrorMessage = errorMessage });
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(errorMessage));
            if (playDeviceRingtone && DeviceInfo.Current.Platform == DevicePlatform.Android)
            {
                try
                {
                    defaultDeviceRingtoneService.StartLoopingAlarmRingtone();
                }
                catch (Exception ringEx)
                {
                    logger.Warning(ringEx, AppConstants.Logging.PlaybackServiceDiagnosticsLog.ShowPlaybackErrorCouldNotStartDeviceRingtone);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.PlaybackServiceDiagnosticsLog.ShowPlaybackErrorInModalKeepSessionFailed);
            try
            {
                dispatcher.Dispatch(new PlaybackErrorAction { ErrorMessage = errorMessage });
                WeakReferenceMessenger.Default.Send(new ShowToastMessage(errorMessage));
                await resetExecutor.ResetAsync();
            }
            catch (Exception innerEx)
            {
                logger.Error(innerEx, AppConstants.Logging.PlaybackServiceDiagnosticsLog.RecoveryAfterShowPlaybackErrorInModalKeepSessionFailed);
            }
        }
    }

    private async Task HandlePlaybackFailureAsync()
    {
        try
        {
            if (!stateManager.IsAlarm && stateManager.CurrentScheduleId.HasValue)
            {
                await ShowPlaybackErrorInModalKeepSessionAsync("Playback failed, tap Retry", playDeviceRingtone: false);
                return;
            }

            await failureHandler.HandlePlaybackFailureAsync(new PlaybackHandleFailureRequest(
                stateManager.IsAlarm,
                stateManager.CurrentScheduleId,
                () => ResetAsync(),
                playlist => stateManager.Playlist = playlist,
                idx => stateManager.CurrentTrackIndex = idx,
                startFromBeginning => PlayCurrentTrackAsync(startFromBeginning)));
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.PlaybackServiceDiagnosticsLog.ErrorInHandlePlaybackFailureAsyncEnsuringErrorUi);
            try
            {
                if (!stateManager.IsAlarm && stateManager.CurrentScheduleId.HasValue)
                {
                    await ShowPlaybackErrorInModalKeepSessionAsync(PlaybackUserFacingStrings.PlaybackFailedTapRetry, playDeviceRingtone: false);
                }
                else
                {
                    dispatcher.Dispatch(new PlaybackErrorAction
                    {
                        ErrorMessage = PlaybackUserFacingStrings.MediaPlaybackFailedPleaseTryAgainModal
                    });
                    WeakReferenceMessenger.Default.Send(new ShowToastMessage(PlaybackUserFacingStrings.MediaPlaybackFailedPleaseTryAgainToast));
                    await resetExecutor.ResetAsync();
                }
            }
            catch (Exception innerEx)
            {
                logger.Error(innerEx, AppConstants.Logging.PlaybackServiceDiagnosticsLog.FallbackErrorHandlingAlsoFailed);
            }
        }
    }

    private Task TryPlayFallbackAlarmSoundAsync(int scheduleId, bool keepErrorMessage = false) =>
        failureHandler.TryPlayFallbackWhenPrepareFailedAsync(new PlaybackPrepareFallbackRequest(
            scheduleId,
            keepErrorMessage,
            playlist => stateManager.Playlist = playlist,
            idx => stateManager.CurrentTrackIndex = idx,
            () => stateManager.ManuallyVisitedTrackIndices.Clear(),
            (playlist, idx) => navigationManager.NotifyNavigationChanged(playlist, idx),
            startFromBeginning => PlayCurrentTrackAsync(startFromBeginning)));

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        stopLock.Dispose();
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
