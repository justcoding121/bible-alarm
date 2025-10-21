using Android.App;
using Android.Content;
using Android.OS;
using Bible.Alarm.Droid.Services.Platform;
// using Bible.Alarm.Services.Droid.Extensions; // Removed - no longer needed
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Infrastructure;
using Bible.Alarm.Services.Tasks;
using Serilog;

namespace Bible.Alarm.Droid.Services.Tasks;

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
            BootstrapHelper.InitializeService(context);

            BootstrapHelper.VerifyBackgroundTasks(context);

            try
            {
                await BootstrapHelper.VerifyServices();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, $"Failed to process restart task: copy media index. Intent action {intent.Action}");
            }

            using var schedulerTask = ServiceProviderManager.GetService<SchedulerTask>();
            await schedulerTask.Handle();

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
            // _container = null; // No longer needed
            BootstrapHelper.Remove(_context);
        }

        AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException -= UnobserverdTaskException;

        _disposed = true;

        base.Dispose();
    }
}