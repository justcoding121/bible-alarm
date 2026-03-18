using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Platforms.Android.Services.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Handlers;

public sealed class AndroidAlarmHandler(
    ILogger logger,
    IPlaybackService playbackService,
    IAlarmScheduleService alarmScheduleService,
    IState<PlaybackState> playbackState)
    : IAndroidAlarmHandler, IDisposable
{
    public event EventHandler<bool> Disposed;

    public async Task HandleAsync(int scheduleId, bool isAlarm)
    {
        if (isAlarm && playbackState.Value.IsPreparingOrPlaying)
        {
            logger.Information("Alarm triggered for schedule {ScheduleId} while playback is already active - skipping to avoid interrupting current playback", scheduleId);
            Dispose();
            return;
        }

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
            logger.Information("Alarm triggered with NotificationEnabled=true for schedule {ScheduleId} - stopping foreground service and showing tap notification", scheduleId);

            // Stop foreground service and its sticky notification (we don't need it if user must tap)
            Platforms.Android.Services.Media.ForegroundServiceCoordinator.StopAlarmForegroundServiceIfActive();

            logger.Debug("Removing any existing local notification for schedule {ScheduleId}", schedule.Id);
            AndroidNotificationService.RemoveLocalNotification(schedule.Id);

            var notificationTitle = string.IsNullOrEmpty(schedule.Name) ? "Bible Alarm" : schedule.Name;
            logger.Information("Showing local notification for schedule {ScheduleId} - Title={Title}", schedule.Id, notificationTitle);
            AndroidNotificationService.ShowLocalNotification(schedule.Id,
                notificationTitle,
                "Press to start listening now.");

            logger.Information("Local notification shown for schedule {ScheduleId} - waiting for user tap", schedule.Id);
            Dispose();
            return;
        }

        // When tap is disabled (NotificationEnabled=false) and alarm triggers:
        // - Do NOT show regular notification
        // - Keep the foreground service notification (already shown by OnAlarmTriggered, without sound)
        // - Continue to playback
        if (isAlarm && !schedule.NotificationEnabled)
        {
            // Tap disabled: foreground service notification is already shown (without sound)
            // No need to show regular notification - just continue to playback
            logger.Information("Alarm triggered for schedule {ScheduleId} with tap disabled - using foreground service notification (no sound)", scheduleId);
        }

        // When user taps notification (isAlarm=false) or manual playback request:
        // - Always start playback immediately (user-initiated playback)
        // - NotificationEnabled flag only applies to alarm triggers, not user-initiated playback
        // - Remove notification if it exists (user tapped it)
        // - Do NOT show any notifications or sounds for user-initiated playback
        if (!isAlarm)
        {
            // User-initiated playback (notification tap or manual request) - start playback silently
            // Remove any existing notifications since user is starting playback
            AndroidNotificationService.RemoveLocalNotification(schedule.Id);
            logger.Information("User-initiated playback for schedule {ScheduleId} - starting playback directly without notifications", scheduleId);

            // For user-initiated playback, directly call PrepareAndPlayAsync without going through alarm handler logic
            // This ensures no foreground service calls or notification sounds
            await Task.Run(async () =>
            {
                try
                {
                    await playbackService.PrepareAndPlayAsync(scheduleId, isAlarm: false);
                }
                catch (Exception e)
                {
                    logger.Error(e, "An error happened when starting user-initiated playback.");
                    Dispose();
                }
            });

            return;
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
