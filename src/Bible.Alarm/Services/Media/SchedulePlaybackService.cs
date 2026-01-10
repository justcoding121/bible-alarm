using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class SchedulePlaybackService(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    IState<PlaybackState> playbackState)
    : ISchedulePlaybackService
{

    public async Task PlayScheduleAsync(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();
        var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        try
        {
            // Remove any existing alarm notifications before starting user-initiated playback
            // This ensures no notification sounds when user clicks play from home page
#if ANDROID
            Platforms.Android.Services.UI.AndroidNotificationService.RemoveLocalNotification(scheduleId);
            // Also stop any alarm foreground service that might be active
            Platforms.Android.Services.Media.ForegroundServiceCoordinator.StopAlarmForegroundServiceIfActive();
#endif
            
            await playbackService.PrepareAndPlayAsync(scheduleId, false);
            // Note: ShowNotificationAsync is not called here because it triggers AlarmRingerReceiver
            // which would cause duplicate playback. Notifications are only shown when alarms actually fire.
        }
        catch (Exception e)
        {
            logger.Information(e, "An error happened when playing alarm.");
            await toastService.ShowMessage("Error. Network may not be available. Please try again.", 5);
        }
    }

    public async Task<bool> CanMoveTrackAsync(int scheduleId)
    {
        using var scope = scopeFactory.CreateScope();

        if (!playbackState.Value.IsPreparingOrPlaying || playbackState.Value.CurrentScheduleId != scheduleId)
        {
            return true;
        }

        var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();
        await toastService.ShowMessage("Cannot update the track when schedule is in progress.");

        return false;
    }

}

