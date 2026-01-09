#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Services.CarPlay;

/// <summary>
/// Handles playback actions for CarPlay schedule items.
/// </summary>
public sealed class CarPlayPlaybackHandler
{
    private static readonly ILogger logger = Log.ForContext<CarPlayPlaybackHandler>();

    /// <summary>
    /// Handles schedule item clicked from CarPlay list.
    /// </summary>
    public static void HandleScheduleItemClicked(int scheduleId)
    {
        logger.Information("[CarPlay] Schedule {ScheduleId} clicked - starting playback", scheduleId);

        try
        {
            StartPlaybackAsync(scheduleId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[CarPlay] Error handling schedule click for schedule {ScheduleId}", scheduleId);
        }
    }

    private static void StartPlaybackAsync(int scheduleId)
    {
        var playbackService = ServiceProviderManager.GetService<ISchedulePlaybackService>();
        if (playbackService == null)
        {
            logger.Error("[CarPlay] ISchedulePlaybackService is null - cannot play schedule {ScheduleId}", scheduleId);
            return;
        }

        // Play asynchronously - don't block the UI thread
        _ = Task.Run(async () =>
        {
            try
            {
                await playbackService.PlayScheduleAsync(scheduleId);
                logger.Information("[CarPlay] ✅ Started playback for schedule {ScheduleId}", scheduleId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "[CarPlay] Error playing schedule {ScheduleId}", scheduleId);
            }
        });
    }
}
