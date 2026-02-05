#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
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

    public async Task PlayNextAsync(
        List<AudioPlayerTrack>? playlist,
        Func<int> getCurrentTrackIndex,
        Action<int> setCurrentTrackIndex,
        int? currentScheduleId,
        bool isIndefinitePlayback,
        Func<Task<bool>> tryAppendNextTrackAsync,
        HashSet<int> manuallyVisitedTrackIndices,
        Func<int, Task> markCurrentTrackAsPlayedAsync,
        Func<bool, Task> playCurrentTrackAsync,
        Func<Task> stopPlaybackAsync)
    {
        if (playlist is null || playlist.Count == 0)
        {
            return;
        }

        var currentTrackIndex = getCurrentTrackIndex();
        if (currentTrackIndex < playlist.Count - 1)
        {
            progressTracker.Stop();
            await audioPlayer.StopAsync();
            await markCurrentTrackAsPlayedAsync(currentTrackIndex);

            // Set auto-advancing flag for smooth transition during manual navigation
            logger.Information(
                "[PlaybackService] PlayNextAsync: Dispatching SetAutoAdvancingAction(true) for manual next - ScheduleId={ScheduleId}, FromTrackIndex={FromTrackIndex}, ToTrackIndex={ToTrackIndex}",
                currentScheduleId,
                currentTrackIndex,
                currentTrackIndex + 1);
            dispatcher.Dispatch(new SetAutoAdvancingAction(true));

            var nextTrackIndex = currentTrackIndex + 1;
            setCurrentTrackIndex(nextTrackIndex);

            // Dispatch navigation state immediately after setting currentTrackIndex
            // This ensures CanPlayNext and CanPlayPrevious are correct before Android Auto processes status changes
            // This prevents button flicker during track transitions
            navigationManager.NotifyNavigationChanged(playlist, nextTrackIndex);

            // Seek-to-saved-position is only for initial play of a track. Any transition via next/prev starts from beginning.
            manuallyVisitedTrackIndices.Add(nextTrackIndex);
            await playCurrentTrackAsync(true);
            return;
        }

        // At the end of the current in-memory playlist.
        progressTracker.Stop();
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

                dispatcher.Dispatch(new SetAutoAdvancingAction(true));

                var nextTrackIndex = currentTrackIndex + 1;
                setCurrentTrackIndex(nextTrackIndex);
                navigationManager.NotifyNavigationChanged(playlist, nextTrackIndex);

                // Manual next always starts from beginning once we extend dynamically.
                manuallyVisitedTrackIndices.Add(nextTrackIndex);
                await playCurrentTrackAsync(true);
                return;
            }
        }

        // Finite playback: stop and dismiss when user tries to go past the end.
        await stopPlaybackAsync();
    }

    public async Task PlayPreviousAsync(
        List<AudioPlayerTrack>? playlist,
        Func<int> getCurrentTrackIndex,
        Action<int> setCurrentTrackIndex,
        int? currentScheduleId,
        bool isIndefinitePlayback,
        Func<Task<bool>> tryPrependPreviousTrackAsync,
        HashSet<int> manuallyVisitedTrackIndices,
        Func<int, Task> markCurrentTrackAsPlayedAsync,
        Func<bool, Task> playCurrentTrackAsync)
    {
        if (playlist is null || playlist.Count == 0)
        {
            return;
        }

        var currentTrackIndex = getCurrentTrackIndex();
        if (currentTrackIndex > 0)
        {
            // Go to previous track
            progressTracker.Stop();
            await audioPlayer.StopAsync();
            await markCurrentTrackAsPlayedAsync(currentTrackIndex);

            // Set auto-advancing flag for smooth transition during manual navigation
            logger.Information(
                "[PlaybackService] PlayPreviousAsync: Dispatching SetAutoAdvancingAction(true) for manual previous - ScheduleId={ScheduleId}, FromTrackIndex={FromTrackIndex}, ToTrackIndex={ToTrackIndex}",
                currentScheduleId,
                currentTrackIndex,
                currentTrackIndex - 1);
            dispatcher.Dispatch(new SetAutoAdvancingAction(true));

            var previousTrackIndex = currentTrackIndex - 1;
            setCurrentTrackIndex(previousTrackIndex);

            // Dispatch navigation state immediately after setting currentTrackIndex
            // This ensures CanPlayNext and CanPlayPrevious are correct before Android Auto processes status changes
            // This prevents button flicker during track transitions
            navigationManager.NotifyNavigationChanged(playlist, previousTrackIndex);

            // Previous button always starts from beginning
            manuallyVisitedTrackIndices.Add(previousTrackIndex);
            await playCurrentTrackAsync(true);
        }
        else if (currentTrackIndex == 0)
        {
            progressTracker.Stop();
            await audioPlayer.StopAsync();
            await markCurrentTrackAsPlayedAsync(currentTrackIndex);

            // Attempt to extend backward (circular previous) when possible.
            var prepended = await tryPrependPreviousTrackAsync();
            if (prepended)
            {
                dispatcher.Dispatch(new SetAutoAdvancingAction(true));
                setCurrentTrackIndex(0);
                navigationManager.NotifyNavigationChanged(playlist, 0);
                manuallyVisitedTrackIndices.Add(0);
                await playCurrentTrackAsync(true);
                return;
            }

            // Fallback: restart current track from beginning.
            manuallyVisitedTrackIndices.Add(currentTrackIndex);
            await playCurrentTrackAsync(true);
            // Don't call NotifyNavigationChanged() - we're still on the same track
        }
    }
}

