#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.CarAppServiceHelpers;

/// <summary>
/// Handles playback actions for car screen schedule items.
/// </summary>
public sealed class CarScreenPlaybackHandler(ILogger logger)
{
    public void HandleScheduleItemClicked(int scheduleId)
    {
        logger.Information("Schedule {ScheduleId} clicked - starting playback", scheduleId);

        try
        {
            StartPlaybackAsync(scheduleId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling schedule click for schedule {ScheduleId}", scheduleId);
        }
    }

    private void StartPlaybackAsync(int scheduleId)
    {
        var playbackService = ServiceProviderManager.GetService<ISchedulePlaybackService>();
        if (playbackService == null)
        {
            logger.Error("ISchedulePlaybackService is null - cannot play schedule {ScheduleId}", scheduleId);
            return;
        }

        // Play asynchronously - don't block the UI thread
        _ = Task.Run(async () =>
        {
            try
            {
                await playbackService.PlayScheduleAsync(scheduleId);
                logger.Information("✅ Started playback for schedule {ScheduleId}", scheduleId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error playing schedule {ScheduleId}", scheduleId);
            }
        });
    }
}

