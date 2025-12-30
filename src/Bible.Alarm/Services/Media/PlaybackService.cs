#nullable enable
using System.Timers;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Timer = System.Timers.Timer;

namespace Bible.Alarm.Services.Media;

public sealed class PlaybackService : IPlaybackService, IRecipient<NextButtonPressedMessage>, IRecipient<PreviousButtonPressedMessage>, IDisposable
{
    private readonly ILogger logger;
    private readonly IAudioPlayer audioPlayer;
    private readonly IPreparePlaybackService preparePlaybackService;
    private readonly IPlaylistService playlistService;
    private readonly IFallbackAlarmSoundService fallbackAlarmSoundService;
    private readonly IDispatcher dispatcher;
    private readonly INotificationService notificationService;
    private readonly IDisplayMetadataService displayMetadataService;

    private List<AudioPlayerTrack>? playlist;
    private int currentTrackIndex = -1;
    private int? currentScheduleId;
    private bool isAlarm;
    private readonly Timer? progressSaveTimer;
    private readonly HashSet<int> manuallyVisitedTrackIndices = [];
    private CancellationTokenSource? preparationCancellationTokenSource;
    private bool isPreparingTrack = false; // Track if we're in the middle of preparing a track

    private bool IsPreparingOrPlayingInternal
    {
        get
        {
            // If we're actively preparing a track, always return true
            // This prevents race conditions where state hasn't transitioned yet
            if (isPreparingTrack)
            {
                return true;
            }

            var isActuallyPlaying = audioPlayer.IsActuallyPlayingOrPaused;
            var status = audioPlayer.Status;
            return isActuallyPlaying ||
                   status == PlayStatus.Loading ||
                   status == PlayStatus.Playing ||
                   status == PlayStatus.Paused;
        }
    }

    private bool CanPlayNextInternal =>
        playlist is not null &&
        currentTrackIndex >= 0 &&
        currentTrackIndex < playlist.Count - 1;

    private bool CanPlayPreviousInternal =>
        playlist is not null &&
        currentTrackIndex > 0;

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
        this.fallbackAlarmSoundService = fallbackAlarmSoundService;
        this.dispatcher = dispatcher;
        this.notificationService = notificationService;
        this.displayMetadataService = displayMetadataService;

        // Register for Next/Previous button press messages from Android system controls
        WeakReferenceMessenger.Default.Register<NextButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<PreviousButtonPressedMessage>(this);

        this.audioPlayer.MediaEnded += OnMediaEnded;
        this.audioPlayer.MediaFailed += OnMediaFailed;

