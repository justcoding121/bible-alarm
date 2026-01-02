#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
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
        HashSet<int> manuallyVisitedTrackIndices,
        Func<int, Task> markCurrentTrackAsPlayedAsync,
        Func<bool, Task> playCurrentTrackAsync)
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

            // If we've already manually visited this track, start from beginning
            // Otherwise, allow resume from saved position (for Bible tracks)
            var startFromBeginning = manuallyVisitedTrackIndices.Contains(nextTrackIndex);
            manuallyVisitedTrackIndices.Add(nextTrackIndex);

            await playCurrentTrackAsync(startFromBeginning);
        }
    }

    public async Task PlayPreviousAsync(
        List<AudioPlayerTrack>? playlist,
        Func<int> getCurrentTrackIndex,
        Action<int> setCurrentTrackIndex,
        int? currentScheduleId,
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
            // On first track - restart current track from beginning
            progressTracker.Stop();
            await audioPlayer.StopAsync();
            manuallyVisitedTrackIndices.Add(currentTrackIndex);
            await playCurrentTrackAsync(true);
            // Don't call NotifyNavigationChanged() - we're still on the same track
        }
    }
}

