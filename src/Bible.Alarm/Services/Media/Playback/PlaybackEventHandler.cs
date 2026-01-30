#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles playback events (media ended, media failed).
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class PlaybackEventHandler
{
    private readonly IPlaylistService playlistService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger logger;
    private readonly PlaybackNavigationManager navigationManager;

    public PlaybackEventHandler(
        IPlaylistService playlistService,
        IDispatcher dispatcher,
        ILogger logger,
        PlaybackNavigationManager navigationManager)
    {
        this.playlistService = playlistService;
        this.dispatcher = dispatcher;
        this.logger = logger;
        this.navigationManager = navigationManager;
    }

    public async Task HandleMediaEndedAsync(
        List<AudioPlayerTrack>? playlist,
        Func<int> getCurrentTrackIndex,
        Action<int> setCurrentTrackIndex,
        int? currentScheduleId,
        bool isIndefinitePlayback,
        Func<Task<bool>> tryAppendNextTrackAsync,
        Func<bool, Task> playCurrentTrackAsync,
        Func<bool, Task> stopAsyncInternal)
    {
        var currentTrackIndex = getCurrentTrackIndex();

        // Mark track as finished - this advances Bible track to next track with position 0.00
        await MarkCurrentTrackAsFinishedAsync(playlist, currentTrackIndex);

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

            var nextTrackIndex = currentTrackIndex + 1;
            setCurrentTrackIndex(nextTrackIndex);

            // Dispatch navigation state immediately after setting currentTrackIndex
            // This ensures CanPlayNext and CanPlayPrevious are correct before Android Auto processes status changes
            // This prevents button flicker during track transitions
            navigationManager.NotifyNavigationChanged(playlist, nextTrackIndex);

            await playCurrentTrackAsync(false);
        }
        else
        {
            if (isIndefinitePlayback)
            {
                // Indefinite playback: extend playlist and continue.
                var appended = await tryAppendNextTrackAsync();
                if (appended && playlist is not null && currentTrackIndex < playlist.Count - 1)
                {
                    dispatcher.Dispatch(new SetAutoAdvancingAction(true));

                    var nextTrackIndex = currentTrackIndex + 1;
                    setCurrentTrackIndex(nextTrackIndex);
                    navigationManager.NotifyNavigationChanged(playlist, nextTrackIndex);
                    await playCurrentTrackAsync(false);
                    return;
                }
            }

            // Finite playback (or failed to extend): stop/dismiss.
            // Skip MarkCurrentTrackAsPlayedAsync in StopAsync because MarkTrackAsFinished already updated the database correctly.
            await stopAsyncInternal(true);
        }
    }

    public async Task HandleMediaFailedAsync(
        List<AudioPlayerTrack>? playlist,
        Func<int> getCurrentTrackIndex,
        Action<int> setCurrentTrackIndex,
        int? currentScheduleId,
        bool isIndefinitePlayback,
        Func<Task<bool>> tryAppendNextTrackAsync,
        string trackUri,
        string trackUrl,
        Func<bool, Task> playCurrentTrackAsync,
        Func<Task> handlePlaybackFailureAsync)
    {
        var currentTrackIndex = getCurrentTrackIndex();
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

            var nextTrackIndex = currentTrackIndex + 1;
            setCurrentTrackIndex(nextTrackIndex);

            // Dispatch navigation state immediately after setting currentTrackIndex
            // This ensures CanPlayNext and CanPlayPrevious are correct before Android Auto processes status changes
            // This prevents button flicker during track transitions
            navigationManager.NotifyNavigationChanged(playlist, nextTrackIndex);

            logger.Information("Attempting to play next track at index {NextTrackIndex}", nextTrackIndex);
            await playCurrentTrackAsync(false);
        }
        else
        {
            if (isIndefinitePlayback)
            {
                var appended = await tryAppendNextTrackAsync();
                if (appended && playlist is not null && currentTrackIndex < playlist.Count - 1)
                {
                    dispatcher.Dispatch(new SetAutoAdvancingAction(true));
                    var nextTrackIndex = currentTrackIndex + 1;
                    setCurrentTrackIndex(nextTrackIndex);
                    navigationManager.NotifyNavigationChanged(playlist, nextTrackIndex);
                    await playCurrentTrackAsync(false);
                    return;
                }
            }

            logger.Warning("No more tracks available or all tracks failed. Handling playback failure.");
            await handlePlaybackFailureAsync();
        }
    }

    private async Task MarkCurrentTrackAsFinishedAsync(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        if (playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        try
        {
            var track = playlist[currentTrackIndex];

            // Only mark Bible tracks as finished here (advances track)
            // Music tracks are handled by ProgressTracker on first progress update
            if (track.PlayItem.Metadata.PlayType == PlayType.Music)
            {
                return;
            }

            await playlistService.MarkTrackAsFinished(track.PlayItem.Metadata);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error marking track as finished");
        }
    }
}

