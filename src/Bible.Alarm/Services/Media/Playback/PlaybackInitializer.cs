#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles initialization and preparation of playback for a schedule.
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class PlaybackInitializer
{
    private readonly IPreparePlaybackService preparePlaybackService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger logger;

    public PlaybackInitializer(
        IPreparePlaybackService preparePlaybackService,
        IDispatcher dispatcher,
        ILogger logger)
    {
        this.preparePlaybackService = preparePlaybackService;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    public async Task<List<AudioPlayerTrack>?> PrepareTracksAsync(
        int scheduleId,
        CancellationToken cancellationToken)
    {
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

        // Run track preparation (including downloads) on background thread to avoid blocking main thread
        // This ensures UI remains responsive during download/preparation phase
        logger.Debug("Starting track preparation on background thread for schedule {ScheduleId}", scheduleId);

        try
        {
            return await Task.Run(async () => await preparePlaybackService.PrepareTracksAsync(scheduleId, cancellationToken));
        }
        catch (OperationCanceledException ex)
        {
            logger.Information(ex, "Track preparation cancelled for schedule {ScheduleId}", scheduleId);
            return null;
        }
    }
}

