using Android.Content;
using Android.OS;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Infrastructure;
using Bible.Alarm.Platforms.Android.Services.Handlers;
using Bible.Alarm.Platforms.Android.Services.Platform;
using Bible.Alarm.Services.Infrastructure;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.BroadcastReceivers;

[BroadcastReceiver(Enabled = true)]
public class AlarmRingerReceiver : BroadcastReceiver, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<AlarmRingerReceiver>();

    private Context _context;
    private Intent _intent;
    private AndroidAlarmHandler _alarmHandler;

    private static readonly SemaphoreSlim Lock = new(1);

    public AlarmRingerReceiver()
    {
        LogSetup.Initialize(VersionFinder.Default,
            new string[] { $"AndroidSdk {Build.VERSION.SdkInt}" }, "Android");

        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
    }

    private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error(e.Exception, "Unobserved task exception.");
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        Logger.Error("Unhandled exception.", e.SerializeObject());
    }

    public override async void OnReceive(Context context, Intent intent)
    {
        var pendingIntent = GoAsync();

        await Lock.WaitAsync();

        try
        {
            _context = context;
            _intent = intent;

            // Ensure DI container is initialized for background service
            // This will also initialize platform-specific bootstrap helpers
            MauiProgram.EnsureDiContainerInitialized();

            var scheduleId = intent.GetStringExtra("ScheduleId");
            var isImmediate = intent.GetBooleanExtra("IsImmediate", false);

            _alarmHandler = ServiceProviderManager.GetService<AndroidAlarmHandler>();
            _alarmHandler.Disposed += OnDisposed;
            await _alarmHandler.Handle(long.Parse(scheduleId), isImmediate);
        }
        catch (Exception e)
        {
            Logger.Error(e, "An error happened when creating the task to ring the alarm.");
            Dispose();
        }
        finally
        {
            Lock.Release();
            pendingIntent.Finish();
        }
    }

    private void OnDisposed(object sender, bool e)
    {
        Dispose(true);
    }

    private bool _disposed = false;

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (_alarmHandler != null) _alarmHandler.Disposed -= OnDisposed;

        _context?.StopService(_intent);

        AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

        _disposed = true;

        base.Dispose(disposing);
    }
}