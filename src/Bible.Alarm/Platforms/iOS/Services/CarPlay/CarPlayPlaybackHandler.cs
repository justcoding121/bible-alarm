/* CARPLAY MEDIA LISTING DISABLED - Requires Apple MFi approval
 * Commented out to avoid App Store rejection for CarPlay Audio entitlement.
 * This handler processes playback actions when schedule items are clicked from the CarPlay list.
 * 
 * To re-enable after Apple approval, uncomment this file.
 */

/*
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
*/

// Placeholder class to prevent compilation errors
namespace Bible.Alarm.Platforms.iOS.Services.CarPlay;

public sealed class CarPlayPlaybackHandler
{
    public static void HandleScheduleItemClicked(int scheduleId)
    {
        // CarPlay listing disabled - no action
    }
}