using Android.App;
using Android.Content;
using Android.OS;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Services.Platform;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.BroadcastReceivers;

[BroadcastReceiver(Enabled = true, DirectBootAware = true, Exported = true)]
[IntentFilter([
    Intent.ActionBootCompleted, Intent.ActionLockedBootCompleted,
    "android.intent.action.QUICKBOOT_POWERON", "com.htc.intent.action.QUICKBOOT_POWERON",
    "com.Bible.Alarm.Restart"
])]
public class RestartReceiver : BroadcastReceiver, IDisposable
{
    private readonly ILogger _logger;

    private Context _context;

    public RestartReceiver() : this(Log.ForContext<RestartReceiver>())
    {
    }

    public RestartReceiver(ILogger logger)
    {
        _logger = logger;
        LogSetup.Initialize(AndroidVersionFinder.Default,
            [$"AndroidSdk {Build.VERSION.SdkInt}"], DevicePlatform.Android.ToString());

        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
    }

    private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.Error(e.Exception, "Unobserved task exception.");
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        _logger.Error(e.ExceptionObject as Exception, "Unhandled exception. IsTerminating: {IsTerminating}",
            e.IsTerminating);
    }

    public override async void OnReceive(Context context, Intent intent)
    {
        _context = context;

        var pendingIntent = GoAsync();

        try
        {
            // Initialize DI container for background service
            MauiAppHolder.CreateAndStore();
            // Run bootstrapper after CreateAndStore for background launch
            MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);

            // Wait for bootstrap to complete before using database services
            await MauiProgram.WaitForBootstrapAsync();

            // ISchedulerService is a singleton, so don't dispose it
            var schedulerService = ServiceProviderManager.GetService<ISchedulerService>();
            await schedulerService.HandleAsync();

            context.StopService(intent);
        }
        catch (Exception e)
        {
            _logger.Error(e, $"Failed to process restart task. Intent action {intent.Action}");
        }
        finally
        {
            pendingIntent.Finish();
        }
    }

    private bool _disposed;

    public new void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }

        AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

        _disposed = true;

        base.Dispose();

        GC.SuppressFinalize(this);
    }
}