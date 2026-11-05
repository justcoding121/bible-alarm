#nullable enable

using Android.Content;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Services.Media3;
using Bible.Alarm.Platforms.Android.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Keeps the alarm process in the foreground until the Media3 library service is running
/// and playback has started. The library service owns the media notification.
/// </summary>
public static class ForegroundServiceCoordinator
{
    private static readonly ILogger logger = Log.ForContext(typeof(ForegroundServiceCoordinator));
    private static readonly Lock @lock = new();
    private static AlarmForegroundService? alarmService;

    /// <summary>
    /// Starts the Media3 library service, then releases the alarm foreground service.
    /// Playback is already running when this is called.
    /// </summary>
    public static Task OnPlaybackStarted()
    {
        var context = global::Android.App.Application.Context;
        if (context != null)
        {
            var intent = new Intent(context, typeof(BibleAlarmMediaLibraryService));
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
        }

        if (BibleAlarmMediaLibraryService.IsForeground)
        {
            BibleAlarmMediaLibraryService.ReleaseAlarmIfPlaybackRunning();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts <see cref="AlarmForegroundService"/> so the process survives bootstrap after an alarm.
    /// </summary>
    public static async Task OnAlarmTriggered(Context context, int scheduleId)
    {
        lock (@lock)
        {
            if (alarmService != null)
            {
                logger.Information("Alarm triggered (schedule {ScheduleId}) while the alarm foreground service is already running", scheduleId);
                return;
            }

            logger.Information("Alarm triggered (schedule {ScheduleId}) - starting foreground service to prevent OS kill", scheduleId);
        }

        var intent = new Intent(context, typeof(AlarmForegroundService));
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }

            var waitCount = 0;
            while (AlarmForegroundService.Instance == null && waitCount < 10)
            {
                await Task.Delay(50);
                waitCount++;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to start AlarmForegroundService");
            return;
        }

        var service = AlarmForegroundService.Instance;
        if (service == null)
        {
            logger.Warning("AlarmForegroundService instance not available after starting");
            return;
        }

        _ = SessionStatePlayer.Instance;
        var hasMetadata = !string.IsNullOrEmpty(ServiceProviderManager.GetService<IMediaSessionManager>()?.Snapshot.Title);
        lock (@lock)
        {
            alarmService = service;
            if (hasMetadata)
            {
                logger.Information("Alarm foreground service starting with existing session metadata");
            }

            ForegroundServiceOperations.StartForeground(service, isAlarmNotification: true);
            logger.Information("Alarm foreground service started with preparing notification");
        }
    }

    /// <summary>
    /// Stops the alarm foreground service when the user must tap before playback, or after playback starts.
    /// </summary>
    public static void StopAlarmForegroundServiceIfActive()
    {
        AlarmForegroundService? alarmServiceToStop = null;
        lock (@lock)
        {
            if (alarmService == null)
            {
                return;
            }

            logger.Information("Stopping alarm foreground service");
            alarmServiceToStop = alarmService;
            ForegroundServiceOperations.StopForeground(alarmService);
            alarmService = null;
        }

        if (alarmServiceToStop == null)
        {
            return;
        }

        try
        {
            alarmServiceToStop.StopSelf();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error stopping AlarmForegroundService");
        }
    }
}
