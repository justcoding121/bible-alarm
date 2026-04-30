using Android.Content;
using Android.OS;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.BroadcastReceivers;

[BroadcastReceiver(Enabled = true)]
public class AlarmRingerReceiver : BroadcastReceiver
{
    private static readonly ILogger logger = Log.ForContext<AlarmRingerReceiver>();

    private static readonly SemaphoreSlim @lock = new(1);

    public AlarmRingerReceiver()
    {
        LogSetup.Initialize(new AssemblyAppVersionFinder(),
            [$"AndroidSdk {Build.VERSION.SdkInt}"], AppConstants.Platform.Android);

        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;
    }

    private const int CrashFlushDelayMs = 500;

    private static void UnobservedTaskExceptionHandler(object sender, UnobservedTaskExceptionEventArgs e)
    {
        logger.Error(e.Exception, AppConstants.Logging.ProcessDiagnosticsLog.UnobservedTaskException);
        FlushAndDelay();
    }

    private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        logger.Error(e.ExceptionObject as Exception, AppConstants.Logging.ProcessDiagnosticsLog.UnhandledExceptionIsTerminating,
            e.IsTerminating);
        FlushAndDelay();
    }

    private static void FlushAndDelay()
    {
        try
        {
            Log.CloseAndFlush();
        }
        catch (Exception)
        {
            // Best-effort Serilog flush during crash path; suppress so delay still runs for log drain.
        }

        Thread.Sleep(CrashFlushDelayMs);
    }

    public override async void OnReceive(Context context, Intent intent)
    {
        try
        {
            Platforms.Android.Services.Media.MediaSessionHelper.Create();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.AndroidMediaSessionCreationDiagnosticsLog.AlarmRingerReceiverOnReceiveFailed);
        }

        var pendingIntent = GoAsync();

        try
        {
            await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
            {
                var scheduleId = intent.GetStringExtra("ScheduleId");
                var isAlarm = intent.GetBooleanExtra("IsAlarm", true);

                if (!string.IsNullOrEmpty(scheduleId) && isAlarm)
                {
                    await Platforms.Android.Services.Media.ForegroundServiceCoordinator.OnAlarmTriggered(context, int.Parse(scheduleId));
                }

                MauiAppHolder.CreateAndStore();

                await Task.Run(() =>
                {
                    MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                });

                await MauiProgram.WaitForBootstrapAsync();

                scheduleId = intent.GetStringExtra("ScheduleId");
                isAlarm = intent.GetBooleanExtra("IsAlarm", true);

                var alarmHandler = ServiceProviderManager.GetService<IAndroidAlarmHandler>();
                await alarmHandler.HandleAsync(int.Parse(scheduleId), isAlarm);

                if (isAlarm && !string.IsNullOrEmpty(scheduleId))
                {
                    try
                    {
                        var schedulerService = ServiceProviderManager.GetService<ISchedulerService>();
                        if (schedulerService != null)
                        {
                            await schedulerService.RescheduleNextOccurrenceAsync(int.Parse(scheduleId));
                            logger.Information("Rescheduled next occurrence for schedule {ScheduleId} after alarm fired", scheduleId);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Warning(ex, "Failed to reschedule next occurrence for schedule {ScheduleId} after alarm fired", scheduleId);
                    }
                }
            });
        }
        catch (Exception e)
        {
            logger.Error(e, AppConstants.Logging.AlarmDiagnostics.CreatingAlarmRingTaskFailed);
            Platforms.Android.Services.Media.ForegroundServiceCoordinator.StopAlarmForegroundServiceIfActive();
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException -= UnobservedTaskExceptionHandler;
            pendingIntent.Finish();
        }
    }
}
