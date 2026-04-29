using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.Android.Services.BroadcastReceivers;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Constants;
using Java.Lang;
using Serilog;
using static Android.App.AlarmManager;
using Exception = System.Exception;

namespace Bible.Alarm.Platforms.Android.Services.AndroidServices;

[Service(Enabled = true)]
public class AlarmSetupService : Service, IDisposable
{
    private static readonly ILogger logger = Log.ForContext<AlarmSetupService>();


    public static bool IsRunning;

    public AlarmSetupService()
    {
        LogSetup.Initialize(new AssemblyAppVersionFinder(),
            [$"AndroidSdk {Build.VERSION.SdkInt}"], AppConstants.Platform.Android);

        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
    }

    private const int CrashFlushDelayMs = 500;

    private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        logger.Error(e.Exception, AppConstants.Logging.ProcessDiagnosticsLog.UnobservedTaskException);
        FlushAndDelay();
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
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
        catch
        {
        }

        System.Threading.Thread.Sleep(CrashFlushDelayMs);
    }

    public override IBinder OnBind(Intent intent) => null;

    public override void OnCreate()
    {
        // Create MediaSession as the very first thing - even before MAUI services are registered
        // This ensures MediaSession is available immediately on process start
        try
        {
            Platforms.Android.Services.Media.MediaSessionHelper.Create();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "AlarmSetupService.OnCreate: failed to create MediaSession");
        }

        base.OnCreate();
        IsRunning = true;
    }

    [return: GeneratedEnum]
    public override StartCommandResult OnStartCommand(Intent intent, [GeneratedEnum] StartCommandFlags flags,
        int startId)
    {
        // Create MediaSession as the very first thing - even before MAUI services are registered
        // This ensures MediaSession is available immediately on process start
        try
        {
            Platforms.Android.Services.Media.MediaSessionHelper.Create();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "AlarmSetupService.OnStartCommand: failed to create MediaSession");
        }

        // Ensure MauiApp is created exactly once (thread-safe)
        // This is a background service entry point
        MauiAppHolder.CreateAndStore();
        // Run bootstrapper after CreateAndStore for background launch
        MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);

        // Wait for bootstrap to complete before using database services
        // Run asynchronously to avoid blocking OnStartCommand
        _ = Task.Run(async () =>
        {
            try
            {
                await MauiProgram.WaitForBootstrapAsync();
            }
            catch (Exception ex)
            {
                // Log but don't fail - service can continue without waiting
                logger.Warning(ex, "Failed to wait for bootstrap in AlarmSetupService");
            }
        });

        try
        {
            var extra = intent.GetStringExtra("Action");

            switch (extra)
            {
                case "SetupBackgroundTasks":
                    Task.Run(async () =>
                    {
                        try
                        {
                            // ISchedulerService is a singleton, so don't dispose it
                            var schedulerService = ServiceProviderManager.GetService<ISchedulerService>();
                            await schedulerService.HandleAsync();
                        }
                        catch (Exception e)
                        {
                            logger.Error(e, "An error happened in handling scheduler task.");
                        }
                    });
                    break;
                default:
                    throw new NotImplementedException();
            }

            StopSelf();

            return base.OnStartCommand(intent, flags, startId);
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened in alarm setup task.");
            throw;
        }
    }

    public override void OnDestroy() => IsRunning = false;

    public static void ScheduleNotification(Context context, int scheduleId, DateTimeOffset time,
        string title, string body)
    {
        try
        {
            using var alarmIntent = new Intent(context, typeof(AlarmRingerReceiver));
            alarmIntent.PutExtra("ScheduleId", scheduleId.ToString());
            alarmIntent.PutExtra("IsAlarm", true);

            using var pIntent = PendingIntent.GetBroadcast(
                context,
                scheduleId,
                alarmIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            using var alarmService = (AlarmManager)context.GetSystemService(AlarmService);

            // Check if exact alarms can be scheduled (Android 12+)
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                if (!alarmService.CanScheduleExactAlarms())
                {
                    logger.Warning("Cannot schedule exact alarm for schedule {ScheduleId}. SCHEDULE_EXACT_ALARM permission may have been revoked by user.", scheduleId);
                    // Note: On Android 12+, user needs to grant this permission in system settings
                    // The app should guide users to Settings > Apps > Bible Alarm > Alarms & reminders
                    return;
                }
            }

            // Figure out the alarm in milliseconds.
            var milliSecondsRemaining = JavaSystem.CurrentTimeMillis()
                                        + (long)time.Subtract(DateTimeOffset.Now).TotalSeconds * 1000;

            if (Build.VERSION.SdkInt < BuildVersionCodes.M)
            {
                alarmService.SetExact(AlarmType.RtcWakeup, milliSecondsRemaining, pIntent);
            }
            else
            {
                using var mainLauncherIntent = new Intent(context, typeof(MainActivity));
                mainLauncherIntent.SetFlags(ActivityFlags.ReorderToFront);

                var mainLauncherPendingIntent = PendingIntent.GetActivity(
                    context,
                    0,
                    mainLauncherIntent,
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

                alarmService.SetAlarmClock(new AlarmClockInfo(milliSecondsRemaining, mainLauncherPendingIntent),
                    pIntent);
            }
        }
        catch (SecurityException ex)
        {
            logger.Error(ex, "SecurityException when scheduling alarm for schedule {ScheduleId}. SCHEDULE_EXACT_ALARM permission may be missing or revoked.", scheduleId);
            // Re-throw to be handled by caller
            throw;
        }
    }

    private bool disposed;

    protected override void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

        disposed = true;

        base.Dispose(disposing);
    }
}
