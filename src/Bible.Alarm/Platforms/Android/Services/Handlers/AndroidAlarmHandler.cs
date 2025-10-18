using Bible.Alarm.Contracts.Media;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Services.Droid;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Droid.Services.Handlers;

public class AndroidAlarmHandler(
    IPlaybackService playbackService,
    ScheduleDbContext dbContext,
    DroidNotificationService notificationService)
    : IAndroidAlarmHandler, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<AndroidAlarmHandler>();


    private bool _playbackServiceInitialized = false;

    public event EventHandler<bool> Disposed;

    public async Task Handle(long scheduleId, bool isImmediate)
    {
        var schedule = await dbContext.AlarmSchedules.FirstOrDefaultAsync(x => x.Id == scheduleId);

        if (schedule == null)
        {
            Dispose();
            return;
        }

        //local notification for android
        if (!isImmediate)
            if (schedule.NotificationEnabled)
            {
                notificationService.RemoveLocalNotification(schedule.Id);
                notificationService.ShowLocalNotification(schedule.Id,
                    string.IsNullOrEmpty(schedule.Name) ? "Bible Alarm" : schedule.Name,
                    "Press to start listening now.");
                Dispose();
                return;
            }

        if (schedule.NotificationEnabled) notificationService.RemoveLocalNotification(schedule.Id);

        // MediaManager removed - using MediaElement instead

        await Task.Run(async () =>
        {
            try
            {
                await playbackService.PrepareAndPlay(scheduleId, isImmediate);

                _playbackServiceInitialized = true;

                // Notification manager removed - using MediaElement instead
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when ringing the alarm.");
                Dispose();
            }
        });
    }

    // PlayerNotificationManager removed - using MediaElement instead
    // Notification handling is now managed by the MediaElement service

    private bool _disposed = false;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // PlayerNotificationManager removed - using MediaElement instead

        dbContext.Dispose();
        notificationService.Dispose();

        if (_playbackServiceInitialized)
        {
            // MediaManager removed - using MediaElement instead
        }

        Disposed?.Invoke(this, true);
    }
}