using Android.Content;
using Android.OS;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Platforms.Android.Services.Handlers;
using Bible.Alarm.Platforms.Android.Services.Platform;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.BroadcastReceivers;

[BroadcastReceiver(Enabled = true)]
public class AlarmRingerReceiver : BroadcastReceiver, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<AlarmRingerReceiver>();

    private Context _context;
    private Intent _intent;
    private IAndroidAlarmHandler _alarmHandler;

    private static readonly SemaphoreSlim Lock = new(1);

    public AlarmRingerReceiver()
    {
        LogSetup.Initialize(AndroidVersionFinder.Default,
            [$"AndroidSdk {Build.VERSION.SdkInt}"], "Android");

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

    public override async void OnReceive(Context context, Intent intent)
    {
        var pendingIntent = GoAsync();

        try
        {
            await ConcurrencyHelper.ExecuteAsync(Lock, async () =>
            {
                _context = context;
                _intent = intent;

                // Initialize DI container for background service
                MauiAppHolder.CreateAndStore();

                // Run bootstrapper asynchronously to avoid blocking the receiver thread
                // This is critical for BroadcastReceivers which must not block
                await Task.Run(() =>
                {
                    MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                });

                // Wait for bootstrap to complete before using database services
                await MauiProgram.WaitForBootstrapAsync();

                var scheduleId = intent.GetStringExtra("ScheduleId");
                var isAlarm = intent.GetBooleanExtra("IsAlarm", true);

                _alarmHandler = ServiceProviderManager.GetService<IAndroidAlarmHandler>();
                // Subscribe to Disposed event if the handler implements it
                if (_alarmHandler is AndroidAlarmHandler concreteHandler)
                {
                    concreteHandler.Disposed += OnDisposed;
                }
                await _alarmHandler.HandleAsync(int.Parse(scheduleId), isAlarm);
            });
        }
        catch (Exception e)
        {
            Logger.Error(e, "An error happened when creating the task to ring the alarm.");
            Dispose();
        }
        finally
        {
            pendingIntent.Finish();
        }
    }

    private void OnDisposed(object sender, bool e)
    {
        Dispose(true);
    }

    private bool _disposed;

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (_alarmHandler is AndroidAlarmHandler concreteHandler)
        {
            concreteHandler.Disposed -= OnDisposed;
        }

        _context?.StopService(_intent);

        AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

        _disposed = true;

        base.Dispose(disposing);
    }
}