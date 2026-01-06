#nullable enable
using System.Timers;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Timer = System.Timers.Timer;

namespace Bible.Alarm.Services.Media;

public sealed class PlaybackService : IPlaybackService, IRecipient<NextButtonPressedMessage>, IRecipient<PreviousButtonPressedMessage>, IRecipient<PlayButtonPressedMessage>, IRecipient<PauseButtonPressedMessage>, IDisposable
{
    private readonly ILogger logger;
    private readonly IAudioPlayer audioPlayer;
    private readonly IPreparePlaybackService preparePlaybackService;
    private readonly IPlaylistService playlistService;
    private readonly IDispatcher dispatcher;
    private readonly IDisplayMetadataService displayMetadataService;
    private readonly IFallbackAlarmSoundService fallbackAlarmSoundService;
    private readonly INotificationService notificationService;

    // Helper classes for modular functionality
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

    public PlaybackService(
        ILogger logger,
        IAudioPlayer audioPlayer,
        IPreparePlaybackService preparePlaybackService,
        IPlaylistService playlistService,
        IFallbackAlarmSoundService fallbackAlarmSoundService,
        IDispatcher dispatcher,
        INotificationService notificationService,
        IDisplayMetadataService displayMetadataService
        )
    {
        this.logger = logger;
        this.audioPlayer = audioPlayer;
        this.preparePlaybackService = preparePlaybackService;
        this.playlistService = playlistService;
        this.dispatcher = dispatcher;
        this.displayMetadataService = displayMetadataService;
        this.fallbackAlarmSoundService = fallbackAlarmSoundService;
        this.notificationService = notificationService;

        // Initialize helper classes
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

        // Set up progress tracker callback
        progressTracker.SetSaveProgressCallback(() => progressTracker.SaveProgressAsync(
            stateManager.Playlist, stateManager.CurrentTrackIndex));

        // Register for Next/Previous/Play/Pause button press messages from system controls
        WeakReferenceMessenger.Default.Register<NextButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<PreviousButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<PlayButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<PauseButtonPressedMessage>(this);

        this.audioPlayer.MediaEnded += OnMediaEnded;
        this.audioPlayer.MediaFailed += OnMediaFailed;
    }

    public async Task PrepareAndPlayAsync(int scheduleId, bool isAlarm)
    {
        // If already playing a different schedule, stop it first
        if (stateManager.IsPreparingOrPlaying(audioPlayer) && stateManager.CurrentScheduleId.HasValue && stateManager.CurrentScheduleId.Value != scheduleId)
        {
            logger.Information("Stopping existing playback of schedule {CurrentScheduleId} before starting schedule {ScheduleId}",
                stateManager.CurrentScheduleId.Value, scheduleId);
            // Mark current track as played to advance TrackNumber, but skip saving "last played" since we're switching
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

            // Create cancellation token source for preparation (can be cancelled when stop is called)
            stateManager.PreparationCancellationTokenSource?.Dispose();
            stateManager.PreparationCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = stateManager.PreparationCancellationTokenSource.Token;

            // Prepare tracks using initializer
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
                // Keep modal open by not resetting - the state will keep isPreparingOrPlaying true
                // Set status to Failed to indicate error state
                dispatcher.Dispatch(new PlaybackStatusChangedAction(PlayStatus.Failed));
                
                // For alarms, also try to play fallback alarm sound (keep error message visible)
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
                // Keep modal open by not resetting - the state will keep isPreparingOrPlaying true
                // Set status to Failed to indicate error state
                dispatcher.Dispatch(new PlaybackStatusChangedAction(PlayStatus.Failed));
                
                // For alarms, also try to play fallback alarm sound (keep error message visible)
                if (isAlarm)
                {
                    await TryPlayFallbackAlarmSoundAsync(scheduleId, keepErrorMessage: true);
                }
                
                return;
            }

            stateManager.CurrentTrackIndex = 0;
            stateManager.ManuallyVisitedTrackIndices.Clear();

            // Dispatch navigation state immediately after setting currentTrackIndex
            // This ensures CanPlayNext and CanPlayPrevious are correct before Android Auto processes status changes
            // This prevents the "prev button only" flicker on initial play
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
        await navigationHandler.PlayNextAsync(
            stateManager.Playlist,
            () => stateManager.CurrentTrackIndex,
            idx => stateManager.CurrentTrackIndex = idx,
            stateManager.CurrentScheduleId,
            stateManager.ManuallyVisitedTrackIndices,
            idx => trackMarker.MarkTrackAsPlayedAsync(stateManager.Playlist, idx),
            startFromBeginning => PlayCurrentTrackAsync(startFromBeginning));
    }

