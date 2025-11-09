using Android.App;
using Android.Content;
using Android.OS;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Android.Services.Platform;


using Bible.Alarm.Services.Scheduler;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.BroadcastReceivers;

[BroadcastReceiver(Enabled = true, DirectBootAware = true, Exported = true)]
[IntentFilter(new[]
{
    Intent.ActionBootCompleted, Intent.ActionLockedBootCompleted,
    "android.intent.action.QUICKBOOT_POWERON", "com.htc.intent.action.QUICKBOOT_POWERON",
    "com.Bible.Alarm.Restart"
})]
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
        LogSetup.Initialize(VersionFinder.Default,
            new string[] { $"AndroidSdk {Build.VERSION.SdkInt}" }, DevicePlatform.Android.ToString());

        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobserverdTaskException;
    }

    private void UnobserverdTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.Error(e.Exception, "Unobserved task exception.");
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        _logger.Error("Unhandled exception.", e.SerializeObject());
    }

    public override async void OnReceive(Context context, Intent intent)
    {
        _context = context;

        var pendingIntent = GoAsync();

        try
        {
            // Ensure DI container is initialized for background service
            // This will also initialize platform-specific bootstrap helpers
            MauiProgram.EnsureDiContainerInitialized();
            using var schedulerService = ServiceProviderManager.GetService<SchedulerService>();
            await schedulerService.Handle();

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

    private bool _disposed = false;

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
    }
}