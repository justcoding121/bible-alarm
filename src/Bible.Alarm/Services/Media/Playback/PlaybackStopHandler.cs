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

    public async Task StopAsync(
        int? scheduleIdToSave,
        TrackMetadata? trackMetadataToMark,
        bool skipMarkAsPlayed,
        bool skipSaveLastPlayed,
        CancellationTokenSource? preparationCancellationTokenSource,
        Action resetState,
        Action stopProgressTimer)
    {
        logger.Information("StopAsync called - stopping alarm completely");

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

        // Reset state early to ensure PlayCurrentTrackAsync checks detect stop immediately
        // This is especially important for the gap between downloads completing and playback starting
        resetState();

        // Stop progress timer first to prevent it from trying to save progress after ServiceProvider is disposed
        stopProgressTimer();

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
            // Dispatch playback stopped action to close modal (state was already reset above)
            dispatcher.Dispatch(new PlaybackStoppedAction());
#if ANDROID || IOS
            // Dispatch SetCarPlayScreenAction to refresh car display with default schedule metadata
            // Android: Updates MediaSession for Android Auto
            // iOS: Updates MPNowPlayingInfoCenter for CarPlay and Lock Screen
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
    }
}

