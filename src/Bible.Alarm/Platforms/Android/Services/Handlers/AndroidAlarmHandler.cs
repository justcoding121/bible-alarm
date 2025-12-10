using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Interfaces;
using Bible.Alarm.Platforms.Android.Services.UI;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Handlers;

public class AndroidAlarmHandler(
    ILogger logger,
    IPlaybackService playbackService,
    IAlarmScheduleService alarmScheduleService)
    : IAndroidAlarmHandler, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IAlarmScheduleService _alarmScheduleService = alarmScheduleService;


    private bool _playbackServiceInitialized;

    public event EventHandler<bool> Disposed;

    public async Task HandleAsync(int scheduleId, bool isAlarm)
    {
        var schedule = await _alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, false);

        if (schedule == null)
        {
            Dispose();
            return;
        }

        //local notification for android
        if (!isAlarm)
            if (schedule.NotificationEnabled)
            {
                AndroidNotificationService.RemoveLocalNotification(schedule.Id);
                AndroidNotificationService.ShowLocalNotification(schedule.Id,
                    string.IsNullOrEmpty(schedule.Name) ? "Bible Alarm" : schedule.Name,
                    "Press to start listening now.");
                Dispose();
                return;
            }

        if (schedule.NotificationEnabled) AndroidNotificationService.RemoveLocalNotification(schedule.Id);

        // MediaManager removed - using MediaElement instead

        await Task.Run(async () =>
        {
            try
            {
                await playbackService.PrepareAndPlayAsync(scheduleId, isAlarm);

                _playbackServiceInitialized = true;

                // Notification manager removed - using MediaElement instead
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error happened when ringing the alarm.");
                Dispose();
            }
        });
    }

    // PlayerNotificationManager removed - using MediaElement instead
    // Notification handling is now managed by the MediaElement service

    private bool _isDisposed;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;

        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // playbackService (IPlaybackService) and IServiceScopeFactory are singletons
        // and should not be disposed here as they are managed by the DI container

        Disposed?.Invoke(this, true);
    }
}