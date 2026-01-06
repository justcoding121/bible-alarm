using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Platforms.Android.Services.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Handlers;

public sealed class AndroidAlarmHandler(
    ILogger logger,
    IPlaybackService playbackService,
    IAlarmScheduleService alarmScheduleService)
    : IAndroidAlarmHandler, IDisposable
{
    public event EventHandler<bool> Disposed;

    public async Task HandleAsync(int scheduleId, bool isAlarm)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, false);

        if (schedule == null)
        {
            Dispose();
            return;
        }

        // If "play only when I tap on notification" is enabled for alarm,
        // stop the foreground service and its sticky notification, then show regular notification
        if (isAlarm && schedule.NotificationEnabled)
        {
            // Stop foreground service and its sticky notification (we don't need it if user must tap)
            Platforms.Android.Services.Media.ForegroundServiceCoordinator.StopAlarmForegroundServiceIfActive();
            
            AndroidNotificationService.RemoveLocalNotification(schedule.Id);
            AndroidNotificationService.ShowLocalNotification(schedule.Id,
                string.IsNullOrEmpty(schedule.Name) ? "Bible Alarm" : schedule.Name,
                "Press to start listening now.");
            Dispose();
            return;
        }

        // When user taps notification (isAlarm=false) or manual playback request:
        // - Always start playback immediately (user-initiated playback)
        // - NotificationEnabled flag only applies to alarm triggers, not user-initiated playback
        // - Remove notification if it exists (user tapped it)
        if (!isAlarm && schedule.NotificationEnabled)
        {
            // User-initiated playback (notification tap or manual request) - start playback
            // Remove notification since user is starting playback
            AndroidNotificationService.RemoveLocalNotification(schedule.Id);
            logger.Information("User-initiated playback for schedule {ScheduleId} (NotificationEnabled=true but user requested playback)", scheduleId);
            // Continue to playback (don't return here)
        }
        else if (schedule.NotificationEnabled)
        {
            // Remove notification if it exists (for non-user-initiated cases)
            AndroidNotificationService.RemoveLocalNotification(schedule.Id);
        }

        // MediaManager removed - using MediaElement instead

        await Task.Run(async () =>
        {
            try
            {
                await playbackService.PrepareAndPlayAsync(scheduleId, isAlarm);

                // Notification manager removed - using MediaElement instead
            }
            catch (Exception e)
            {
                logger.Error(e, "An error happened when ringing the alarm.");
                Dispose();
            }
        });
    }

    // PlayerNotificationManager removed - using MediaElement instead
    // Notification handling is now managed by the MediaElement service

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // playbackService (IPlaybackService) and IServiceScopeFactory are singletons
        // and should not be disposed here as they are managed by the DI container

        Disposed?.Invoke(this, true);
    }
}
