using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Database;
using Bible.Alarm.Platforms.Android.Services.UI;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Handlers;

public class AndroidAlarmHandler(
    ILogger logger,
    IPlaybackService playbackService,
    IServiceScopeFactory scopeFactory)
    : IAndroidAlarmHandler, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;


    private bool _playbackServiceInitialized;

    public event EventHandler<bool> Disposed;

    public async Task Handle(int scheduleId, bool isImmediate)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        
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
                DroidNotificationService.RemoveLocalNotification(schedule.Id);
                DroidNotificationService.ShowLocalNotification(schedule.Id,
                    string.IsNullOrEmpty(schedule.Name) ? "Bible Alarm" : schedule.Name,
                    "Press to start listening now.");
                Dispose();
                return;
            }

        if (schedule.NotificationEnabled) DroidNotificationService.RemoveLocalNotification(schedule.Id);

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
                _logger.Error(e, "An error happened when ringing the alarm.");
                Dispose();
            }
        });
    }

    // PlayerNotificationManager removed - using MediaElement instead
    // Notification handling is now managed by the MediaElement service

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // PlayerNotificationManager removed - using MediaElement instead

        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // notificationService (INotificationService) is a singleton
        // and should not be disposed here as it is managed by the DI container

        if (_playbackServiceInitialized)
        {
            // MediaManager removed - using MediaElement instead
        }

        Disposed?.Invoke(this, true);
    }
}