    public async Task PlayPreviousAsync()
    {
        await navigationHandler.PlayPreviousAsync(
            stateManager.Playlist,
            () => stateManager.CurrentTrackIndex,
            idx => stateManager.CurrentTrackIndex = idx,
            stateManager.CurrentScheduleId,
            stateManager.ManuallyVisitedTrackIndices,
            idx => trackMarker.MarkTrackAsPlayedAsync(stateManager.Playlist, idx),
            startFromBeginning => PlayCurrentTrackAsync(startFromBeginning));
    }

    /// <summary>
    /// Handles Next button press message from Android system media controls (notification/lockscreen).
    /// Calls PlayNextAsync() on UI thread with a delay to let MediaSession finish processing.
    /// </summary>
    public void Receive(NextButtonPressedMessage message)
    {
        systemControlsHandler.HandleNextButton(() => PlayNextAsync());
    }

    /// <summary>
    /// Handles Previous button press message from Android system media controls (notification/lockscreen).
    /// Calls PlayPreviousAsync() on UI thread with a delay to let MediaSession finish processing.
    /// </summary>
    public void Receive(PreviousButtonPressedMessage message)
    {
        systemControlsHandler.HandlePreviousButton(() => PlayPreviousAsync());
    }

    /// <summary>
    /// Handles Play button press message from system media controls (notification/lockscreen).
    /// Calls PlayAsync() on UI thread with a delay to let system controls finish processing.
    /// </summary>
    public void Receive(PlayButtonPressedMessage message)
    {
        systemControlsHandler.HandlePlayButton(() => PlayAsync());
    }

    /// <summary>
    /// Handles Pause button press message from system media controls (notification/lockscreen).
    /// Calls PauseAsync() on UI thread with a delay to let system controls finish processing.
    /// </summary>
    public void Receive(PauseButtonPressedMessage message)
    {
        systemControlsHandler.HandlePauseButton(() => PauseAsync());
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
        // Save currentScheduleId before resetting state (needed for SaveLastPlayed)
        var scheduleIdToSave = stateManager.CurrentScheduleId;

        // Save track metadata before resetting state (needed for marking track as played/finished)
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

        // Dispatch playback stopped action
        dispatcher.Dispatch(new PlaybackStoppedAction());

#if ANDROID
        // Dispatch SetCarPlayScreenAction to refresh Android Auto with default schedule metadata
        dispatcher.Dispatch(new SetCarPlayScreenAction());
        logger.Debug("SetCarPlayScreenAction dispatched after playback reset");
#endif

        // Log reset completion for debugging
        logger.Debug("Playback reset completed. Status: {Status}, ScheduleId: {ScheduleId}",
            audioPlayer.Status,
            stateManager.CurrentScheduleId);
    }

    /// <summary>
    /// Resets playback state without closing the modal, then immediately prepares and plays again.
    /// Used for retry functionality - modal stays open and updates automatically.
    /// Always uses isAlarm=false for retry (regular playback, not alarm behavior).
    /// </summary>
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

        // Determine if this is the first or last track
        var isFirstTrack = stateManager.CurrentTrackIndex == 0;
        var isLastTrack = stateManager.Playlist != null && stateManager.CurrentTrackIndex == stateManager.Playlist.Count - 1;

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

        // All injected services (_audioPlayer, _preparePlaybackService, _playlistService, 
        // _fallbackAlarmSoundService, _dispatcher, _notificationService) are singletons, 
        // so don't dispose them
    }
}

