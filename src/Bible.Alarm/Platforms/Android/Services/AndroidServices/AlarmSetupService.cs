using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Services.BroadcastReceivers;
using Bible.Alarm.Platforms.Android.Services.Platform;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Java.Lang;
using Serilog;
using static Android.App.AlarmManager;
using Exception = System.Exception;

namespace Bible.Alarm.Platforms.Android.Services.AndroidServices;

[Service(Enabled = true)]
public class AlarmSetupService : Service, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<AlarmSetupService>();


    public static bool IsRunning;

    public AlarmSetupService()
    {
        LogSetup.Initialize(AndroidVersionFinder.Default,
            [$"AndroidSdk {Build.VERSION.SdkInt}"], DevicePlatform.Android.ToString());

        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
    }

    private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error(e.Exception, "Unobserved task exception.");
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        Logger.Error(e.ExceptionObject as Exception, "Unhandled exception. IsTerminating: {IsTerminating}",
            e.IsTerminating);
    }

    public override IBinder OnBind(Intent intent)
    {
        return null;
    }

    public override void OnCreate()
    {
        base.OnCreate();
        IsRunning = true;
    }

    [return: GeneratedEnum]
    public override StartCommandResult OnStartCommand(Intent intent, [GeneratedEnum] StartCommandFlags flags,
        int startId)
    {
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
                Log.Logger.Warning(ex, "Failed to wait for bootstrap in AlarmSetupService");
            }
        });

        try
        {
            var extra = intent.GetStringExtra("Action");

            switch (extra)
            {
                case "Add":
                    {
                        var time = DateTimeOffset.Parse(intent.GetStringExtra("Time"));
                        var title = intent.GetStringExtra("Title");
                        var body = intent.GetStringExtra("Body");
                        ScheduleNotification(ApplicationContext, int.Parse(intent.GetStringExtra("ScheduleId")), time,
                            title, body);
                        break;
                    }
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
                            Logger.Error(e, "An error happened in handling scheduler task.");
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
            Logger.Error(e, "An error happened in alarm setup task.");
            throw;
        }
    }

    public override void OnDestroy()
    {
        IsRunning = false;
    }

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
                (int)scheduleId,
                alarmIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            using var alarmService = (AlarmManager)context.GetSystemService(AlarmService);

            // Check if exact alarms can be scheduled (Android 12+)
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            {
#pragma warning disable CA1416
                if (!alarmService.CanScheduleExactAlarms())
                {
                    Logger.Warning("Cannot schedule exact alarm for schedule {ScheduleId}. SCHEDULE_EXACT_ALARM permission may have been revoked by user.", scheduleId);
                    // Note: On Android 12+, user needs to grant this permission in system settings
                    // The app should guide users to Settings > Apps > Bible Alarm > Alarms & reminders
                    return;
                }
#pragma warning restore CA1416
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
            Logger.Error(ex, "SecurityException when scheduling alarm for schedule {ScheduleId}. SCHEDULE_EXACT_ALARM permission may be missing or revoked.", scheduleId);
            // Re-throw to be handled by caller
            throw;
        }
    }

    private bool _disposed;

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

        _disposed = true;

        base.Dispose(disposing);
    }
}