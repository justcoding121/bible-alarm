#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles track navigation (next/previous) operations.
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class PlaybackNavigationHandler
{
    private readonly IAudioPlayer audioPlayer;
    private readonly IDispatcher dispatcher;
    private readonly ILogger logger;
    private readonly ProgressTracker progressTracker;
    private readonly PlaybackNavigationManager navigationManager;

    public PlaybackNavigationHandler(
        IAudioPlayer audioPlayer,
        IDispatcher dispatcher,
        ILogger logger,
        ProgressTracker progressTracker,
        PlaybackNavigationManager navigationManager)
    {
        this.audioPlayer = audioPlayer;
        this.dispatcher = dispatcher;
        this.logger = logger;
        this.progressTracker = progressTracker;
        this.navigationManager = navigationManager;
    }

    public async Task PlayNextAsync(PlaybackNavigationNextRequest request)
    {
        var playlist = request.Playlist;
        Func<int> getCurrentTrackIndex = request.GetCurrentTrackIndex;
        Action<int> setCurrentTrackIndex = request.SetCurrentTrackIndex;
        var currentScheduleId = request.CurrentScheduleId;
        var isIndefinitePlayback = request.IsIndefinitePlayback;
        Func<Task<bool>> tryAppendNextTrackAsync = request.TryAppendNextTrackAsync;
        var manuallyVisitedTrackIndices = request.ManuallyVisitedTrackIndices;
        Func<int, Task> markCurrentTrackAsPlayedAsync = request.MarkCurrentTrackAsPlayedAsync;
        Func<bool, Task> playCurrentTrackAsync = request.PlayCurrentTrackAsync;
        Func<Task> stopPlaybackAsync = request.StopPlaybackAsync;
        Func<Task> handlePlaybackFailureAsync = request.HandlePlaybackFailureAsync;

        if (playlist is null || playlist.Count == 0)
        {
            return;
        }

        dispatcher.Dispatch(new PlaybackTrackTransitionStartedAction());
        dispatcher.Dispatch(new PlaybackStatusChangedAction(PlayStatus.Loading));
        await Task.Delay(150);

        var currentTrackIndex = getCurrentTrackIndex();
        if (currentTrackIndex < playlist.Count - 1)
        {
            // Set auto-advancing BEFORE StopAsync so isAutoAdvancing is true when the
            // Stopped state from StopAsync is processed by MediaSessionEffect, preventing
            // a brief Play-button flash.
            logger.Information(
                "[PlaybackService] PlayNextAsync: Dispatching SetAutoAdvancingAction(true) for manual next - ScheduleId={ScheduleId}, FromTrackIndex={FromTrackIndex}, ToTrackIndex={ToTrackIndex}",
                currentScheduleId,
                currentTrackIndex,
                currentTrackIndex + 1);
            dispatcher.Dispatch(new SetAutoAdvancingAction(true));

            progressTracker.Stop();
            audioPlayer.NotifyTrackTransitionStarting();
            await audioPlayer.StopAsync();
            await markCurrentTrackAsPlayedAsync(currentTrackIndex);

            var nextTrackIndex = currentTrackIndex + 1;
            setCurrentTrackIndex(nextTrackIndex);

            navigationManager.NotifyNavigationChanged(playlist, nextTrackIndex);

            // Resume only on first visit; start from beginning when returning to an already-visited track
            var alreadyVisited = manuallyVisitedTrackIndices.Contains(nextTrackIndex);
            manuallyVisitedTrackIndices.Add(nextTrackIndex);
            await playCurrentTrackAsync(alreadyVisited);
            return;
        }

        // At the end of the current in-memory playlist.
        dispatcher.Dispatch(new SetAutoAdvancingAction(true));
        progressTracker.Stop();
        audioPlayer.NotifyTrackTransitionStarting();
        await audioPlayer.StopAsync();
        await markCurrentTrackAsPlayedAsync(currentTrackIndex);

        if (isIndefinitePlayback)
        {
            var appended = await tryAppendNextTrackAsync();
            if (appended && currentTrackIndex < playlist.Count - 1)
            {
                logger.Information(
                    "[PlaybackService] PlayNextAsync: Extended playlist for indefinite playback - ScheduleId={ScheduleId}, FromTrackIndex={FromTrackIndex}, ToTrackIndex={ToTrackIndex}",
                    currentScheduleId,
                    currentTrackIndex,
                    currentTrackIndex + 1);

                var nextTrackIndex = currentTrackIndex + 1;
                setCurrentTrackIndex(nextTrackIndex);
                navigationManager.NotifyNavigationChanged(playlist, nextTrackIndex);

                // Manual next always starts from beginning once we extend dynamically.
                manuallyVisitedTrackIndices.Add(nextTrackIndex);
                await playCurrentTrackAsync(true);
                return;
            }

            // Append failed (catalog/download error) - show error in modal with retry
            logger.Warning("PlayNextAsync: Failed to extend playlist for indefinite playback. Showing error in modal.");
            dispatcher.Dispatch(new PlaybackTrackTransitionEndedAction());
            await handlePlaybackFailureAsync();
            return;
        }

        // Finite playback: stop and dismiss when user tries to go past the end.
        dispatcher.Dispatch(new PlaybackTrackTransitionEndedAction());
        await stopPlaybackAsync();
    }

    public async Task PlayPreviousAsync(PlaybackNavigationPreviousRequest request)
    {
        var playlist = request.Playlist;
        Func<int> getCurrentTrackIndex = request.GetCurrentTrackIndex;
        Action<int> setCurrentTrackIndex = request.SetCurrentTrackIndex;
        var currentScheduleId = request.CurrentScheduleId;
        Func<Task<bool>> tryPrependPreviousTrackAsync = request.TryPrependPreviousTrackAsync;
        var manuallyVisitedTrackIndices = request.ManuallyVisitedTrackIndices;
        Func<int, Task> markCurrentTrackAsPlayedAsync = request.MarkCurrentTrackAsPlayedAsync;
        Func<bool, Task> playCurrentTrackAsync = request.PlayCurrentTrackAsync;
        Func<Task> handlePlaybackFailureAsync = request.HandlePlaybackFailureAsync;

        if (playlist is null || playlist.Count == 0)
        {
            return;
        }

        dispatcher.Dispatch(new PlaybackTrackTransitionStartedAction());
        dispatcher.Dispatch(new PlaybackStatusChangedAction(PlayStatus.Loading));
        await Task.Delay(150);

        var currentTrackIndex = getCurrentTrackIndex();
        if (currentTrackIndex > 0)
        {
            // Set auto-advancing BEFORE StopAsync so isAutoAdvancing is true when the
            // Stopped state from StopAsync is processed by MediaSessionEffect.
            logger.Information(
                "[PlaybackService] PlayPreviousAsync: Dispatching SetAutoAdvancingAction(true) for manual previous - ScheduleId={ScheduleId}, FromTrackIndex={FromTrackIndex}, ToTrackIndex={ToTrackIndex}",
                currentScheduleId,
                currentTrackIndex,
                currentTrackIndex - 1);
            dispatcher.Dispatch(new SetAutoAdvancingAction(true));

            progressTracker.Stop();
            audioPlayer.NotifyTrackTransitionStarting();
            await audioPlayer.StopAsync();
            await markCurrentTrackAsPlayedAsync(currentTrackIndex);

            var previousTrackIndex = currentTrackIndex - 1;
            setCurrentTrackIndex(previousTrackIndex);

            navigationManager.NotifyNavigationChanged(playlist, previousTrackIndex);

            // Previous button always starts from beginning
            manuallyVisitedTrackIndices.Add(previousTrackIndex);
            await playCurrentTrackAsync(true);
        }
        else if (currentTrackIndex == 0)
        {
            dispatcher.Dispatch(new SetAutoAdvancingAction(true));

            progressTracker.Stop();
            audioPlayer.NotifyTrackTransitionStarting();
            await audioPlayer.StopAsync();
            await markCurrentTrackAsPlayedAsync(currentTrackIndex);

            // Attempt to extend backward (circular previous) when possible.
            var prepended = await tryPrependPreviousTrackAsync();
            if (prepended)
            {
                // SetAutoAdvancingAction was already dispatched above before StopAsync.
                setCurrentTrackIndex(0);
                navigationManager.NotifyNavigationChanged(playlist, 0);
                manuallyVisitedTrackIndices.Add(0);
                await playCurrentTrackAsync(true);
                return;
            }

            // Prepend failed (catalog/download error) - show error in modal with retry
            logger.Warning("PlayPreviousAsync: Failed to extend playlist backward. Showing error in modal.");
            dispatcher.Dispatch(new PlaybackTrackTransitionEndedAction());
            await handlePlaybackFailureAsync();
        }
    }
}

