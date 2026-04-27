#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles stopping playback and cleanup operations.
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class PlaybackStopHandler
{
    private readonly IAudioPlayer audioPlayer;
    private readonly IPlaylistService playlistService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger logger;
    private readonly ProgressTracker progressTracker;

    public PlaybackStopHandler(
        IAudioPlayer audioPlayer,
        IPlaylistService playlistService,
        IDispatcher dispatcher,
        ILogger logger,
        ProgressTracker progressTracker)
    {
        this.audioPlayer = audioPlayer;
        this.playlistService = playlistService;
        this.dispatcher = dispatcher;
        this.logger = logger;
        this.progressTracker = progressTracker;
    }

    public async Task StopAsync(PlaybackStopRequest request)
    {
        var scheduleIdToSave = request.ScheduleIdToSave;
        var trackMetadataToMark = request.TrackMetadataToMark;
        var skipMarkAsPlayed = request.SkipMarkAsPlayed;
        var skipSaveLastPlayed = request.SkipSaveLastPlayed;
        var preparationCancellationTokenSource = request.PreparationCancellationTokenSource;
        var resetState = request.ResetState;
        var stopProgressTimer = request.StopProgressTimer;
        var skipDispatchStopped = request.SkipDispatchStopped;

        logger.Information("StopAsync called - stopping alarm completely");

        var dispatched = false;

        try
        {
            // Cancel any ongoing preparation/downloads
            try
            {
                preparationCancellationTokenSource?.CancelAsync();
                logger.Debug("Cancelled preparation cancellation token");
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error cancelling preparation token");
            }

            // Stop progress timer FIRST to prevent in-flight timer callbacks from racing with state reset.
            // System.Timers.Timer.Stop() doesn't cancel in-flight callbacks, but it prevents new ones.
            try
            {
                stopProgressTimer();
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error stopping progress timer");
            }

            // Reset state to ensure PlayCurrentTrackAsync checks detect stop immediately.
            // This is especially important for the gap between downloads completing and playback starting.
            try
            {
                resetState();
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error resetting playback state");
            }

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
                    // Music tracks are handled by ProgressTracker on first progress update
                    // Only mark Bible tracks as played here (saves current position)
                    if (trackMetadataToMark.PlayType != PlayType.Music)
                    {
                        // For Bible tracks, mark as played (which saves current position)
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
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in audioPlayer.ResetAsync, attempting minimal cleanup");
                try
                {
                    await audioPlayer.ResetAsync();
                }
                catch (Exception resetEx)
                {
                    logger.Warning(resetEx, "Error resetting player in fallback");
                }
            }

            if (!skipDispatchStopped)
            {
                dispatched = true;
                dispatcher.Dispatch(new PlaybackStoppedAction());
#if ANDROID || IOS
                dispatcher.Dispatch(new SetCarPlayScreenAction());
                logger.Debug("SetCarPlayScreenAction dispatched after playback reset");
#endif
            }
        }
        finally
        {
            if (!skipDispatchStopped && !dispatched)
            {
                logger.Warning("PlaybackStoppedAction was not dispatched during normal flow — dispatching in finally");
                try
                {
                    dispatcher.Dispatch(new PlaybackStoppedAction());
#if ANDROID || IOS
                    dispatcher.Dispatch(new SetCarPlayScreenAction());
#endif
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to dispatch PlaybackStoppedAction in finally block");
                }
            }
        }
    }
}