        progressSaveTimer = new(1000);
        progressSaveTimer.Elapsed += OnProgressSaveTimerElapsed;
        progressSaveTimer.AutoReset = true;
    }

    public async Task PrepareAndPlayAsync(int scheduleId, bool isAlarm)
    {
        // If already playing a different schedule, stop it first
        if (IsPreparingOrPlayingInternal && currentScheduleId.HasValue && currentScheduleId.Value != scheduleId)
        {
            logger.Information("Stopping existing playback of schedule {CurrentScheduleId} before starting schedule {ScheduleId}",
                currentScheduleId.Value, scheduleId);
            // Mark current track as played to advance TrackNumber, but skip saving "last played" since we're switching
            await MarkCurrentTrackAsPlayedAsync();
            await StopAsyncInternal(skipMarkAsPlayed: true, skipSaveLastPlayed: true);
        }

        if (IsPreparingOrPlayingInternal)
        {
            logger.Warning("Cannot prepare and play schedule {ScheduleId} - already preparing or playing schedule {CurrentScheduleId}. Status: {Status}",
                scheduleId,
                currentScheduleId,
                audioPlayer.Status);
            return;
        }

        // Modal visibility is now handled reactively via PlaybackState subscription in App.xaml.cs
        // No need to send ShowAlarmModalMessage here

        try
        {
            currentScheduleId = scheduleId;
            this.isAlarm = isAlarm;

            // Dispatch playback started action BEFORE preparing tracks so modal opens
            // This ensures the modal is visible when we show error messages or play fallback
            dispatcher.Dispatch(new PlaybackStartedAction(scheduleId));

            // Set auto-advancing flag for initial play to prevent button flicker during preparation
            // This keeps the pause button visible during Loading state transitions
            logger.Information(
                "[PlaybackService] PrepareAndPlayAsync: Dispatching SetAutoAdvancingAction(true) for initial play - ScheduleId={ScheduleId}",
                scheduleId);
            dispatcher.Dispatch(new SetAutoAdvancingAction(true));

            // Immediately set loading/buffering status for any new schedule start (phone tap or Android Auto play).
            // This keeps UI/Android Auto from showing "idle" controls with empty metadata while we prepare tracks.
            logger.Information(
                "[PlaybackService] PrepareAndPlayAsync: Dispatching PlaybackStatusChangedAction(Loading) - ScheduleId={ScheduleId}",
                scheduleId);
            dispatcher.Dispatch(new PlaybackStatusChangedAction(PlayStatus.Loading));

            // Create cancellation token source for preparation (can be cancelled when stop is called)
            preparationCancellationTokenSource?.Dispose();
            preparationCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = preparationCancellationTokenSource.Token;

            // Run track preparation (including downloads) on background thread to avoid blocking main thread
            // This ensures UI remains responsive during download/preparation phase
            logger.Debug("Starting track preparation on background thread for schedule {ScheduleId}", scheduleId);
            try
            {
                playlist = await Task.Run(async () => await preparePlaybackService.PrepareTracksAsync(scheduleId, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                logger.Information("Track preparation cancelled for schedule {ScheduleId}", scheduleId);
                await ResetAsync();
                return;
            }

            if (playlist is null)
            {
                logger.Warning("Failed to prepare tracks for schedule {ScheduleId}", scheduleId);
                await HandlePlaybackFailureAsync();
                return;
            }

            if (playlist.Count == 0)
            {
                logger.Warning("No tracks prepared for schedule {ScheduleId}", scheduleId);
                await ResetAsync();
                return;
            }

            currentTrackIndex = 0;
            manuallyVisitedTrackIndices.Clear();

            // Dispatch navigation state immediately after setting currentTrackIndex
            // This ensures CanPlayNext and CanPlayPrevious are correct before Android Auto processes status changes
            // This prevents the "prev button only" flicker on initial play
            NotifyNavigationChanged();

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
        if (currentTrackIndex < 0 || playlist is null || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        if (audioPlayer.Status == PlayStatus.Paused)
        {
            await audioPlayer.ResumeAsync();
            StartProgressTimerIfBibleTrack();
        }
        else if (audioPlayer.Status is PlayStatus.Stopped or PlayStatus.Ended)
        {
            await PlayCurrentTrackAsync();
        }
        else
        {
            await audioPlayer.PlayAsync();
            StartProgressTimerIfBibleTrack();
        }
    }

    public async Task PauseAsync()
    {
        if (IsPreparingOrPlayingInternal)
        {
            progressSaveTimer?.Stop();
            await audioPlayer.PauseAsync();
            // Clear auto-advancing flag when user manually pauses
            logger.Information(
                "[PlaybackService] PauseAsync: Dispatching SetAutoAdvancingAction(false) - ScheduleId={ScheduleId}, TrackIndex={TrackIndex}",
                currentScheduleId,
                currentTrackIndex);
            dispatcher.Dispatch(new SetAutoAdvancingAction(false));
        }
    }

    public async Task PlayNextAsync()
    {
        if (playlist is null || playlist.Count == 0)
        {
            return;
        }

        if (currentTrackIndex < playlist.Count - 1)
        {
            progressSaveTimer?.Stop();
            await audioPlayer.StopAsync();
            await MarkCurrentTrackAsPlayedAsync();
            // Set auto-advancing flag for smooth transition during manual navigation
            logger.Information(
                "[PlaybackService] PlayNextAsync: Dispatching SetAutoAdvancingAction(true) for manual next - ScheduleId={ScheduleId}, FromTrackIndex={FromTrackIndex}, ToTrackIndex={ToTrackIndex}",
                currentScheduleId,
                currentTrackIndex,
                currentTrackIndex + 1);
            dispatcher.Dispatch(new SetAutoAdvancingAction(true));
            currentTrackIndex++;

            // Dispatch navigation state immediately after setting currentTrackIndex
            // This ensures CanPlayNext and CanPlayPrevious are correct before Android Auto processes status changes
            // This prevents button flicker during track transitions
            NotifyNavigationChanged();

            // If we've already manually visited this track, start from beginning
            // Otherwise, allow resume from saved position (for Bible tracks)
            var startFromBeginning = manuallyVisitedTrackIndices.Contains(currentTrackIndex);
            manuallyVisitedTrackIndices.Add(currentTrackIndex);

            await PlayCurrentTrackAsync(startFromBeginning: startFromBeginning);
        }
    }

    public async Task PlayPreviousAsync()
    {
        if (playlist is null || playlist.Count == 0)
        {
            return;
        }

        if (currentTrackIndex > 0)
        {
            // Go to previous track
            progressSaveTimer?.Stop();
            await audioPlayer.StopAsync();
            await MarkCurrentTrackAsPlayedAsync();
            // Set auto-advancing flag for smooth transition during manual navigation
            logger.Information(
                "[PlaybackService] PlayPreviousAsync: Dispatching SetAutoAdvancingAction(true) for manual previous - ScheduleId={ScheduleId}, FromTrackIndex={FromTrackIndex}, ToTrackIndex={ToTrackIndex}",
                currentScheduleId,
                currentTrackIndex,
                currentTrackIndex - 1);
            dispatcher.Dispatch(new SetAutoAdvancingAction(true));
            currentTrackIndex--;

            // Dispatch navigation state immediately after setting currentTrackIndex
            // This ensures CanPlayNext and CanPlayPrevious are correct before Android Auto processes status changes
            // This prevents button flicker during track transitions
            NotifyNavigationChanged();

            // Previous button always starts from beginning
            manuallyVisitedTrackIndices.Add(currentTrackIndex);
            await PlayCurrentTrackAsync(startFromBeginning: true);
        }
        else if (currentTrackIndex == 0)
        {
            // On first track - restart current track from beginning
            progressSaveTimer?.Stop();
            await audioPlayer.StopAsync();
            manuallyVisitedTrackIndices.Add(currentTrackIndex);
            await PlayCurrentTrackAsync(startFromBeginning: true);
            // Don't call NotifyNavigationChanged() - we're still on the same track
        }
    }


    private void NotifyNavigationChanged()
    {
        var canPlayNext = CanPlayNextInternal;
        var canPlayPrevious = CanPlayPreviousInternal;

        // Dispatch Fluxor action
        dispatcher.Dispatch(new PlaybackNavigationChangedAction(canPlayNext, canPlayPrevious));
    }

    /// <summary>
    /// Handles Next button press message from Android system media controls (notification/lockscreen).
    /// Calls PlayNextAsync() on UI thread with a delay to let MediaSession finish processing.
    /// </summary>
    public void Receive(NextButtonPressedMessage message)
    {
        logger.Debug("Next button pressed from system controls - calling PlayNextAsync");
        // Add delay to let MediaSession finish processing the button press
        // This prevents IllegalStateException when ExoPlayer is transitioning
        Task.Run(async () =>
        {
            // Delay to let MediaSession finish
            await Task.Delay(150);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await PlayNextAsync();
            });
        });
    }

    /// <summary>
    /// Handles Previous button press message from Android system media controls (notification/lockscreen).
    /// Calls PlayPreviousAsync() on UI thread with a delay to let MediaSession finish processing.
    /// </summary>
    public void Receive(PreviousButtonPressedMessage message)
    {
        logger.Debug("Previous button pressed from system controls - calling PlayPreviousAsync");
        // Add delay to let MediaSession finish processing the button press
        // This prevents IllegalStateException when ExoPlayer is transitioning
        Task.Run(async () =>
        {
            // Delay to let MediaSession finish
            await Task.Delay(150);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await PlayPreviousAsync();
            });
        });
    }

    public async Task SeekForwardAsync()
    {
        if (!IsPreparingOrPlayingInternal)
        {
            return;
        }

        var currentPosition = audioPlayer.CurrentPosition;
        if (!currentPosition.HasValue)
        {
            return;
        }

        var duration = audioPlayer.Duration;

        // Don't seek if duration is not yet loaded or is zero
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var newPosition = currentPosition.Value.Add(TimeSpan.FromSeconds(15));

        // Clamp to duration - seeking to duration will trigger MediaEnded
        // which will advance to next track, which is the desired behavior
        if (newPosition >= duration)
        {
            newPosition = duration;
        }

        await audioPlayer.SeekToAsync(newPosition);
    }

    public async Task SeekBackwardAsync()
    {
        if (!IsPreparingOrPlayingInternal)
        {
            return;
        }

        var currentPosition = audioPlayer.CurrentPosition;
        if (!currentPosition.HasValue)
        {
            return;
        }

        var newPosition = currentPosition.Value.Subtract(TimeSpan.FromSeconds(15));

        // Clamp to zero - can't seek before the start
        if (newPosition < TimeSpan.Zero)
        {
            newPosition = TimeSpan.Zero;
        }

        await audioPlayer.SeekToAsync(newPosition);
    }

    public async Task SeekToAsync(TimeSpan position)
    {
        if (!IsPreparingOrPlayingInternal)
        {
            return;
        }

        // Clamp position to valid range (0 to duration)
        var duration = audioPlayer.Duration;
        if (duration.TotalSeconds > 0 && position > duration)
        {
            position = duration;
        }
        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        await audioPlayer.SeekToAsync(position);
    }

    public async Task StopAsync() => await StopAsyncInternal(skipMarkAsPlayed: false);

    private async Task StopAsyncInternal(bool skipMarkAsPlayed, bool skipSaveLastPlayed = false)
    {
        logger.Information("StopAsync called - stopping alarm completely");

        // Save currentScheduleId before resetting state (needed for SaveLastPlayed)
        var scheduleIdToSave = currentScheduleId;

        // Save track metadata before resetting state (needed for marking track as played/finished)
        TrackMetadata? trackMetadataToMark = null;
        if (!skipMarkAsPlayed && playlist != null && currentTrackIndex >= 0 && currentTrackIndex < playlist.Count)
        {
            trackMetadataToMark = playlist[currentTrackIndex].PlayItem.Metadata;
        }

        // Cancel any ongoing preparation/downloads
        try
        {
            preparationCancellationTokenSource?.Cancel();
            logger.Debug("Cancelled preparation cancellation token");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error cancelling preparation token");
        }

        // Reset state early to ensure PlayCurrentTrackAsync checks detect stop immediately
        // This is especially important for the gap between downloads completing and playback starting
        // Note: We save currentScheduleId and trackMetadataToMark above before resetting
        ResetState();

        // Stop progress timer first to prevent it from trying to save progress after ServiceProvider is disposed
        progressSaveTimer?.Stop();

        // Stop player immediately for responsive user experience
        try
        {
            await audioPlayer.StopAsync();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error stopping player, will continue with reset");
        }

        // Skip marking as played if track was already marked as finished (e.g., when last track ends naturally)
        // This prevents overwriting the database update that MarkTrackAsFinished() already made
        if (!skipMarkAsPlayed && trackMetadataToMark != null)
        {
            try
            {
                // For music tracks, mark as finished (which advances to next track unless repeat is enabled)
                // This matches the behavior when a track finishes naturally
                // For Bible tracks, mark as played (which saves current position)
                if (trackMetadataToMark.PlayType == PlayType.Music)
                {
                    await playlistService.MarkTrackAsFinished(trackMetadataToMark);
                }
                else
                {
                    await playlistService.MarkTrackAsPlayed(trackMetadataToMark);
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error marking current track as played/finished");
            }
        }

        if (scheduleIdToSave.HasValue && !skipSaveLastPlayed)
        {
            try
            {
                await playlistService.SaveLastPlayed(scheduleIdToSave.Value);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error saving last played");
            }
        }

        // Reset player - state was already reset above, but ensure player is fully reset
        // This resets the player and dispatches actions to close modal
        try
        {
            await audioPlayer.ResetAsync();
            // Dispatch playback stopped action to close modal (state was already reset above)
            dispatcher.Dispatch(new PlaybackStoppedAction());
#if ANDROID
            // Dispatch SetCarPlayScreenAction to refresh Android Auto with default schedule metadata
            dispatcher.Dispatch(new SetCarPlayScreenAction());
            logger.Debug("SetCarPlayScreenAction dispatched after playback reset");
#endif
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error in audioPlayer.ResetAsync, attempting minimal cleanup");
            // Ensure modal closes even if ResetAsync fails
            try
            {
                await audioPlayer.ResetAsync();
            }
            catch (Exception resetEx)
            {
                logger.Warning(resetEx, "Error resetting player in fallback");
            }
            // State was already reset above, just dispatch action to close modal
            dispatcher.Dispatch(new PlaybackStoppedAction());
        }

        // Modal visibility is now handled reactively via PlaybackState subscription in App.xaml.cs
        // No need to send HideAlarmModalMessage here
    }

    private async Task ResetAsync()
    {
        progressSaveTimer?.Stop();
        await audioPlayer.ResetAsync();

        ResetState();

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
            currentScheduleId);
    }

    private void ResetState()
    {
        currentScheduleId = null;
        playlist = null;
        currentTrackIndex = -1;
        isAlarm = false;
        manuallyVisitedTrackIndices.Clear();

        // Dispose cancellation token source
        try
        {
            preparationCancellationTokenSource?.Dispose();
            preparationCancellationTokenSource = null;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error disposing preparation cancellation token source");
        }
    }

    private async Task PlayCurrentTrackAsync(bool startFromBeginning = false)
    {
        if (playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            logger.Warning("Cannot play track: playlist is null or track index {TrackIndex} is out of range (playlist count: {PlaylistCount})",
                currentTrackIndex,
                playlist?.Count ?? 0);
            return;
        }

        var track = playlist[currentTrackIndex];

        if (string.IsNullOrEmpty(track.Uri))
        {
            logger.Error("Cannot play track at index {TrackIndex}: URI is null or empty. URL: {TrackUrl}",
                currentTrackIndex,
                track.PlayItem?.Url ?? "Unknown");
            await HandlePlaybackFailureAsync();
            return;
        }

        logger.Debug("Preparing to play track at index {TrackIndex}. URI: {TrackUri}, URL: {TrackUrl}",
            currentTrackIndex,
            track.Uri,
            track.PlayItem?.Url ?? "Unknown");

        // Determine if this is the first or last track
        var isFirstTrack = currentTrackIndex == 0;
        var isLastTrack = playlist != null && currentTrackIndex == playlist.Count - 1;

        // Mark that we're preparing a track to prevent race conditions in IsPreparingOrPlayingInternal
        isPreparingTrack = true;
        try
        {
            await audioPlayer.PrepareAsync(track, isFirstTrack, isLastTrack);

            // On iOS, MediaElement may need a brief moment after PrepareAsync before it can play
            // Wait for the media to be in a ready state (not None or Failed)
            // This also gives time for the state to transition from "Opening" to "Paused"
            await WaitForMediaReadyAsync();

            // Check if stop was called during PrepareAsync or WaitForMediaReadyAsync
            // This ensures stop works correctly in the gap between downloads and playback
            // Note: isPreparingTrack flag ensures IsPreparingOrPlayingInternal returns true during this check
            if (!IsPreparingOrPlayingInternal || playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
            {
                logger.Information("Playback was stopped during PrepareAsync/WaitForMediaReadyAsync - aborting PlayCurrentTrackAsync");
                return;
            }
        }
        finally
        {
            // Clear the flag after preparation is complete (whether successful or not)
            isPreparingTrack = false;
        }

        // Check again after WaitForMediaReadyAsync in case stop was called during the wait
        if (!IsPreparingOrPlayingInternal || playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            logger.Information("Playback was stopped during WaitForMediaReadyAsync - aborting PlayCurrentTrackAsync");
            return;
        }

        // Seek to saved position for Bible tracks if resume is enabled
        // But always start from beginning if startFromBeginning is true (e.g., when going to previous track)
        if (!startFromBeginning
            && track.PlayItem?.Metadata != null
            && track.PlayItem.Metadata.PlayType == PlayType.Bible
            && track.PlayItem.Metadata.FinishedDuration != TimeSpan.Zero)
        {
            var shouldResume = await ShouldResumeFromLastPositionAsync();
            if (shouldResume)
            {
                try
                {
                    await audioPlayer.SeekToAsync(track.PlayItem.Metadata.FinishedDuration);
                }
                catch (InvalidOperationException ex)
                {
                    // Seek may fail if player isn't ready yet (seekable ranges not available)
                    // This is okay - we'll just start from the beginning instead
                    logger.Debug(ex, "Seek to resume position failed (player not ready), will start from beginning");
                }
                catch (Exception ex)
                {
                    // Catch any other exceptions during seek
                    logger.Warning(ex, "Unexpected error during seek to resume position, will start from beginning");
                }
            }
        }

        // Final check before starting playback - ensure stop wasn't called during seek/resume operations
        if (!IsPreparingOrPlayingInternal || playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            logger.Information("Playback was stopped before PlayAsync - aborting PlayCurrentTrackAsync");
            return;
        }

        logger.Debug("Calling PlayAsync for track at index {TrackIndex}", currentTrackIndex);
        // Note: isPreparingTrack is already false at this point, so IsPreparingOrPlayingInternal
        // will rely on actual MediaElement state, which should be ready by now
        await audioPlayer.PlayAsync();

        // On iOS, wait a bit longer for playback to actually start
        // MediaElement may need time to transition to Playing state
#if IOS
        await Task.Delay(300);
#endif

        logger.Debug("PlayAsync completed for track at index {TrackIndex}, Status: {Status}",
            currentTrackIndex,
            audioPlayer.Status);
        StartProgressTimerIfBibleTrack();

        // Don't clear auto-advancing flag here - let the reducer handle it when status stabilizes to Playing
        // This prevents rapid state changes from causing flicker
    }

    private void StartProgressTimerIfBibleTrack()
    {
        if (playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        var track = playlist[currentTrackIndex];
        if (track.PlayItem.Metadata.PlayType == PlayType.Bible)
        {
            progressSaveTimer?.Start();
        }
        else
        {
            progressSaveTimer?.Stop();
        }
    }

    private async void OnMediaEnded(object? sender, EventArgs e)
    {
        try
        {
            progressSaveTimer?.Stop();

            // Mark track as finished - this advances Bible chapter to next chapter with position 0.00
            await MarkCurrentTrackAsFinishedAsync();

            if (playlist is not null && currentTrackIndex < playlist.Count - 1)
            {
                // Set auto-advancing flag before transitioning to next track
                // This keeps the pause button visible during the transition
                logger.Information(
                    "[PlaybackService] OnMediaEnded: Dispatching SetAutoAdvancingAction(true) for automatic next track - ScheduleId={ScheduleId}, FromTrackIndex={FromTrackIndex}, ToTrackIndex={ToTrackIndex}",
                    currentScheduleId,
                    currentTrackIndex,
                    currentTrackIndex + 1);
                dispatcher.Dispatch(new SetAutoAdvancingAction(true));

                currentTrackIndex++;

                // Dispatch navigation state immediately after setting currentTrackIndex
                // This ensures CanPlayNext and CanPlayPrevious are correct before Android Auto processes status changes
                // This prevents button flicker during track transitions
                NotifyNavigationChanged();

                await PlayCurrentTrackAsync();
            }
            else
            {
                // Last track ended - skip MarkCurrentTrackAsPlayedAsync in StopAsync
                // because MarkTrackAsFinished already updated the database correctly
                await StopAsyncInternal(skipMarkAsPlayed: true);
            }
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
            var trackUri = playlist?[currentTrackIndex]?.Uri ?? "Unknown";
            var trackUrl = playlist?[currentTrackIndex]?.PlayItem?.Url ?? "Unknown";

            logger.Warning("Media failed for track at index {TrackIndex}. URI: {TrackUri}, URL: {TrackUrl}",
                currentTrackIndex,
                trackUri,
                trackUrl);

            if (playlist is not null && currentTrackIndex < playlist.Count - 1)
            {
                // Set auto-advancing flag before transitioning to next track
                logger.Information(
                    "[PlaybackService] OnMediaFailed: Dispatching SetAutoAdvancingAction(true) for automatic next track after failure - ScheduleId={ScheduleId}, FromTrackIndex={FromTrackIndex}, ToTrackIndex={ToTrackIndex}",
                    currentScheduleId,
                    currentTrackIndex,
                    currentTrackIndex + 1);
                dispatcher.Dispatch(new SetAutoAdvancingAction(true));

                currentTrackIndex++;

                // Dispatch navigation state immediately after setting currentTrackIndex
                // This ensures CanPlayNext and CanPlayPrevious are correct before Android Auto processes status changes
                // This prevents button flicker during track transitions
                NotifyNavigationChanged();

                logger.Information("Attempting to play next track at index {NextTrackIndex}", currentTrackIndex);
                await PlayCurrentTrackAsync();
            }
            else
            {
                logger.Warning("No more tracks available or all tracks failed. Handling playback failure.");
                await HandlePlaybackFailureAsync();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling media failed event");
        }
    }

    private async Task MarkCurrentTrackAsPlayedAsync()
    {
        if (playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        try
        {
            var track = playlist[currentTrackIndex];
            await playlistService.MarkTrackAsPlayed(track.PlayItem.Metadata);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error marking track as played");
        }
    }

    private void OnProgressSaveTimerElapsed(object? sender, ElapsedEventArgs e) => _ = SaveProgressAsync();

    private async Task SaveProgressAsync()
    {
        if (playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        var track = playlist[currentTrackIndex];

        // Only save progress for Bible tracks
        if (track.PlayItem.Metadata.PlayType != PlayType.Bible)
        {
            return;
        }

        // Only save if currently playing
        if (audioPlayer.Status != PlayStatus.Playing)
        {
            return;
        }

        try
        {
            var currentPosition = audioPlayer.CurrentPosition;
            if (currentPosition.HasValue)
            {
                // Update the track metadata with current position
                track.PlayItem.Metadata.FinishedDuration = currentPosition.Value;
                await playlistService.MarkTrackAsPlayed(track.PlayItem.Metadata);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error saving progress");
        }
    }

    private async Task<bool> ShouldResumeFromLastPositionAsync()
    {
        if (!currentScheduleId.HasValue)
        {
            return false;
        }

        try
        {
            return await playlistService.ShouldResumeFromLastPositionAsync(currentScheduleId.Value);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking if should resume from last position");
            return false;
        }
    }

    private async Task WaitForMediaReadyAsync()
    {
        // On iOS, MediaElement may need a moment after PrepareAsync before it can play
        // This is especially important when transitioning between tracks (Stop -> Prepare -> Play)
        // Wait for the MediaElement to transition from "Opening" to a ready state (Paused, Playing, or Buffering)
        // This ensures IsPreparingOrPlayingInternal will return true when we check it

        var maxWaitTime = TimeSpan.FromSeconds(2);
        var checkInterval = TimeSpan.FromMilliseconds(50);
        var elapsed = TimeSpan.Zero;

        while (elapsed < maxWaitTime)
        {
            // Check if MediaElement is in a ready state
            if (audioPlayer.IsActuallyPlayingOrPaused ||
                audioPlayer.Status == PlayStatus.Loading ||
                audioPlayer.Status == PlayStatus.Playing ||
                audioPlayer.Status == PlayStatus.Paused)
            {
                logger.Debug("Media ready check completed - MediaElement is in ready state, proceeding to play");
                return;
            }

            await Task.Delay(checkInterval);
            elapsed = elapsed.Add(checkInterval);
        }

        // If we've waited the max time, proceed anyway - the state might still transition
        logger.Debug("Media ready check completed after timeout - proceeding to play anyway");
    }

    private async Task MarkCurrentTrackAsFinishedAsync()
    {
        if (playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        try
        {
            var track = playlist[currentTrackIndex];
            await playlistService.MarkTrackAsFinished(track.PlayItem.Metadata);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error marking track as finished");
        }
    }

    private async Task HandlePlaybackFailureAsync()
    {
        // Reset player and playlist service
        await ResetAsync();

        if (isAlarm && currentScheduleId.HasValue)
        {
            // For alarms, dispatch error message so alarm modal shows the error
            dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = "Media playback failed. Playing fallback alarm sound."
            });

            // Show alarm notification and try to play fallback sound
            try
            {
                await notificationService.ShowNotificationAsync(currentScheduleId.Value);
                await PlayFallbackAlarmSoundAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error showing alarm notification or playing fallback alarm sound");
                // Update error message if fallback also fails
                dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = "Media playback failed. Check your internet connection."
                });
            }
        }
        else
        {
            // For non-alarms, show error message in UI
            dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = "Media download failed. Check your internet connection."
            });

            // Also show toast for immediate feedback
            WeakReferenceMessenger.Default.Send(new ShowToastMessage("Media download failed. Check your internet connection."));
        }
    }

    private async Task PlayFallbackAlarmSoundAsync()
    {
        try
        {
            var fallbackTrack = await fallbackAlarmSoundService.GetFallbackAlarmTrackAsync();
            if (fallbackTrack is null)
            {
                logger.Error("Failed to get fallback alarm track");
                // Even for alarms, if fallback fails, show error but keep modal open
                dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = "Media download failed. Check your internet connection."
                });
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Media download failed. Check your internet connection."));
                // Don't reset - keep modal open so user can see the error
                return;
            }

            // Clear any previous error when starting fallback playback
            dispatcher.Dispatch(new PlaybackErrorAction { ErrorMessage = null });

            playlist = [fallbackTrack];
            currentTrackIndex = 0;
            await PlayCurrentTrackAsync();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error playing fallback alarm sound");
            // Even for alarms, if fallback fails, show error but keep modal open
            dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = "Media download failed. Check your internet connection."
            });
            WeakReferenceMessenger.Default.Send(new ShowToastMessage("Media download failed. Check your internet connection."));
            // Don't reset - keep modal open so user can see the error
        }
    }

    public void Dispose()
    {
        if (progressSaveTimer is { } timer)
        {
            timer.Elapsed -= OnProgressSaveTimerElapsed;
            timer.Stop();
            timer.Dispose();
        }

        // Unsubscribe from AudioPlayer events
        audioPlayer.MediaEnded -= OnMediaEnded;
        audioPlayer.MediaFailed -= OnMediaFailed;

        // Unregister from messages
        WeakReferenceMessenger.Default.Unregister<NextButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PreviousButtonPressedMessage>(this);

        // All injected services (_audioPlayer, _preparePlaybackService, _playlistService, 
        // _fallbackAlarmSoundService, _dispatcher, _notificationService) are singletons, 
        // so don't dispose them
    }
}

